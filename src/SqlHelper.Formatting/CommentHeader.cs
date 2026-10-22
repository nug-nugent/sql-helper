using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Headers;
using SqlHelper.Formatting.Verification;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Adds or updates the house comment header on the first procedure, function or view in a script (see
    /// docs/style-guide.md, Comment headers). Only the header comment is touched; the code is checked to be unchanged.
    /// </summary>
    /// <remarks>
    /// The header is looked for among the comments directly before the object's <c>CREATE</c> / <c>ALTER</c>:
    /// <list type="bullet">
    /// <item>A house header gets a history line added at the end.</item>
    /// <item>An SSMS template header (<c>-- Author: / Create date: / Description:</c>) is rewritten as a house header,
    /// its author and date becoming the first history line, plus the added line.</item>
    /// <item>A dash history block (<c>-- AB - 01/06/2023 - what changed</c> lines between <c>-------</c> rules) is
    /// rewritten as a house header with those lines as its history, plus the added line.</item>
    /// <item>Otherwise a new header goes directly before the object, and any other comments stay where they are.</item>
    /// </list>
    /// </remarks>
    public sealed class CommentHeader
    {
        readonly HeaderOptions _options;

        public CommentHeader(HeaderOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Initials of a plain "First Last" name, e.g. DH for David Huggett; null for anything else.</summary>
        public static string InitialsOf(string name) => HeaderText.InitialsOf(name);

        public HeaderResult Apply(string sql)
        {
            sql = sql ?? "";
            string initials = _options.Initials?.Trim();
            if (string.IsNullOrEmpty(initials))
                return HeaderResult.Failure(sql, new[] { "Set your initials before adding a header." });

            TSqlScript script;
            using (var reader = new StringReader(sql))
            {
                script = (TSqlScript)CreateParser().Parse(reader, out IList<ParseError> errors);
                if (errors.Count > 0)
                    return HeaderResult.Failure(sql, errors.Select(e => $"Line {e.Line}, column {e.Column}: {e.Message}").ToList());
            }

            var target = script.Batches.SelectMany(b => b.Statements).FirstOrDefault(IsHeaderTarget);
            if (target == null)
                return HeaderResult.Failure(sql, new[] { "There's no procedure, function or view in this script to put a header on." });

            string output;
            int caret;
            try
            {
                var splice = Plan(sql, script.ScriptTokenStream, target.FirstTokenIndex, initials);
                output = sql.Substring(0, splice.Start) + splice.Text + sql.Substring(splice.Start + splice.Length);
                caret = splice.Start + splice.Caret;
            }
            catch (Exception ex)
            {
                return HeaderResult.Failure(sql, new[] { "SQL Helper hit an internal error and left this unchanged: " + ex.Message });
            }

            string problem = RoundTripCheck.FindCodeProblem(script.ScriptTokenStream, output, CreateParser);
            if (problem != null)
                return HeaderResult.Failure(sql, new[] { "SQL Helper left this unchanged because " + problem });

            return HeaderResult.Success(output, caret);
        }

        Splice Plan(string sql, IList<TSqlParserToken> tokens, int objectStart, string initials)
        {
            string newLine = _options.NewLine ?? LineBreakIn(sql);
            string date = _options.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            string created = $"{date} : {initials} - Created";
            string update = $"{date} : {initials} - {_options.Note?.Trim()}";
            var comments = LeadingComments(tokens, objectStart);

            var house = comments.LastOrDefault(c => c.TokenType == TSqlTokenType.MultilineComment && HeaderText.IsHouseHeader(c.Text));
            if (house != null)
            {
                int closingLine = house.Text.LastIndexOf('\n') + 1;
                string line = HeaderText.BodyIndent + update + newLine;
                return new Splice(house.Offset + closingLine, 0, line, HeaderText.BodyIndent.Length + update.Length);
            }

            var ssms = FindRuledBlock(sql, comments, HeaderText.IsSsmsRule, HeaderText.ParseSsms);
            if (ssms != null)
            {
                var (first, last, parsed) = ssms.Value;
                var history = parsed.HasHistory
                    ? new[] { $"{parsed.Created} : {HeaderText.InitialsOf(parsed.Author) ?? parsed.Author} - Created", update }
                    : new[] { created };
                string header = HeaderText.Build(parsed.Description.ToList(), history, newLine);
                int caret = parsed.Description.Count == 0 && !parsed.HasHistory
                    ? HeaderText.EmptyDescriptionEnd(newLine)
                    : header.Length - HeaderText.BottomRule.Length - newLine.Length;
                return Replace(first, last, header, caret);
            }

            var dashes = FindRuledBlock(sql, comments, HeaderText.IsDashRule, HeaderText.ParseDashHistory);
            if (dashes != null)
            {
                var (first, last, history) = dashes.Value;
                string header = HeaderText.Build(new string[0], history.Concat(new[] { update }).ToList(), newLine);
                return Replace(first, last, header, header.Length - HeaderText.BottomRule.Length - newLine.Length);
            }

            string fresh = HeaderText.Build(new string[0], new[] { created }, newLine);
            int objectOffset = tokens[objectStart].Offset;
            int lineStart = objectOffset == 0 ? 0 : sql.LastIndexOf('\n', objectOffset - 1) + 1;
            if (sql.Substring(lineStart, objectOffset - lineStart).Trim().Length == 0)
                return new Splice(lineStart, 0, fresh + newLine, HeaderText.EmptyDescriptionEnd(newLine));
            return new Splice(objectOffset, 0, newLine + fresh + newLine, newLine.Length + HeaderText.EmptyDescriptionEnd(newLine));
        }

        /// <summary>Replaces the comments from <paramref name="first"/> to <paramref name="last"/> with a new header.</summary>
        static Splice Replace(TSqlParserToken first, TSqlParserToken last, string header, int caret) =>
            new Splice(first.Offset, last.Offset + last.Text.TrimEnd('\r', '\n').Length - first.Offset, header, caret);

        /// <summary>
        /// The last block among the comments that is a run of <c>--</c> lines, one per line, between two rule lines,
        /// whose lines <paramref name="parse"/> accepts (returns non-null for).
        /// </summary>
        static (TSqlParserToken First, TSqlParserToken Last, T Parsed)? FindRuledBlock<T>(string sql, List<TSqlParserToken> comments,
            Func<string, bool> isRule, Func<IEnumerable<string>, T> parse) where T : class
        {
            (TSqlParserToken, TSqlParserToken, T)? found = null;
            for (int open = 0; open < comments.Count; open++)
            {
                if (!IsLineComment(comments[open]) || !isRule(comments[open].Text)) continue;
                for (int close = open + 1; close < comments.Count && IsLineComment(comments[close]) && OnNextLine(sql, comments[close - 1], comments[close]); close++)
                {
                    if (!isRule(comments[close].Text)) continue;
                    var parsed = parse(comments.Skip(open + 1).Take(close - open - 1).Select(c => c.Text));
                    if (parsed != null) found = (comments[open], comments[close], parsed);
                    open = close;
                    break;
                }
            }
            return found;
        }

        static bool IsLineComment(TSqlParserToken token) => token.TokenType == TSqlTokenType.SingleLineComment;

        static bool OnNextLine(string sql, TSqlParserToken previous, TSqlParserToken next)
        {
            int end = previous.Offset + previous.Text.Length;
            return sql.Substring(end, next.Offset - end).Count(c => c == '\n') + previous.Text.Count(c => c == '\n') == 1;
        }

        /// <summary>The comments between the object's first token and the code before it, in order.</summary>
        static List<TSqlParserToken> LeadingComments(IList<TSqlParserToken> tokens, int objectStart)
        {
            var comments = new List<TSqlParserToken>();
            for (int i = objectStart - 1; i >= 0; i--)
            {
                var type = tokens[i].TokenType;
                if (type == TSqlTokenType.WhiteSpace) continue;
                if (type != TSqlTokenType.SingleLineComment && type != TSqlTokenType.MultilineComment) break;
                comments.Insert(0, tokens[i]);
            }
            return comments;
        }

        static bool IsHeaderTarget(TSqlStatement statement) =>
            statement is ProcedureStatementBody || statement is FunctionStatementBody || statement is ViewStatementBody;

        static string LineBreakIn(string sql)
        {
            int index = sql.IndexOf('\n');
            if (index < 0) return Environment.NewLine;
            return index > 0 && sql[index - 1] == '\r' ? "\r\n" : "\n";
        }

        static TSqlParser CreateParser() => new TSql170Parser(initialQuotedIdentifiers: true);

        /// <summary>Replace <see cref="Length"/> characters at <see cref="Start"/> with <see cref="Text"/>.</summary>
        struct Splice
        {
            public Splice(int start, int length, string text, int caret)
            {
                Start = start;
                Length = length;
                Text = text;
                Caret = caret;
            }

            public int Start { get; }
            public int Length { get; }
            public string Text { get; }

            /// <summary>Cursor position, relative to <see cref="Start"/>.</summary>
            public int Caret { get; }
        }
    }
}