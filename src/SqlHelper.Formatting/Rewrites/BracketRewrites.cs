using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Rewrites
{
    /// <summary>
    /// The Clean Up rule for brackets: every table, column, schema, procedure and alias name written as
    /// <see cref="Names.Write"/> says. Only names in those positions are collected, so data types, function names, date
    /// parts and option words, which ScriptDom also calls identifiers, are never touched.
    /// </summary>
    internal sealed class BracketRewrites : TSqlFragmentVisitor
    {
        static readonly HashSet<string> TriggerTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "inserted", "deleted" };

        readonly List<Identifier> _names = new List<Identifier>();
        readonly List<(int First, int Last)> _dataTypes = new List<(int, int)>();

        public static List<TokenEdit> Find(TSqlScript script)
        {
            var rewrites = new BracketRewrites();
            script.Accept(rewrites);
            var tokens = script.ScriptTokenStream;

            var edits = new List<TokenEdit>();
            var seen = new HashSet<int>();
            foreach (var name in rewrites._names)
            {
                int index = name.FirstTokenIndex;
                if (index < 0 || index != name.LastTokenIndex || !seen.Add(index)) continue;
                if (rewrites._dataTypes.Any(range => range.First <= index && index <= range.Last)) continue;
                string value = name.Value;
                if (string.IsNullOrEmpty(value) || value[0] == '#' || value[0] == '@' || TriggerTables.Contains(value)) continue;

                string written = Names.Write(value);
                if (written != tokens[index].Text) edits.Add(TokenEdit.Replace(index, index, written));
            }
            return edits;
        }

        public override void Visit(DataTypeReference node) => _dataTypes.Add((node.FirstTokenIndex, node.LastTokenIndex));

        public override void Visit(SchemaObjectName node) => _names.AddRange(node.Identifiers);

        public override void Visit(ColumnReferenceExpression node)
        {
            if (node.ColumnType == ColumnType.Regular && node.MultiPartIdentifier != null)
                _names.AddRange(node.MultiPartIdentifier.Identifiers);
        }

        public override void Visit(TableReferenceWithAlias node) => Add(node.Alias);

        public override void Visit(TableReferenceWithAliasAndColumns node) => _names.AddRange(node.Columns);

        public override void Visit(SelectScalarExpression node) => Add(node.ColumnName?.Identifier);

        public override void Visit(ColumnDefinitionBase node) => Add(node.ColumnIdentifier);

        public override void Visit(CommonTableExpression node)
        {
            Add(node.ExpressionName);
            _names.AddRange(node.Columns);
        }

        void Add(Identifier name)
        {
            if (name != null) _names.Add(name);
        }
    }
}