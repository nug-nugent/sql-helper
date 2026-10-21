using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Layout
{
    /// <summary>
    /// Token-level rules that don't depend on line layout: which tokens are uppercased, and where spaces are dropped.
    /// </summary>
    internal static class TokenRules
    {
        public static void Apply(TSqlScript script, TokenLayout layout)
        {
            var visitor = new Visitor(layout);
            script.Accept(visitor);

            for (int i = 0; i < layout.Tokens.Count; i++)
            {
                if (!layout.IsCode(i)) continue;
                var type = layout.Tokens[i].TokenType;

                layout.Uppercase[i] = visitor.ForcedUppercase.Contains(i)
                    || (type == TSqlTokenType.Identifier ? !visitor.NameTokens.Contains(i) : !KeepsItsCase(type));

                // ScriptDom splits <>, >=, <=, != and friends into single characters; keep written-together ones together.
                if (i > 0 && layout.IsCode(i - 1) && IsComparisonPart(layout, i) && IsComparisonPart(layout, i - 1))
                    layout.NoSpaceBefore[i] = true;

                switch (type)
                {
                    case TSqlTokenType.Comma:
                    case TSqlTokenType.Semicolon:
                    case TSqlTokenType.RightParenthesis:
                        layout.NoSpaceBefore[i] = true;
                        break;
                    case TSqlTokenType.Dot:
                        layout.NoSpaceBefore[i] = true;
                        layout.NoSpaceAfter[i] = true;
                        break;
                    case TSqlTokenType.LeftParenthesis:
                        layout.NoSpaceAfter[i] = true;
                        break;
                }
            }
        }

        static bool IsComparisonPart(TokenLayout layout, int index)
        {
            string text = layout.Tokens[index].Text;
            return text.Length > 0 && text.Trim('<', '>', '=', '!').Length == 0;
        }

        // Literals, names and variables are written exactly as typed; everything else that isn't a plain identifier
        // is a keyword or punctuation.
        static bool KeepsItsCase(TSqlTokenType type)
        {
            switch (type)
            {
                case TSqlTokenType.QuotedIdentifier:
                case TSqlTokenType.Variable:
                case TSqlTokenType.AsciiStringLiteral:
                case TSqlTokenType.UnicodeStringLiteral:
                case TSqlTokenType.AsciiStringOrQuotedIdentifier:
                case TSqlTokenType.Integer:
                case TSqlTokenType.Numeric:
                case TSqlTokenType.Real:
                case TSqlTokenType.Money:
                case TSqlTokenType.HexLiteral:
                case TSqlTokenType.Label:
                    return true;
                default:
                    return false;
            }
        }

        sealed class Visitor : TSqlFragmentVisitor
        {
            readonly TokenLayout _layout;

            public Visitor(TokenLayout layout)
            {
                _layout = layout;
            }

            /// <summary>Identifier tokens that name something (tables, columns, aliases…); all others are keywords.</summary>
            public HashSet<int> NameTokens { get; } = new HashSet<int>();

            /// <summary>Name tokens that are uppercased anyway: data types, built-in functions, system variables.</summary>
            public HashSet<int> ForcedUppercase { get; } = new HashSet<int>();

            public override void Visit(Identifier node) => AddRange(NameTokens, node);

            public override void Visit(SqlDataTypeReference node) => AddRange(ForcedUppercase, node.Name);

            public override void Visit(GlobalVariableExpression node) => AddRange(ForcedUppercase, node);

            public override void Visit(FunctionCall node)
            {
                // Scalar user-defined functions must be schema-qualified, so an unqualified call is always built in.
                if (node.CallTarget == null) AddRange(ForcedUppercase, node.FunctionName);
            }

            public override void Visit(PivotedTableReference node)
            {
                // PIVOT (SUM(Amt) FOR …): the aggregate is a plain identifier to ScriptDom, but reads as a function call.
                var aggregate = node.AggregateFunctionIdentifier;
                AddRange(ForcedUppercase, aggregate);
                int bracket = _layout.NextCode(aggregate.LastTokenIndex);
                if (_layout.Is(bracket, TSqlTokenType.LeftParenthesis)) _layout.NoSpaceBefore[bracket] = true;
            }

            public override void Visit(UnaryExpression node)
            {
                if (node.FirstTokenIndex >= 0) _layout.NoSpaceAfter[node.FirstTokenIndex] = true;
            }

            public override void Visit(TSqlFragment node)
            {
                // Function-call-like brackets hug the name before them: COUNT(*), VARCHAR(50), IDENTITY(1, 1).
                if (node.FirstTokenIndex < 0 || !HugsBracket(node)) return;
                int bracket = _layout.FindTopLevel(node.FirstTokenIndex, node.LastTokenIndex, i => _layout.Is(i, TSqlTokenType.LeftParenthesis));
                if (bracket > node.FirstTokenIndex) _layout.NoSpaceBefore[bracket] = true;
            }

            static bool HugsBracket(TSqlFragment node)
            {
                string name = node.GetType().Name;
                return name.EndsWith("Call")
                    || node is CoalesceExpression
                    || node is NullIfExpression
                    || node is SqlDataTypeReference
                    || node is IdentityOptions
                    || node is RaiseErrorStatement
                    || node is XmlForClauseOption;
            }

            static void AddRange(HashSet<int> set, TSqlFragment fragment)
            {
                if (fragment == null || fragment.FirstTokenIndex < 0) return;
                for (int i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex; i++) set.Add(i);
            }
        }
    }
}
