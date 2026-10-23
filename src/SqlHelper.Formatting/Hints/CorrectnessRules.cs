using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>SH8xx: mistakes that are cheap to spot, and often found while looking for slow code.</summary>
    internal static class CorrectnessRules
    {
        static readonly Regex EndOfDay = new Regex(@"23:59(:59)?", RegexOptions.CultureInvariant);

        /// <summary>A date written d/m/y or m/d/y (with /, - or .), optionally with a time.</summary>
        static readonly Regex AmbiguousDate = new Regex(@"^\s*\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4}(\s+\d{1,2}:\d{2}(:\d{2}(\.\d+)?)?)?\s*$", RegexOptions.CultureInvariant);

        public static void Check(HintContext c)
        {
            foreach (var @in in c.Tree.All<InPredicate>())
                if (@in.NotDefined && @in.Subquery != null)
                    c.Warning("SH801", @in, "NOT IN (subquery) returns no rows at all if the subquery returns a NULL, and is often planned less well than NOT EXISTS. Use NOT EXISTS (SELECT 1 FROM ... WHERE ...) instead.");
            foreach (var comparison in c.Tree.All<BooleanComparisonExpression>())
            {
                if (HintContext.Strip(comparison.FirstExpression) is NullLiteral || HintContext.Strip(comparison.SecondExpression) is NullLiteral)
                    c.Warning("SH802", comparison, "A comparison with NULL is never true (= NULL matches nothing, and <> NULL matches nothing either). Use IS NULL or IS NOT NULL.");
                else if (EndsTheDay(comparison.ComparisonType == BooleanComparisonType.LessThanOrEqualTo ? comparison.SecondExpression
                    : comparison.ComparisonType == BooleanComparisonType.GreaterThanOrEqualTo ? comparison.FirstExpression : null))
                    c.Warning("SH803", comparison, "An end of day written as 23:59:59 misses times later in that last second (and .999 rounds up to the next day for datetime). Use < the next day instead: col < '20240201'.");
            }
            foreach (var between in c.Tree.All<BooleanTernaryExpression>())
                if (EndsTheDay(between.ThirdExpression))
                    c.Warning("SH803", between, "BETWEEN with an end of day written as 23:59:59 misses times later in that last second (and .999 rounds up to the next day for datetime). Use col >= start AND col < the next day instead.");
            foreach (var type in c.Tree.All<SqlDataTypeReference>()) NoLength(c, type);
            foreach (var query in c.Tree.All<QuerySpecification>()) AssignFromManyRows(c, query);
            foreach (var literal in c.Tree.All<StringLiteral>())
            {
                if (!AmbiguousDate.IsMatch(literal.Value)) continue;
                // CONVERT(date, '01/02/2024', 103) says which way round it is.
                if (c.Tree.Parent(literal) is ConvertCall convert && convert.Style != null) continue;
                c.Suggestion("SH806", literal, $"'{literal.Value}' means a different date depending on the login's language and DATEFORMAT setting. Write dates as 'yyyymmdd' (or 'yyyy-mm-ddThh:mm:ss' with a time).");
            }
        }

        static bool EndsTheDay(ScalarExpression expression) =>
            HintContext.Strip(expression) is StringLiteral literal && EndOfDay.IsMatch(literal.Value);

        /// <summary>SH804: <c>VARCHAR</c> and friends with no length.</summary>
        static void NoLength(HintContext c, SqlDataTypeReference type)
        {
            switch (type.SqlDataTypeOption)
            {
                case SqlDataTypeOption.Char:
                case SqlDataTypeOption.VarChar:
                case SqlDataTypeOption.NChar:
                case SqlDataTypeOption.NVarChar:
                case SqlDataTypeOption.Binary:
                case SqlDataTypeOption.VarBinary:
                    break;
                default:
                    return;
            }
            if (type.Parameters.Count > 0) return;
            var parent = c.Tree.Parent(type);
            bool conversion = parent is CastCall || parent is ConvertCall || parent is TryCastCall || parent is TryConvertCall;
            string name = HintContext.Text(type).ToUpperInvariant();
            c.Warning("SH804", type, conversion
                ? $"{name} with no length in a conversion means {name}(30), so longer values are cut short without an error. Give the length."
                : $"{name} with no length here means {name}(1), so longer values are cut short to one character. Give the length.");
        }

        /// <summary>SH805: <c>SELECT @x = col FROM t</c> with nothing to make it one row: the variable gets the value from an arbitrary row.</summary>
        static void AssignFromManyRows(HintContext c, QuerySpecification query)
        {
            var assignments = query.SelectElements.OfType<SelectSetVariable>().ToList();
            if (assignments.Count == 0 || query.FromClause == null) return;
            if (query.WhereClause != null || query.TopRowFilter != null || query.GroupByClause != null || query.OffsetClause != null) return;
            if (query.SelectElements.Any(e => c.HasAggregate(e))) return;
            // SELECT @list = @list + name builds a string from every row on purpose.
            if (assignments.Any(a => a.AssignmentKind != AssignmentKind.Equals
                || c.Tree.Descendants(a.Expression).OfType<VariableReference>().Any(v => string.Equals(v.Name, a.Variable.Name, System.StringComparison.OrdinalIgnoreCase))))
                return;
            // Triggers reading inserted / deleted are SH206.
            if (c.Module(query) is TriggerStatementBody) return;
            c.Suggestion("SH805", query, "This sets a variable from a query that can return many rows, so it ends up with the value from whichever row comes last. Add a WHERE or TOP (1) ... ORDER BY that picks the row you want.");
        }
    }
}