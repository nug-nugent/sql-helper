using System.Collections.Generic;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Layout;

namespace SqlHelper.Formatting.Output
{
    /// <summary>
    /// Writes the layout plan out as text: every code token in its original order, with the planned breaks, indents and
    /// casing, and every comment re-attached to the code next to it.
    /// </summary>
    /// <remarks>
    /// A comment with no line break between it and the code before it is <em>trailing</em> and stays at the end of that
    /// code's line. Any other comment is <em>leading</em> and goes on its own line above the code after it. A trailing
    /// comment after a comma that starts a new line (leading-comma lists) moves back in front of the comma, so it stays
    /// with the item it was written next to.
    /// </remarks>
    internal sealed class Emitter
    {
        readonly TokenLayout _layout;
        readonly FormattingOptions _options;
        readonly StringBuilder _out = new StringBuilder();
        readonly HashSet<int> _emittedComments = new HashSet<int>();
        int _lineLevel;
        bool _atStart = true;
        bool _lineMustEnd;

        Emitter(TokenLayout layout, FormattingOptions options)
        {
            _layout = layout;
            _options = options;
        }

        public static string Emit(TokenLayout layout, FormattingOptions options) => new Emitter(layout, options).Run();

        IList<TSqlParserToken> Tokens => _layout.Tokens;

        string Run()
        {
            var verbatim = new Dictionary<int, int>();
            foreach (var (first, last) in _layout.VerbatimRanges) verbatim[first] = last;

            int previous = -1;
            for (int i = 0; i < Tokens.Count; i++)
            {
                if (!_layout.IsCode(i)) continue;

                WriteTrailingComments(previous, i);
                if (_layout.Is(i, TSqlTokenType.Comma) && _layout.Breaks[i] != Break.Default)
                    WriteTrailingComments(i, _layout.NextCode(i) < 0 ? Tokens.Count : _layout.NextCode(i));

                var leading = LeadingComments(previous, i);
                WriteSeparator(previous, i, leading);
                WriteToken(i);
                previous = i;

                if (verbatim.TryGetValue(i, out int verbatimLast))
                {
                    for (int j = i + 1; j <= verbatimLast; j++)
                    {
                        if (_layout.IsComment(j)) _emittedComments.Add(j);
                        if (_layout.IsCode(j)) WriteToken(j);
                        else _out.Append(Tokens[j].Text);
                    }
                    previous = verbatimLast;
                    i = verbatimLast;
                }
            }

            WriteTrailingComments(previous, Tokens.Count);
            foreach (int comment in LeadingComments(previous, Tokens.Count))
            {
                NewLine(0);
                WriteComment(comment);
            }

            return _out.ToString();
        }

        void WriteSeparator(int previous, int index, List<int> leadingComments)
        {
            var kind = _layout.Breaks[index];
            int level = _layout.Levels[index];

            if (kind == Break.Default && leadingComments.Count > 0 && !_lineMustEnd && OnSameLineAs(leadingComments, index))
            {
                // /* note */ written just before mid-line code stays just before it.
                foreach (int comment in leadingComments)
                {
                    if (!_atStart) _out.Append(' ');
                    WriteComment(comment);
                }
                if (!_layout.NoSpaceBefore[index]) _out.Append(' ');
                return;
            }

            if (kind == Break.Default && (leadingComments.Count > 0 || _lineMustEnd))
            {
                // Code that has to move to a new line although layout didn't ask for one: continue one level in.
                kind = Break.NewLine;
                if (!_atStart) level = _lineLevel + 1;
            }

            if (kind == Break.Default)
            {
                if (!_atStart && !_layout.NoSpaceBefore[index] && (previous < 0 || !_layout.NoSpaceAfter[previous]))
                    _out.Append(' ');
                return;
            }

            if (!_atStart)
            {
                if (kind == Break.BlankLine) _out.Append(_options.NewLine);
                NewLine(level);
            }
            else
            {
                Indent(level);
            }

            foreach (int comment in leadingComments)
            {
                WriteComment(comment);
                NewLine(level);
            }
        }

        void WriteToken(int index)
        {
            var text = Tokens[index].Text;
            _out.Append(_layout.Uppercase[index] ? text.ToUpperInvariant() : text);
            _atStart = false;
            _lineMustEnd = false;
        }

        /// <summary>Comments between <paramref name="after"/> and <paramref name="before"/> that sit on <paramref name="after"/>'s line.</summary>
        void WriteTrailingComments(int after, int before)
        {
            if (after < 0) return;
            for (int j = after + 1; j < before && j < Tokens.Count; j++)
            {
                if (Tokens[j].TokenType == TSqlTokenType.WhiteSpace && ContainsLineBreak(j)) return;
                if (!_layout.IsComment(j) || _emittedComments.Contains(j)) continue;
                _out.Append(' ');
                WriteComment(j);
            }
        }

        List<int> LeadingComments(int after, int before)
        {
            var comments = new List<int>();
            bool pastLineBreak = after < 0;
            for (int j = after + 1; j < before && j < Tokens.Count; j++)
            {
                if (Tokens[j].TokenType == TSqlTokenType.WhiteSpace && ContainsLineBreak(j)) pastLineBreak = true;
                if (_layout.IsComment(j) && !_emittedComments.Contains(j) && pastLineBreak) comments.Add(j);
            }
            return comments;
        }

        void WriteComment(int index)
        {
            _emittedComments.Add(index);
            var token = Tokens[index];
            if (token.TokenType == TSqlTokenType.SingleLineComment)
            {
                _out.Append(token.Text.TrimEnd('\r', '\n'));
                _lineMustEnd = true;
            }
            else
            {
                _out.Append(token.Text);
            }
            _atStart = false;
        }

        void NewLine(int level)
        {
            _out.Append(_options.NewLine);
            Indent(level);
            _lineMustEnd = false;
        }

        void Indent(int level)
        {
            for (int i = 0; i < level; i++) _out.Append(_options.IndentString);
            _lineLevel = level;
        }

        /// <summary>True if the comments are all block comments with no line break between them and the code at <paramref name="index"/>.</summary>
        bool OnSameLineAs(List<int> comments, int index)
        {
            foreach (int comment in comments)
                if (Tokens[comment].TokenType != TSqlTokenType.MultilineComment) return false;
            for (int j = comments[0]; j < index; j++)
                if (Tokens[j].TokenType == TSqlTokenType.WhiteSpace && ContainsLineBreak(j)) return false;
            return true;
        }

        bool ContainsLineBreak(int index) => Tokens[index].Text.IndexOf('\n') >= 0 || Tokens[index].Text.IndexOf('\r') >= 0;
    }
}
