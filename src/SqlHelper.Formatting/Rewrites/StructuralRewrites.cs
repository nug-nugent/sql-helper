using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Rewrites
{
    /// <summary>
    /// The Clean Up rules that rewrite keywords and aliases: joins, <c>PROC</c> / <c>TRAN</c> / <c>EXECUTE</c>,
    /// <c>INSERT INTO</c> / <c>DELETE FROM</c>, <c>!=</c>, and <c>AS</c> on aliases. Brackets are
    /// <see cref="BracketRewrites"/>.
    /// </summary>
    internal sealed class StructuralRewrites : TSqlFragmentVisitor
    {
        readonly IList<TSqlParserToken> _tokens;
        readonly string _sql;
        readonly List<TokenEdit> _edits = new List<TokenEdit>();

        StructuralRewrites(string sql, IList<TSqlParserToken> tokens)
        {
            _sql = sql;
            _tokens = tokens;
        }

        public static List<TokenEdit> Find(string sql, TSqlScript script)
        {
            var rewrites = new StructuralRewrites(sql, script.ScriptTokenStream);
            rewrites.FindInTokens();
            script.Accept(rewrites);
            return rewrites._edits;
        }

        /// <summary><c>PROC</c>, <c>TRAN</c> and <c>!=</c>, which mean the same wherever they are.</summary>
        void FindInTokens()
        {
            for (int i = 0; i < _tokens.Count; i++)
            {
                switch (_tokens[i].TokenType)
                {
                    case TSqlTokenType.Proc:
                        _edits.Add(TokenEdit.Replace(i, i, "PROCEDURE"));
                        break;
                    case TSqlTokenType.Tran:
                        _edits.Add(TokenEdit.Replace(i, i, "TRANSACTION"));
                        break;
                    case TSqlTokenType.Bang:
                        if (i + 1 < _tokens.Count && _tokens[i + 1].TokenType == TSqlTokenType.EqualsSign)
                            _edits.Add(TokenEdit.Replace(i, i + 1, "<>"));
                        break;
                }
            }
        }

        public override void Visit(QualifiedJoin node)
        {
            var keywords = Enumerable.Range(node.FirstTableReference.LastTokenIndex + 1,
                    node.SecondTableReference.FirstTokenIndex - node.FirstTableReference.LastTokenIndex - 1)
                .Where(i => TokenScan.IsCode(_tokens[i])).ToList();
            if (keywords.Count == 0) return;

            if (node.QualifiedJoinType == QualifiedJoinType.Inner)
            {
                if (!keywords.Any(i => _tokens[i].TokenType == TSqlTokenType.Inner))
                    _edits.Add(TokenEdit.Insert(keywords[0], "INNER "));
                return;
            }

            int outer = keywords.FindIndex(i => _tokens[i].TokenType == TSqlTokenType.Outer);
            if (outer < 0) return;
            outer = keywords[outer];
            int last = outer;
            while (last + 1 < _tokens.Count && _tokens[last + 1].TokenType == TSqlTokenType.WhiteSpace) last++;
            _edits.Add(new TokenEdit(outer, last, "", new TSqlParserToken[0]));
        }

        public override void Visit(ExecuteStatement node) => ExecInsteadOfExecute(node.FirstTokenIndex);

        public override void Visit(ExecuteInsertSource node) => ExecInsteadOfExecute(node.FirstTokenIndex);

        void ExecInsteadOfExecute(int index)
        {
            if (index >= 0 && string.Equals(_tokens[index].Text, "EXECUTE", StringComparison.OrdinalIgnoreCase))
                _edits.Add(TokenEdit.Replace(index, index, "EXEC"));
        }

        public override void Visit(InsertSpecification node) => KeywordBefore(node.Target, TSqlTokenType.Into, "INTO ");

        public override void Visit(DeleteSpecification node)
        {
            if (node.FromClause == null) KeywordBefore(node.Target, TSqlTokenType.From, "FROM ");
        }

        void KeywordBefore(TSqlFragment target, TSqlTokenType type, string keyword)
        {
            if (target == null || target.FirstTokenIndex < 0) return;
            int previous = TokenScan.PreviousCode(_tokens, target.FirstTokenIndex);
            if (previous >= 0 && _tokens[previous].TokenType != type)
                _edits.Add(TokenEdit.Insert(target.FirstTokenIndex, keyword));
        }

        public override void Visit(SelectScalarExpression node)
        {
            var name = node.ColumnName;
            if (name == null || name.FirstTokenIndex < 0 || name.FirstTokenIndex != name.LastTokenIndex) return;
            int alias = name.FirstTokenIndex;
            string aliasText = name.ValueExpression is StringLiteral literal ? Names.Write(literal.Value) : _tokens[alias].Text;
            var expression = node.Expression;

            if (alias < expression.FirstTokenIndex)
            {
                // Alias = expression: turned round, if only "=" and spaces separate them (a comment there would move).
                bool plain = Enumerable.Range(alias + 1, expression.FirstTokenIndex - alias - 1)
                    .All(i => _tokens[i].TokenType == TSqlTokenType.WhiteSpace || _tokens[i].TokenType == TSqlTokenType.EqualsSign);
                if (!plain) return;
                int start = _tokens[expression.FirstTokenIndex].Offset;
                int end = _tokens[expression.LastTokenIndex].Offset + _tokens[expression.LastTokenIndex].Text.Length;
                var moved = TokenEdit.Replace(alias, expression.LastTokenIndex, " AS " + aliasText);
                _edits.Add(new TokenEdit(alias, expression.LastTokenIndex, _sql.Substring(start, end - start) + " AS " + aliasText,
                    _tokens.Skip(expression.FirstTokenIndex).Take(expression.LastTokenIndex - expression.FirstTokenIndex + 1).Concat(moved.Tokens)));
                return;
            }

            bool hasAs = _tokens[TokenScan.PreviousCode(_tokens, alias)].TokenType == TSqlTokenType.As;
            if (name.ValueExpression is StringLiteral)
                _edits.Add(TokenEdit.Replace(alias, alias, (hasAs ? "" : "AS ") + aliasText));
            else if (!hasAs)
                _edits.Add(TokenEdit.Insert(alias, "AS "));
        }

        public override void Visit(TableReferenceWithAlias node)
        {
            var alias = node.Alias;
            if (alias == null || alias.FirstTokenIndex < 0) return;
            int previous = TokenScan.PreviousCode(_tokens, alias.FirstTokenIndex);
            if (previous >= 0 && _tokens[previous].TokenType != TSqlTokenType.As)
                _edits.Add(TokenEdit.Insert(alias.FirstTokenIndex, "AS "));
        }
    }
}