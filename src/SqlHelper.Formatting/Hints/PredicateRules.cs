using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>SH1xx: <c>WHERE</c> and <c>ON</c> conditions that stop SQL Server seeking an index.</summary>
    internal static class PredicateRules
    {
        const int LongInList = 100;

        public static void Check(HintContext c)
        {
            foreach (var comparison in c.Tree.All<BooleanComparisonExpression>())
            {
                if (c.Filter(comparison) == FilterKind.None) continue;
                ColumnSide(c, comparison.FirstExpression);
                ColumnSide(c, comparison.SecondExpression);
            }
            foreach (var like in c.Tree.All<LikePredicate>())
            {
                if (c.Filter(like) == FilterKind.None) continue;
                ColumnSide(c, like.FirstExpression);
                LeadingWildcard(c, like);
            }
            foreach (var @in in c.Tree.All<InPredicate>())
            {
                if (c.Filter(@in) == FilterKind.None) continue;
                ColumnSide(c, @in.Expression);
                if (@in.Values.Count > LongInList)
                    c.Suggestion("SH110", @in, $"An IN list of {@in.Values.Count} values is slow to compile and hard to read. Put the values in a temp table or table-valued parameter and join to it.");
            }
            foreach (var between in c.Tree.All<BooleanTernaryExpression>())
            {
                if (c.Filter(between) == FilterKind.None) continue;
                ColumnSide(c, between.FirstExpression);
            }
            foreach (var or in c.Tree.All<BooleanBinaryExpression>())
            {
                if (or.BinaryExpressionType != BooleanBinaryExpressionType.Or || !IsOutermostOr(c, or)) continue;
                var filter = c.Filter(or);
                if (filter == FilterKind.None) continue;
                var parts = HintContext.Disjuncts(or).ToList();
                if (CatchAll(c, or, parts)) continue;
                if (filter == FilterKind.On)
                    c.Warning("SH109", or, "OR in a join condition usually forces a scan or a nested loop over every row. Split the query into a UNION ALL of one query per alternative, or join twice.");
                else if (parts.Select(p => string.Join(",", c.Columns(p).Select(HintContext.ColumnKey).Distinct().OrderBy(k => k))).Where(k => k.Length > 0).Distinct().Count() > 1)
                    c.Suggestion("SH108", or, "OR across different columns usually means one index can't serve the condition, so SQL Server scans. Consider a UNION ALL of one query per column (each able to seek), if this is slow.");
            }
            foreach (var where in c.Tree.All<WhereClause>())
                NegativeOnly(c, where);
        }

        /// <summary>SH101–SH105, SH107: one side of a comparison that holds a column inside some calculation.</summary>
        static void ColumnSide(HintContext c, ScalarExpression side)
        {
            var expression = HintContext.Strip(side);
            if (expression == null || expression is ColumnReferenceExpression || !c.Columns(expression).Any()) return;
            switch (expression)
            {
                case FunctionCall call when call.CallTarget != null:
                    // A user-defined function: SH203.
                    return;
                case FunctionCall call when HintContext.NameIs(call.FunctionName, "ISNULL"):
                    if (IsVariable(call.Parameters.FirstOrDefault()))
                        CatchAllHint(c, call);
                    else
                        c.Warning("SH102", call, "ISNULL() around a column stops SQL Server seeking an index on it. Write the condition on the bare column, with an OR ... IS NULL if NULLs should match.");
                    return;
                case CoalesceExpression coalesce:
                    if (IsVariable(coalesce.Expressions.FirstOrDefault()))
                        CatchAllHint(c, coalesce);
                    else
                        c.Warning("SH102", coalesce, "COALESCE() around a column stops SQL Server seeking an index on it. Write the condition on the bare column, with an OR ... IS NULL if NULLs should match.");
                    return;
                case FunctionCall call when HintContext.NameIs(call.FunctionName, "DATEDIFF") || HintContext.NameIs(call.FunctionName, "DATEDIFF_BIG"):
                    c.Warning("SH103", call, $"{call.FunctionName.Value.ToUpperInvariant()}() on a column stops SQL Server seeking an index on it. Compare the bare column with a date worked out on the other side, e.g. col >= DATEADD(day, -7, GETDATE()).");
                    return;
                case CastCall cast when IsDate(cast.DataType):
                case ConvertCall convert when IsDate(convert.DataType):
                case TryCastCall tryCast when IsDate(tryCast.DataType):
                case TryConvertCall tryConvert when IsDate(tryConvert.DataType):
                    // Casting a datetime column to date can still seek.
                    return;
                case BinaryExpression binary:
                    if (HasStringLiteral(c, binary))
                        c.Warning("SH105", binary, "Joining columns into a string before comparing stops SQL Server seeking an index on any of them. Compare each column with its own value instead.");
                    else
                        c.Warning("SH104", binary, "Arithmetic on a column stops SQL Server seeking an index on it. Move the calculation to the other side, so the column stands alone.");
                    return;
                case UnaryExpression unary:
                    c.Warning("SH104", unary, "Arithmetic on a column stops SQL Server seeking an index on it. Move the calculation to the other side, so the column stands alone.");
                    return;
                default:
                    c.Warning("SH101", expression, $"{Describe(expression)} around a column stops SQL Server seeking an index on it. Compare the bare column instead (for a date part, with a range: col >= '20240101' AND col < '20250101').");
                    return;
            }
        }

        static string Describe(ScalarExpression expression)
        {
            switch (expression)
            {
                case FunctionCall call: return call.FunctionName.Value.ToUpperInvariant() + "()";
                case LeftFunctionCall _: return "LEFT()";
                case RightFunctionCall _: return "RIGHT()";
                case CastCall _: return "CAST()";
                case ConvertCall _: return "CONVERT()";
                case TryCastCall _: return "TRY_CAST()";
                case TryConvertCall _: return "TRY_CONVERT()";
                case NullIfExpression _: return "NULLIF()";
                case IIfCall _: return "IIF()";
                case CaseExpression _: return "A CASE expression";
                default: return "A calculation";
            }
        }

        static bool IsDate(DataTypeReference type) =>
            type is SqlDataTypeReference sql && sql.SqlDataTypeOption == SqlDataTypeOption.Date;

        static bool IsVariable(ScalarExpression expression) => HintContext.Strip(expression) is VariableReference;

        static bool HasStringLiteral(HintContext c, BinaryExpression binary) =>
            c.OwnDescendants(binary).OfType<StringLiteral>().Any();

        /// <summary>SH106: <c>LIKE '%x'</c>, <c>LIKE '_x'</c> and <c>LIKE '%' + @p</c>.</summary>
        static void LeadingWildcard(HintContext c, LikePredicate like)
        {
            var pattern = HintContext.Strip(like.SecondExpression);
            while (pattern is BinaryExpression concatenation && concatenation.BinaryExpressionType == BinaryExpressionType.Add)
                pattern = HintContext.Strip(concatenation.FirstExpression);
            if (pattern is StringLiteral literal && (literal.Value.StartsWith("%") || literal.Value.StartsWith("_")))
                c.Warning("SH106", like.SecondExpression, "A LIKE pattern that starts with a wildcard can't seek an index, so every row is read. Anchor the start of the pattern if you can, or use full-text search.");
        }

        /// <summary>
        /// SH107: <c>@p IS NULL OR col = @p</c>, the "optional parameter" condition. Returns true when the OR is one, so
        /// it isn't flagged again as an OR across columns.
        /// </summary>
        static bool CatchAll(HintContext c, BooleanBinaryExpression or, List<BooleanExpression> parts)
        {
            var optional = parts.OfType<BooleanIsNullExpression>()
                .Where(n => !n.IsNot && HintContext.Strip(n.Expression) is VariableReference)
                .Select(n => ((VariableReference)HintContext.Strip(n.Expression)).Name)
                .ToList();
            if (optional.Count == 0) return false;
            bool usesIt = parts.Any(p => !(p is BooleanIsNullExpression)
                && c.Tree.Descendants(p).OfType<VariableReference>().Any(v => optional.Contains(v.Name, StringComparer.OrdinalIgnoreCase))
                && c.Columns(p).Any());
            if (!usesIt) return false;
            CatchAllHint(c, or);
            return true;
        }

        static void CatchAllHint(HintContext c, TSqlFragment at)
        {
            if (HasRecompile(c, at)) return;
            c.Suggestion("SH107", at, "An optional-parameter condition gets one plan for every combination of parameters, which is usually a scan. Add OPTION (RECOMPILE) to the statement, or build the SQL with only the conditions that apply.");
        }

        static bool HasRecompile(HintContext c, TSqlFragment fragment) =>
            c.Tree.Ancestors(fragment).OfType<StatementWithCtesAndXmlNamespaces>()
                .Any(s => s.OptimizerHints.Any(h => h.HintKind == OptimizerHintKind.Recompile));

        static bool IsOutermostOr(HintContext c, BooleanBinaryExpression or)
        {
            var parent = c.Tree.Parent(or);
            while (parent is BooleanParenthesisExpression) parent = c.Tree.Parent(parent);
            return !(parent is BooleanBinaryExpression outer && outer.BinaryExpressionType == BooleanBinaryExpressionType.Or);
        }

        /// <summary>SH111: a <c>WHERE</c> whose every condition is a <c>&lt;&gt;</c>, <c>NOT LIKE</c> or <c>NOT IN (list)</c>.</summary>
        static void NegativeOnly(HintContext c, WhereClause where)
        {
            if (where.SearchCondition == null) return;
            var parts = HintContext.Conjuncts(where.SearchCondition).ToList();
            if (parts.All(IsNegative))
                c.Suggestion("SH111", where, "Every condition here is negative (<>, NOT LIKE or NOT IN), so no index can be used to find the rows. If most rows are excluded, say which ones are wanted instead.");
        }

        static bool IsNegative(BooleanExpression condition)
        {
            switch (condition)
            {
                case BooleanComparisonExpression comparison:
                    return comparison.ComparisonType == BooleanComparisonType.NotEqualToBrackets
                        || comparison.ComparisonType == BooleanComparisonType.NotEqualToExclamation;
                case LikePredicate like:
                    return like.NotDefined;
                case InPredicate @in:
                    // NOT IN (subquery) is SH801.
                    return @in.NotDefined && @in.Subquery == null;
                default:
                    // NOT EXISTS is an anti-join, which can seek.
                    return false;
            }
        }
    }
}