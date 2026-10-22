using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Rewrites
{
    /// <summary>
    /// Replaces the tokens <see cref="First"/> to <see cref="Last"/> with <see cref="Text"/>, which must parse to
    /// <see cref="Tokens"/>. When <see cref="Last"/> is <see cref="First"/> - 1 nothing is replaced and the text is
    /// inserted before token <see cref="First"/>.
    /// </summary>
    internal sealed class TokenEdit
    {
        public TokenEdit(int first, int last, string text, IEnumerable<TSqlParserToken> tokens)
        {
            First = first;
            Last = last;
            Text = text;
            Tokens = tokens.ToList();
        }

        public int First { get; }
        public int Last { get; }
        public string Text { get; }
        public IReadOnlyList<TSqlParserToken> Tokens { get; }

        bool IsInsertion => Last < First;

        /// <summary>A replacement of tokens with new code text (keywords, names); its tokens come from parsing it.</summary>
        public static TokenEdit Replace(int first, int last, string text) => new TokenEdit(first, last, text, Tokenize(text));

        public static TokenEdit Insert(int before, string text) => new TokenEdit(before, before - 1, text, Tokenize(text));

        /// <summary>Whether the two edits touch the same tokens, so applying both at once is ambiguous.</summary>
        public bool Overlaps(TokenEdit other)
        {
            if (IsInsertion && other.IsInsertion) return First == other.First;
            if (IsInsertion) return other.First <= First && First <= other.Last;
            if (other.IsInsertion) return First <= other.First && other.First <= Last;
            return First <= other.Last && other.First <= Last;
        }

        /// <summary>The text with the edits made, and the tokens it must parse to.</summary>
        public static (string Text, List<TSqlParserToken> Expected) Apply(string sql, IList<TSqlParserToken> tokens, IEnumerable<TokenEdit> edits)
        {
            var output = new StringBuilder(sql.Length);
            var expected = new List<TSqlParserToken>(tokens.Count);
            int position = 0;
            int next = 0;
            foreach (var edit in edits.OrderBy(e => e.First))
            {
                int start = tokens[edit.First].Offset;
                int end = edit.IsInsertion ? start : tokens[edit.Last].Offset + tokens[edit.Last].Text.Length;
                output.Append(sql, position, start - position).Append(edit.Text);
                position = end;

                for (; next < edit.First; next++) expected.Add(tokens[next]);
                expected.AddRange(edit.Tokens);
                next = edit.Last + 1;
            }
            output.Append(sql, position, sql.Length - position);
            for (; next < tokens.Count; next++) expected.Add(tokens[next]);
            return (output.ToString(), expected);
        }

        static List<TSqlParserToken> Tokenize(string text)
        {
            using (var reader = new StringReader(text))
            {
                return new TSql170Parser(initialQuotedIdentifiers: true).GetTokenStream(reader, out _)
                    .Where(t => t.TokenType != TSqlTokenType.EndOfFile).ToList();
            }
        }
    }
}