using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>SH3xx: the shape of a query.</summary>
    internal static class QueryShapeRules
    {
        public static void Check(HintContext c)
        {
            foreach (var star in c.Tree.All<SelectStarExpression>()) SelectStar(c, star);
            foreach (var with in c.Tree.All<WithCtesAndXmlNamespaces>()) CteReuse(c, with);
            foreach (var comparison in c.Tree.All<BooleanComparisonExpression>()) CountAboveZero(c, comparison);
            foreach (var query in c.Tree.All<QuerySpecification>())
            {
                if (query.UniqueRowFilter == UniqueRowFilter.Distinct && query.FromClause != null
                    && query.FromClause.TableReferences.Any(t => c.Tree.Descendants(t).Prepend(t).Any(d => d is QualifiedJoin)))
                    c.Suggestion("SH304", query, "DISTINCT on a query with joins often hides a join that matches more rows than intended, and sorts or hashes every row to remove them. Check the joins; EXISTS may say what's meant.");
                Top(c, query);
                CommaJoin(c, query);
                OuterJoinFilteredInWhere(c, query);
            }
            foreach (var union in c.Tree.All<BinaryQueryExpression>()) UnionCouldBeAll(c, union);
            foreach (var join in c.Tree.All<QualifiedJoin>()) SubqueryInOn(c, join);
        }

        /// <summary>SH301: <c>SELECT *</c>, except in <c>EXISTS</c> and over a CTE or derived table that lists its columns.</summary>
        static void SelectStar(HintContext c, SelectStarExpression star)
        {
            if (!(c.Tree.Parent(star) is QuerySpecification query)) return;
            if (c.Tree.Ancestors(query).TakeWhile(a => !(a is TSqlStatement)).Any(a => a is ExistsPredicate)) return;
            if (query.FromClause != null && query.FromClause.TableReferences.SelectMany(t => TableSources(c, t)).All(s => s == null))
                return;
            if (c.Module(star) != null)
                c.Warning("SH301", star, "SELECT * in a view, procedure or function reads every column, including ones added later, and can stop an index covering the query. List the columns.");
            else
                c.Suggestion("SH301", star, "SELECT * reads every column, which can stop an index covering the query. List the columns, unless this is a quick look at the data.");
        }

        /// <summary>The tables a FROM reads: a null for each CTE or derived table (whose columns are already chosen).</summary>
        static IEnumerable<NamedTableReference> TableSources(HintContext c, TableReference reference)
        {
            switch (reference)
            {
                case JoinTableReference join:
                    return TableSources(c, join.FirstTableReference).Concat(TableSources(c, join.SecondTableReference));
                case JoinParenthesisTableReference parenthesis:
                    return TableSources(c, parenthesis.Join);
                case NamedTableReference named when named.SchemaObject.SchemaIdentifier == null && IsCteName(c, named):
                    return new NamedTableReference[] { null };
                case NamedTableReference named:
                    return new[] { named };
                case QueryDerivedTable _:
                    return new NamedTableReference[] { null };
                default:
                    // A table-valued function, variable, VALUES list: treat as a table.
                    return new[] { new NamedTableReference() };
            }
        }

        static bool IsCteName(HintContext c, NamedTableReference table) =>
            c.Tree.Ancestors(table).OfType<StatementWithCtesAndXmlNamespaces>()
                .Any(s => s.WithCtesAndXmlNamespaces != null
                    && s.WithCtesAndXmlNamespaces.CommonTableExpressions.Any(cte => HintContext.NameIs(cte.ExpressionName, table.SchemaObject.BaseIdentifier.Value)));

        /// <summary>SH302: a CTE read more than once, which runs its query each time.</summary>
        static void CteReuse(HintContext c, WithCtesAndXmlNamespaces with)
        {
            var statement = c.Tree.Parent(with);
            foreach (var cte in with.CommonTableExpressions)
            {
                int uses = c.Tree.Descendants(statement).OfType<NamedTableReference>()
                    .Count(t => t.SchemaObject.SchemaIdentifier == null && HintContext.NameIs(t.SchemaObject.BaseIdentifier, cte.ExpressionName.Value)
                        && !c.Tree.Ancestors(t).Contains(cte));
                if (uses > 1)
                    c.Suggestion("SH302", cte.ExpressionName, $"The CTE {cte.ExpressionName.Value} is read {uses} times, and its query runs each time. If it's expensive, put its rows in a temp table once.");
            }
        }

        /// <summary>SH303: <c>(SELECT COUNT(*) ...) &gt; 0</c>, which counts every row to find out whether there is one.</summary>
        static void CountAboveZero(HintContext c, BooleanComparisonExpression comparison)
        {
            var first = HintContext.Strip(comparison.FirstExpression);
            var second = HintContext.Strip(comparison.SecondExpression);
            var type = comparison.ComparisonType;
            bool countFirst = IsCountSubquery(first) && IsAnyRow(type, second);
            bool countSecond = IsCountSubquery(second) && IsAnyRow(Reverse(type), first);
            if (countFirst || countSecond)
                c.Suggestion("SH303", comparison, "Counting every row to see whether there are any does more work than needed. Use EXISTS (SELECT 1 FROM ...), which stops at the first row.");
        }

        static bool IsCountSubquery(ScalarExpression expression) =>
            expression is ScalarSubquery subquery && subquery.QueryExpression is QuerySpecification query
            && query.SelectElements.Count == 1 && query.SelectElements[0] is SelectScalarExpression element
            && HintContext.Strip(element.Expression) is FunctionCall call && call.CallTarget == null
            && (HintContext.NameIs(call.FunctionName, "COUNT") || HintContext.NameIs(call.FunctionName, "COUNT_BIG"))
            && query.GroupByClause == null;

        /// <summary>Whether <c>count &lt;type&gt; value</c> means "at least one row".</summary>
        static bool IsAnyRow(BooleanComparisonType type, ScalarExpression value)
        {
            if (!(value is IntegerLiteral literal)) return false;
            switch (type)
            {
                case BooleanComparisonType.GreaterThan:
                case BooleanComparisonType.NotEqualToBrackets:
                case BooleanComparisonType.NotEqualToExclamation:
                    return literal.Value == "0";
                case BooleanComparisonType.GreaterThanOrEqualTo:
                    return literal.Value == "1";
                default:
                    return false;
            }
        }

        static BooleanComparisonType Reverse(BooleanComparisonType type)
        {
            switch (type)
            {
                case BooleanComparisonType.LessThan: return BooleanComparisonType.GreaterThan;
                case BooleanComparisonType.LessThanOrEqualTo: return BooleanComparisonType.GreaterThanOrEqualTo;
                case BooleanComparisonType.GreaterThan: return BooleanComparisonType.LessThan;
                case BooleanComparisonType.GreaterThanOrEqualTo: return BooleanComparisonType.LessThanOrEqualTo;
                default: return type;
            }
        }

        /// <summary>SH306 and SH307: <c>TOP 100 PERCENT ... ORDER BY</c> where the order is ignored, and <c>TOP</c> without an order.</summary>
        static void Top(HintContext c, QuerySpecification query)
        {
            var top = query.TopRowFilter;
            if (top == null) return;
            bool hundredPercent = top.Percent && HintContext.Strip(top.Expression) is Literal literal && literal.Value == "100";
            if (hundredPercent)
            {
                if (query.OrderByClause != null && !IsOutermost(c, query))
                    c.Warning("SH306", top, "TOP 100 PERCENT ... ORDER BY in a view, subquery or CTE doesn't order anything: SQL Server ignores the ORDER BY. Order the outer query that reads it.");
                return;
            }
            if (query.OrderByClause != null || query.FromClause == null) return;
            if (c.Tree.Ancestors(query).TakeWhile(a => !(a is TSqlStatement)).Any(a => a is ExistsPredicate)) return;
            c.Warning("SH307", top, "TOP without ORDER BY returns whichever rows SQL Server finds first, which can change from one run to the next. Add an ORDER BY that says which rows you want.");
        }

        /// <summary>Whether the query is the statement's own result, not a subquery, derived table, CTE or view body.</summary>
        static bool IsOutermost(HintContext c, QuerySpecification query)
        {
            TSqlFragment current = query;
            var parent = c.Tree.Parent(current);
            while (parent is BinaryQueryExpression || parent is QueryParenthesisExpression)
            {
                current = parent;
                parent = c.Tree.Parent(current);
            }
            return parent is SelectStatement select && !(c.Tree.Parent(select) is ViewStatementBody) && !(c.Tree.Parent(select) is CursorDefinition);
        }

        /// <summary>SH308: <c>FROM a, b</c>.</summary>
        static void CommaJoin(HintContext c, QuerySpecification query)
        {
            var tables = query.FromClause?.TableReferences;
            if (tables == null || tables.Count < 2) return;
            var linked = new HashSet<string>(HintContext.ExposedNames(tables[0]), StringComparer.OrdinalIgnoreCase);
            var conditions = query.WhereClause?.SearchCondition == null
                ? new List<BooleanExpression>()
                : HintContext.Conjuncts(query.WhereClause.SearchCondition).ToList();
            bool crossJoin = false;
            foreach (var table in tables.Skip(1))
            {
                var names = new HashSet<string>(HintContext.ExposedNames(table), StringComparer.OrdinalIgnoreCase);
                bool joined = conditions.Any(condition =>
                {
                    var qualifiers = c.Columns(condition).Select(HintContext.Qualifier).Where(q => q != null).ToList();
                    return qualifiers.Any(names.Contains) && qualifiers.Any(linked.Contains);
                });
                if (!joined) crossJoin = true;
                linked.UnionWith(names);
            }
            if (crossJoin)
                c.Warning("SH308", query.FromClause, "FROM a, b with nothing in the WHERE linking every table is a cross join: every row of one with every row of the other. Write the joins out as INNER JOIN ... ON (or CROSS JOIN, if that's really meant).");
            else
                c.Suggestion("SH308", query.FromClause, "Old-style comma joins put the join conditions in the WHERE, where a missing one silently becomes a cross join. Write them as INNER JOIN ... ON.");
        }

        /// <summary>SH309: a <c>LEFT JOIN</c> whose right-hand table is filtered in the <c>WHERE</c>, making it an inner join.</summary>
        static void OuterJoinFilteredInWhere(HintContext c, QuerySpecification query)
        {
            if (query.FromClause == null || query.WhereClause?.SearchCondition == null) return;
            var optional = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var join in query.FromClause.TableReferences.SelectMany(t => c.Tree.Descendants(t, d => !HintContext.IsQueryBoundary(d)).Prepend(t)).OfType<QualifiedJoin>())
            {
                if (join.QualifiedJoinType == QualifiedJoinType.LeftOuter) optional.UnionWith(HintContext.ExposedNames(join.SecondTableReference));
                else if (join.QualifiedJoinType == QualifiedJoinType.RightOuter) optional.UnionWith(HintContext.ExposedNames(join.FirstTableReference));
            }
            if (optional.Count == 0) return;
            foreach (var condition in HintContext.Conjuncts(query.WhereClause.SearchCondition))
            {
                // IS NULL, ISNULL() and COALESCE() let the missing rows through.
                var parts = new[] { (TSqlFragment)condition }.Concat(c.OwnDescendants(condition)).ToList();
                if (parts.Any(p => p is BooleanIsNullExpression || p is CoalesceExpression
                    || (p is FunctionCall call && HintContext.NameIs(call.FunctionName, "ISNULL")))) continue;
                var table = c.Columns(condition).Select(HintContext.Qualifier).FirstOrDefault(q => q != null && optional.Contains(q));
                if (table != null)
                    c.Warning("SH309", condition, $"This filters {table}, the optional side of an outer join, in the WHERE, so rows without a match are thrown away and the outer join acts as an inner join. Move the condition into the join's ON, or make it an INNER JOIN.");
            }
        }

        /// <summary>SH305: <c>UNION</c> whose branches can't return the same row, so the duplicate check is wasted.</summary>
        static void UnionCouldBeAll(HintContext c, BinaryQueryExpression union)
        {
            if (union.BinaryQueryExpressionType != BinaryQueryExpressionType.Union || union.All) return;
            if (c.Tree.Parent(union) is BinaryQueryExpression outer && outer.BinaryQueryExpressionType == BinaryQueryExpressionType.Union && !outer.All) return;
            var branches = Branches(union).ToList();
            if (branches.Any(b => b == null)) return;
            int columns = branches[0].SelectElements.Count;
            if (branches.Any(b => b.SelectElements.Count != columns)) return;
            for (int i = 0; i < columns; i++)
            {
                var values = branches.Select(b => b.SelectElements[i] is SelectScalarExpression e && HintContext.Strip(e.Expression) is Literal l && !(l is NullLiteral) ? l.Value : null).ToList();
                if (values.All(v => v != null) && values.Distinct(StringComparer.Ordinal).Count() == values.Count)
                {
                    c.Suggestion("SH305", union, "UNION removes duplicates, but each branch here returns a different constant, so no two rows can be the same. UNION ALL skips the sort or hash that removing them costs.");
                    return;
                }
            }
        }

        static IEnumerable<QuerySpecification> Branches(QueryExpression query)
        {
            while (query is QueryParenthesisExpression parenthesis) query = parenthesis.QueryExpression;
            if (query is BinaryQueryExpression binary && binary.BinaryQueryExpressionType == BinaryQueryExpressionType.Union && !binary.All)
                return Branches(binary.FirstQueryExpression).Concat(Branches(binary.SecondQueryExpression));
            return new[] { query as QuerySpecification };
        }

        /// <summary>SH310: a subquery in a join's <c>ON</c>.</summary>
        static void SubqueryInOn(HintContext c, QualifiedJoin join)
        {
            if (join.SearchCondition == null) return;
            foreach (var subquery in c.Tree.Descendants(join.SearchCondition, d => !(d is ScalarSubquery)).OfType<ScalarSubquery>())
                c.Suggestion("SH310", subquery, "A subquery in a join's ON condition can run once per pair of rows. Join to it as a derived table, or move it to the WHERE.");
        }
    }
}