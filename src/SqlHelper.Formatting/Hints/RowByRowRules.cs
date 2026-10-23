using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>SH2xx: work done a row at a time.</summary>
    internal static class RowByRowRules
    {
        /// <summary>Methods of xml, hierarchyid and the spatial types, which look like schema-qualified function calls.</summary>
        static readonly HashSet<string> TypeMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "value", "query", "exist", "nodes", "modify",
            "GetAncestor", "GetDescendant", "GetLevel", "GetReparentedValue", "IsDescendantOf", "ToString",
            "STAsText", "STDistance", "STIntersects", "STContains", "STWithin", "STBuffer", "STArea", "STLength", "Lat", "Long",
        };

        public static void Check(HintContext c)
        {
            foreach (var cursor in c.Tree.All<DeclareCursorStatement>()) Cursor(c, cursor, cursor.CursorDefinition);
            foreach (var set in c.Tree.All<SetVariableStatement>())
                if (set.CursorDefinition != null) Cursor(c, set, set.CursorDefinition);
            foreach (var loop in c.Tree.All<WhileStatement>()) Loop(c, loop);
            foreach (var call in c.Tree.All<FunctionCall>()) ScalarFunction(c, call);
            foreach (var function in c.Tree.All<FunctionStatementBody>())
                if (function.ReturnType is TableValuedFunctionReturnType)
                    c.Warning("SH204", function.Name, "A multi-statement table-valued function is a black box to the optimiser, which guesses its row count. Write it as an inline function (RETURNS TABLE AS RETURN SELECT ...) if you can.");
            foreach (var subquery in c.Tree.All<ScalarSubquery>()) CorrelatedInSelectList(c, subquery);
            foreach (var trigger in c.Tree.All<TriggerStatementBody>()) SingleRowTrigger(c, trigger);
            foreach (var cte in c.Tree.All<CommonTableExpression>()) NumberGenerator(c, cte);
            foreach (var sort in c.Tree.All<ExpressionWithSortOrder>())
                if (HintContext.Strip(sort.Expression) is FunctionCall call && call.CallTarget == null && HintContext.NameIs(call.FunctionName, "NEWID"))
                    c.Warning("SH208", sort, "ORDER BY NEWID() gives every row a value and sorts the lot to pick a few at random. On a big table, use TABLESAMPLE or pick random keys instead.");
        }

        /// <summary>SH201.</summary>
        static void Cursor(HintContext c, TSqlFragment at, CursorDefinition definition)
        {
            var options = definition?.Options.Select(o => o.OptionKind).ToList() ?? new List<CursorOptionKind>();
            bool cheap = options.Contains(CursorOptionKind.Local)
                && (options.Contains(CursorOptionKind.FastForward) || options.Contains(CursorOptionKind.Static));
            c.Warning("SH201", at, "A cursor works through rows one at a time, which is usually far slower than one set-based statement."
                + (cheap ? "" : " If it has to stay, declare it LOCAL FAST_FORWARD."));
        }

        /// <summary>SH202: a <c>WHILE</c> loop that reads or writes tables (not a cursor's fetch loop, nor a batching loop).</summary>
        static void Loop(HintContext c, WhileStatement loop)
        {
            if (c.Tree.Descendants(loop.Predicate).OfType<GlobalVariableExpression>().Any(g => g.Name.Equals("@@FETCH_STATUS", StringComparison.OrdinalIgnoreCase)))
                return;
            var body = c.Tree.Descendants(loop.Statement).ToList();
            // DELETE TOP (1000) ... in a loop is the usual way to change a big table in batches.
            if (body.OfType<DeleteSpecification>().Any(d => d.TopRowFilter != null) || body.OfType<UpdateSpecification>().Any(u => u.TopRowFilter != null))
                return;
            if (body.OfType<NamedTableReference>().Any(t => !HintContext.IsTempOrVariable(t.SchemaObject)) || body.OfType<DataModificationSpecification>().Any())
                c.Suggestion("SH202", loop, "This loop reads or changes tables a row (or a few rows) at a time. One set-based statement over all the rows is usually much faster.");
        }

        /// <summary>SH203: a call to a user-defined scalar function in a select list or a filter.</summary>
        static void ScalarFunction(HintContext c, FunctionCall call)
        {
            if (!(call.CallTarget is MultiPartIdentifierCallTarget target) || TypeMethods.Contains(call.FunctionName.Value)) return;
            var schema = target.MultiPartIdentifier.Identifiers.Last();
            if (HintContext.NameIs(schema, "sys")) return;
            bool inSelectList = c.Tree.Ancestors(call).TakeWhile(a => !(a is QuerySpecification)).Any(a => a is SelectElement);
            var condition = c.Tree.Ancestors(call).OfType<BooleanExpression>().FirstOrDefault();
            bool inFilter = condition != null && c.Filter(condition) != FilterKind.None
                && !c.Tree.Ancestors(call).TakeWhile(a => a != condition).Any(HintContext.IsQueryBoundary);
            if (inSelectList || inFilter)
                c.Suggestion("SH203", call, $"The scalar function {HintContext.Text(target)}.{call.FunctionName.Value}() runs once per row and stops the query going parallel (unless SQL Server 2019 or later can inline it). An inline table-valued function with CROSS APPLY, or the expression written out, is usually faster.");
        }

        /// <summary>SH205: a subquery in the select list that refers to the outer query's tables.</summary>
        static void CorrelatedInSelectList(HintContext c, ScalarSubquery subquery)
        {
            var element = c.Tree.Parent(subquery);
            while (element is ParenthesisExpression) element = c.Tree.Parent(element);
            if (!(element is SelectScalarExpression) || !(c.Tree.Parent(element) is QuerySpecification outer) || outer.FromClause == null) return;
            var outerNames = new HashSet<string>(outer.FromClause.TableReferences.SelectMany(HintContext.ExposedNames), StringComparer.OrdinalIgnoreCase);
            var innerNames = new HashSet<string>(c.Tree.Descendants(subquery).OfType<FromClause>().SelectMany(f => f.TableReferences).SelectMany(HintContext.ExposedNames),
                StringComparer.OrdinalIgnoreCase);
            bool correlated = c.Tree.Descendants(subquery).OfType<ColumnReferenceExpression>()
                .Select(HintContext.Qualifier).Any(q => q != null && outerNames.Contains(q) && !innerNames.Contains(q));
            if (correlated)
                c.Suggestion("SH205", subquery, "A subquery in the select list that refers to the outer query runs once per row unless SQL Server can turn it into a join. A JOIN or OUTER APPLY, with any aggregate grouped once, is usually faster, especially when several subqueries read the same table.");
        }

        /// <summary>SH206: a trigger that reads <c>inserted</c> or <c>deleted</c> into a variable, as if one row changed.</summary>
        static void SingleRowTrigger(HintContext c, TriggerStatementBody trigger)
        {
            foreach (var query in c.Tree.Descendants(trigger).OfType<QuerySpecification>())
            {
                if (query.FromClause == null || !query.FromClause.TableReferences.SelectMany(TablesIn).Any(IsInsertedOrDeleted)) continue;
                // An aggregate (SELECT @n = COUNT(*) FROM inserted) is one row however many changed.
                if (query.SelectElements.Any(e => c.HasAggregate(e))) continue;
                bool assigns = query.SelectElements.OfType<SelectSetVariable>().Any();
                if (!assigns && c.Tree.Parent(query) is ScalarSubquery subquery)
                {
                    var statement = c.Statement(subquery);
                    assigns = statement is SetVariableStatement || statement is DeclareVariableStatement;
                }
                if (assigns)
                    c.Warning("SH206", query, "This reads inserted or deleted into a variable, which keeps only one row. A statement can change many rows at once, so write the trigger set-based, joining to inserted / deleted.");
            }
        }

        static IEnumerable<NamedTableReference> TablesIn(TableReference reference)
        {
            switch (reference)
            {
                case NamedTableReference named:
                    yield return named;
                    break;
                case JoinTableReference join:
                    foreach (var table in TablesIn(join.FirstTableReference).Concat(TablesIn(join.SecondTableReference))) yield return table;
                    break;
                case JoinParenthesisTableReference parenthesis:
                    foreach (var table in TablesIn(parenthesis.Join)) yield return table;
                    break;
            }
        }

        static bool IsInsertedOrDeleted(NamedTableReference table) =>
            table.SchemaObject.SchemaIdentifier == null
            && (HintContext.NameIs(table.SchemaObject.BaseIdentifier, "inserted") || HintContext.NameIs(table.SchemaObject.BaseIdentifier, "deleted"));

        /// <summary>SH207: a recursive CTE whose anchor reads no table: counting up numbers or dates.</summary>
        static void NumberGenerator(HintContext c, CommonTableExpression cte)
        {
            if (!(cte.QueryExpression is BinaryQueryExpression union) || union.BinaryQueryExpressionType != BinaryQueryExpressionType.Union) return;
            var anchor = union.FirstQueryExpression;
            while (anchor is BinaryQueryExpression nested) anchor = nested.FirstQueryExpression;
            while (anchor is QueryParenthesisExpression parenthesis) anchor = parenthesis.QueryExpression;
            if (!(anchor is QuerySpecification anchorQuery) || anchorQuery.FromClause != null) return;
            bool recursive = c.Tree.Descendants(union.SecondQueryExpression).OfType<NamedTableReference>()
                .Any(t => t.SchemaObject.SchemaIdentifier == null && HintContext.NameIs(t.SchemaObject.BaseIdentifier, cte.ExpressionName.Value));
            if (recursive)
                c.Suggestion("SH207", cte.ExpressionName, "A recursive CTE that counts up builds its rows one at a time. A numbers (tally) table, or GENERATE_SERIES on SQL Server 2022 and later, is much faster.");
        }
    }
}