using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>Where a boolean expression filters rows.</summary>
    internal enum FilterKind
    {
        None,
        Where,
        On,
    }

    /// <summary>What the rules share: the tree, the options, the hints found so far, and questions about the tree.</summary>
    internal sealed class HintContext
    {
        readonly List<Hint> _hints = new List<Hint>();
        readonly HashSet<(string Code, int Offset)> _seen = new HashSet<(string Code, int Offset)>();

        public HintContext(TSqlScript script, HintOptions options)
        {
            Tree = new SyntaxIndex(script);
            Options = options;
        }

        public SyntaxIndex Tree { get; }

        public HintOptions Options { get; }

        public IReadOnlyList<Hint> Hints =>
            _hints.OrderBy(h => h.Offset).ThenBy(h => h.Code, StringComparer.Ordinal).ToList();

        public void Add(string code, HintSeverity severity, TSqlFragment at, string message)
        {
            if (!_seen.Add((code, at.StartOffset))) return;
            _hints.Add(new Hint(code, severity, at.StartLine, at.StartColumn, at.StartOffset, at.FragmentLength, message));
        }

        public void AddAtToken(string code, HintSeverity severity, TSqlParserToken at, string message)
        {
            if (!_seen.Add((code, at.Offset))) return;
            _hints.Add(new Hint(code, severity, at.Line, at.Column, at.Offset, at.Text.Length, message));
        }

        public void Warning(string code, TSqlFragment at, string message) => Add(code, HintSeverity.Warning, at, message);

        public void Suggestion(string code, TSqlFragment at, string message) => Add(code, HintSeverity.Suggestion, at, message);

        /// <summary>The fragment's source text, on one line.</summary>
        public static string Text(TSqlFragment fragment)
        {
            if (fragment.FirstTokenIndex < 0) return "";
            var text = new System.Text.StringBuilder();
            for (int i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex; i++)
            {
                var token = fragment.ScriptTokenStream[i];
                text.Append(token.TokenType == TSqlTokenType.WhiteSpace ? " " : token.Text);
            }
            return text.ToString();
        }

        /// <summary>A subquery, derived table or CTE body: the start of a query of its own.</summary>
        public static bool IsQueryBoundary(TSqlFragment fragment) =>
            fragment is ScalarSubquery || fragment is QueryDerivedTable || fragment is CommonTableExpression;

        /// <summary>The descendants of <paramref name="fragment"/> that belong to its own query, not to a subquery in it.</summary>
        public IEnumerable<TSqlFragment> OwnDescendants(TSqlFragment fragment) =>
            Tree.Descendants(fragment, d => !IsQueryBoundary(d)).Where(d => !IsQueryBoundary(d));

        /// <summary>The regular column references in an expression, outside any subquery in it.</summary>
        public IEnumerable<ColumnReferenceExpression> Columns(TSqlFragment fragment)
        {
            if (IsQueryBoundary(fragment)) yield break;
            if (fragment is ColumnReferenceExpression self && self.ColumnType == ColumnType.Regular) yield return self;
            foreach (var column in OwnDescendants(fragment).OfType<ColumnReferenceExpression>())
                if (column.ColumnType == ColumnType.Regular) yield return column;
        }

        public static ScalarExpression Strip(ScalarExpression expression)
        {
            while (expression is ParenthesisExpression parenthesis) expression = parenthesis.Expression;
            return expression;
        }

        public static BooleanExpression Strip(BooleanExpression expression)
        {
            while (expression is BooleanParenthesisExpression parenthesis) expression = parenthesis.Expression;
            return expression;
        }

        /// <summary>The <c>AND</c>ed parts of a condition, through brackets.</summary>
        public static IEnumerable<BooleanExpression> Conjuncts(BooleanExpression condition) => Flatten(condition, BooleanBinaryExpressionType.And);

        /// <summary>The <c>OR</c>ed parts of a condition, through brackets.</summary>
        public static IEnumerable<BooleanExpression> Disjuncts(BooleanExpression condition) => Flatten(condition, BooleanBinaryExpressionType.Or);

        static IEnumerable<BooleanExpression> Flatten(BooleanExpression condition, BooleanBinaryExpressionType type)
        {
            condition = Strip(condition);
            if (condition is BooleanBinaryExpression binary && binary.BinaryExpressionType == type)
            {
                foreach (var part in Flatten(binary.FirstExpression, type)) yield return part;
                foreach (var part in Flatten(binary.SecondExpression, type)) yield return part;
            }
            else yield return condition;
        }

        /// <summary>
        /// Whether a boolean expression is part of a <c>WHERE</c> or join <c>ON</c> condition (through <c>AND</c>,
        /// <c>OR</c>, <c>NOT</c> and brackets, but not through a <c>CASE</c> or a subquery).
        /// </summary>
        public FilterKind Filter(BooleanExpression expression)
        {
            TSqlFragment current = expression;
            var parent = Tree.Parent(current);
            while (parent is BooleanBinaryExpression || parent is BooleanParenthesisExpression || parent is BooleanNotExpression)
            {
                current = parent;
                parent = Tree.Parent(current);
            }
            if (parent is WhereClause where && where.SearchCondition == current) return FilterKind.Where;
            if (parent is QualifiedJoin join && join.SearchCondition == current) return FilterKind.On;
            return FilterKind.None;
        }

        /// <summary>
        /// The names a table reference makes available to the query: aliases, or the table's own name when it has none,
        /// through joins.
        /// </summary>
        public static IEnumerable<string> ExposedNames(TableReference reference)
        {
            switch (reference)
            {
                case JoinTableReference join:
                    foreach (var name in ExposedNames(join.FirstTableReference)) yield return name;
                    foreach (var name in ExposedNames(join.SecondTableReference)) yield return name;
                    break;
                case JoinParenthesisTableReference parenthesis:
                    foreach (var name in ExposedNames(parenthesis.Join)) yield return name;
                    break;
                case NamedTableReference named:
                    yield return (named.Alias ?? named.SchemaObject.BaseIdentifier).Value;
                    break;
                case VariableTableReference variable:
                    yield return variable.Alias?.Value ?? variable.Variable.Name;
                    break;
                case TableReferenceWithAlias aliased when aliased.Alias != null:
                    yield return aliased.Alias.Value;
                    break;
            }
        }

        /// <summary>The table name or alias a column reference is qualified with, or null.</summary>
        public static string Qualifier(ColumnReferenceExpression column)
        {
            var parts = column.MultiPartIdentifier?.Identifiers;
            return parts != null && parts.Count >= 2 ? parts[parts.Count - 2].Value : null;
        }

        /// <summary><c>alias.column</c> (or just the column), lowercased, to compare references.</summary>
        public static string ColumnKey(ColumnReferenceExpression column) =>
            column.MultiPartIdentifier == null ? "" : string.Join(".", column.MultiPartIdentifier.Identifiers.Select(i => i.Value.ToLowerInvariant()));

        static readonly HashSet<string> Aggregates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AVG", "CHECKSUM_AGG", "COUNT", "COUNT_BIG", "GROUPING", "GROUPING_ID", "MAX", "MIN", "STDEV", "STDEVP",
            "STRING_AGG", "SUM", "VAR", "VARP", "APPROX_COUNT_DISTINCT",
        };

        /// <summary>Whether the fragment holds an aggregate function call of its own query (not one in a subquery, nor a window function).</summary>
        public bool HasAggregate(TSqlFragment fragment) =>
            new[] { fragment }.Concat(OwnDescendants(fragment)).OfType<FunctionCall>()
                .Any(f => f.CallTarget == null && f.OverClause == null && Aggregates.Contains(f.FunctionName.Value));

        public static bool NameIs(Identifier identifier, string name) =>
            identifier != null && string.Equals(identifier.Value, name, StringComparison.OrdinalIgnoreCase);

        public static bool IsTempOrVariable(SchemaObjectName name) =>
            name?.BaseIdentifier != null && (name.BaseIdentifier.Value.StartsWith("#") || name.BaseIdentifier.Value.StartsWith("@"));

        /// <summary>The procedure, function, trigger or view the fragment is in, or null in a plain script.</summary>
        public TSqlFragment Module(TSqlFragment fragment) =>
            Tree.Ancestors(fragment).FirstOrDefault(a => a is ProcedureStatementBody || a is FunctionStatementBody
                || a is TriggerStatementBody || a is ViewStatementBody);

        /// <summary>The innermost statement the fragment is in that isn't a block or a module (a <c>SELECT</c>, not the <c>IF</c> around it).</summary>
        public TSqlStatement Statement(TSqlFragment fragment) =>
            Tree.Ancestors(fragment).OfType<TSqlStatement>().FirstOrDefault(s => !(s is IfStatement || s is WhileStatement
                || s is BeginEndBlockStatement || s is TryCatchStatement || s is ProcedureStatementBody || s is FunctionStatementBody
                || s is TriggerStatementBody || s is ViewStatementBody));
    }
}