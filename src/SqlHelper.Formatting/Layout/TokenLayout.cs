using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Layout
{
    /// <summary>What goes in front of a token in the output.</summary>
    internal enum Break
    {
        /// <summary>A single space, unless punctuation rules say none.</summary>
        Default,
        NewLine,
        BlankLine,
    }

    /// <summary>
    /// The layout plan: one decision per token of the original script. Layout code only ever writes here; the
    /// <see cref="Output.Emitter"/> turns the plan into text. Because every token is emitted from this list, in order,
    /// layout can only change whitespace, never the code itself.
    /// </summary>
    internal sealed class TokenLayout
    {
        public TokenLayout(IList<TSqlParserToken> tokens)
        {
            Tokens = tokens;
            Breaks = new Break[tokens.Count];
            Levels = new int[tokens.Count];
            NoSpaceBefore = new bool[tokens.Count];
            NoSpaceAfter = new bool[tokens.Count];
            Uppercase = new bool[tokens.Count];
        }

        public IList<TSqlParserToken> Tokens { get; }
        public Break[] Breaks { get; }
        /// <summary>Indent level of the line a token starts, when its break is a new line.</summary>
        public int[] Levels { get; }
        public bool[] NoSpaceBefore { get; }
        public bool[] NoSpaceAfter { get; }
        public bool[] Uppercase { get; }

        /// <summary>Token ranges copied with their original whitespace, for statements layout doesn't handle yet.</summary>
        public List<(int First, int Last)> VerbatimRanges { get; } = new List<(int First, int Last)>();

        public void NewLine(int tokenIndex, int level) => SetBreak(tokenIndex, Break.NewLine, level);

        public void BlankLine(int tokenIndex, int level) => SetBreak(tokenIndex, Break.BlankLine, level);

        public void SetBreak(int tokenIndex, Break kind, int level)
        {
            if (tokenIndex < 0) return;
            Breaks[tokenIndex] = kind;
            Levels[tokenIndex] = level;
        }

        public bool IsCode(int index)
        {
            var type = Tokens[index].TokenType;
            return type != TSqlTokenType.WhiteSpace && type != TSqlTokenType.EndOfFile && !IsComment(index);
        }

        public bool IsComment(int index)
        {
            var type = Tokens[index].TokenType;
            return type == TSqlTokenType.SingleLineComment || type == TSqlTokenType.MultilineComment;
        }

        /// <summary>The next code token after <paramref name="index"/>, or -1.</summary>
        public int NextCode(int index)
        {
            for (int i = index + 1; i < Tokens.Count; i++)
                if (IsCode(i)) return i;
            return -1;
        }

        /// <summary>The previous code token before <paramref name="index"/>, or -1.</summary>
        public int PreviousCode(int index)
        {
            for (int i = index - 1; i >= 0; i--)
                if (IsCode(i)) return i;
            return -1;
        }

        public bool Is(int index, TSqlTokenType type) => index >= 0 && Tokens[index].TokenType == type;

        public bool IsWord(int index, string word) =>
            index >= 0 && string.Equals(Tokens[index].Text, word, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The first code token in [<paramref name="from"/>, <paramref name="to"/>] that isn't inside brackets opened in
        /// that range and matches <paramref name="predicate"/>, or -1.
        /// </summary>
        public int FindTopLevel(int from, int to, System.Func<int, bool> predicate)
        {
            int depth = 0;
            for (int i = from; i <= to && i < Tokens.Count; i++)
            {
                if (!IsCode(i)) continue;
                if (depth == 0 && predicate(i)) return i;
                if (Is(i, TSqlTokenType.LeftParenthesis)) depth++;
                else if (Is(i, TSqlTokenType.RightParenthesis)) depth--;
            }
            return -1;
        }

        /// <summary>
        /// The first token of a search condition. ScriptDom starts an EXISTS predicate (and any condition beginning
        /// with one) at its bracket, leaving the EXISTS keyword outside; this steps back to include it.
        /// </summary>
        public int ConditionStart(TSqlFragment condition)
        {
            int first = condition.FirstTokenIndex;
            int previous = PreviousCode(first);
            return Is(first, TSqlTokenType.LeftParenthesis) && Is(previous, TSqlTokenType.Exists) ? previous : first;
        }

        /// <summary>The fragment's last token, stepping back over a statement-ending semicolon.</summary>
        public int LastBeforeSemicolon(TSqlFragment fragment)
        {
            int last = fragment.LastTokenIndex;
            return Is(last, TSqlTokenType.Semicolon) ? PreviousCode(last) : last;
        }

        /// <summary>True if layout put a line break anywhere inside the fragment after its first token.</summary>
        public bool SpansLines(TSqlFragment fragment)
        {
            for (int i = fragment.FirstTokenIndex + 1; i <= fragment.LastTokenIndex; i++)
            {
                if (Breaks[i] != Break.Default) return true;
                if (Tokens[i].TokenType == TSqlTokenType.SingleLineComment) return true;
                if (Tokens[i].TokenType != TSqlTokenType.WhiteSpace && Tokens[i].Text.IndexOf('\n') >= 0) return true;
            }
            foreach (var (first, last) in VerbatimRanges)
                if (first <= fragment.LastTokenIndex && last >= fragment.FirstTokenIndex) return true;
            return false;
        }
    }
}
