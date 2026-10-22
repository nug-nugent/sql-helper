using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.ServerSide;
using SqlHelper.Formatting.Verification;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Marks up the first procedure in a script as a server-side grid procedure (see docs/style-guide.md, Server-side
    /// markers). Only marker comments are added, and the grid's <c>SELECT</c> loses its semicolon; the rest of the text
    /// is checked to be unchanged.
    /// </summary>
    /// <remarks>
    /// The procedure's last statement is the grid's <c>SELECT</c>. <c>--##ServerSideMark</c> goes directly inside the
    /// body and again after the <c>SELECT</c>; <c>--##ServerSideFinalSqlStart</c> / <c>--##ServerSideFinalSqlEnd</c>
    /// surround the <c>SELECT</c>, each separated from it by a blank line. Markers already there are left alone.
    /// </remarks>
    public static class ServerSideMarkers
    {
        public static FormatResult Add(string sql)
        {
            sql = sql ?? "";

            TSqlScript script;
            using (var reader = new StringReader(sql))
            {
                script = (TSqlScript)CreateParser().Parse(reader, out IList<ParseError> errors);
                if (errors.Count > 0)
                    return FormatResult.Failure(sql, errors.Select(e => $"Line {e.Line}, column {e.Column}: {e.Message}").ToList());
            }

            var procedure = script.Batches.SelectMany(b => b.Statements).OfType<ProcedureStatementBody>().FirstOrDefault();
            if (procedure == null)
                return FormatResult.Failure(sql, new[] { "There's no procedure in this script to add server-side markers to." });

            var tokens = script.ScriptTokenStream;
            string output;
            List<TSqlParserToken> expected;
            try
            {
                string refusal = Plan(sql, tokens, procedure, out List<Insertion> insertions, out int removal);
                if (refusal != null) return FormatResult.Failure(sql, new[] { refusal });

                output = Edit(sql, tokens, insertions, removal);
                expected = ExpectedTokens(tokens, insertions, removal);
            }
            catch (Exception ex)
            {
                return FormatResult.Failure(sql, new[] { "SQL Helper hit an internal error and left this unchanged: " + ex.Message });
            }

            string problem = RoundTripCheck.FindProblem(expected, output, CreateParser);
            if (problem != null)
                return FormatResult.Failure(sql, new[] { "SQL Helper left this unchanged because " + problem });

            return FormatResult.Success(output);
        }

        /// <summary>
        /// The markers to add and the token index of the semicolon to remove (-1 for none), or a message saying why the
        /// procedure can't be marked up.
        /// </summary>
        static string Plan(string sql, IList<TSqlParserToken> tokens, ProcedureStatementBody procedure, out List<Insertion> insertions, out int removal)
        {
            insertions = new List<Insertion>();
            removal = -1;

            var statements = procedure.StatementList?.Statements ?? (IList<TSqlStatement>)new TSqlStatement[0];
            int opener;
            if (statements.Count == 1 && statements[0] is BeginEndBlockStatement block)
            {
                opener = block.FirstTokenIndex;
                statements = block.StatementList?.Statements ?? (IList<TSqlStatement>)new TSqlStatement[0];
            }
            else
            {
                opener = statements.Count > 0 ? TokenScan.PreviousCode(tokens, statements[0].FirstTokenIndex) : -1;
            }

            if (!(statements.LastOrDefault() is SelectStatement select))
                return "The procedure's last statement has to be the grid's SELECT.";

            int first = select.FirstTokenIndex;
            int last = select.LastTokenIndex;
            if (tokens[last].TokenType == TSqlTokenType.Semicolon) removal = last;

            string newLine = TokenScan.LineBreakIn(sql);
            int selectLineStart = LineStart(sql, tokens[first].Offset);
            string indent = sql.Substring(selectLineStart, tokens[first].Offset - selectLineStart);
            if (indent.Trim().Length > 0)
                return "The grid's SELECT has to start on a line of its own.";
            int selectLineEnd = LineEnd(sql, tokens[last].Offset + tokens[last].Text.Length);
            int after = TokenScan.NextCode(tokens, last);
            if (after >= 0 && tokens[after].Offset < selectLineEnd)
                return "The grid's SELECT has to end its line.";

            var before = Comments(tokens, opener, first);
            var following = Comments(tokens, last, after < 0 ? tokens.Count : after);

            if (!before.Any(t => Marker.Is(t, Marker.Mark)))
            {
                int bodyStart = LineEnd(sql, tokens[opener].Offset + tokens[opener].Text.Length) + newLine.Length;
                if (tokens[TokenScan.NextCode(tokens, opener)].Offset < bodyStart)
                    return $"The procedure's body has to start on the line after its {tokens[opener].Text.ToUpperInvariant()}.";
                insertions.Add(new Insertion(bodyStart, indent + Marker.Comment(Marker.Mark) + newLine, Marker.Mark));
            }

            if (!before.Any(t => Marker.Is(t, Marker.FinalSqlStart)))
                insertions.Add(new Insertion(selectLineStart, indent + Marker.Comment(Marker.FinalSqlStart) + newLine + newLine, Marker.FinalSqlStart));

            var end = following.FirstOrDefault(t => Marker.Is(t, Marker.FinalSqlEnd));
            bool hasClosingMark = following.Any(t => Marker.Is(t, Marker.Mark));
            if (end == null)
            {
                string text = newLine + newLine + indent + Marker.Comment(Marker.FinalSqlEnd);
                var markers = new List<string> { Marker.FinalSqlEnd };
                if (!hasClosingMark)
                {
                    text += newLine + indent + Marker.Comment(Marker.Mark);
                    markers.Add(Marker.Mark);
                }
                insertions.Add(new Insertion(selectLineEnd, text, markers.ToArray()));
            }
            else if (!hasClosingMark)
            {
                insertions.Add(new Insertion(LineEnd(sql, end.Offset + end.Text.TrimEnd('\r', '\n').Length), newLine + indent + Marker.Comment(Marker.Mark), Marker.Mark));
            }

            return null;
        }

        /// <summary>The comment tokens strictly between two token indexes.</summary>
        static List<TSqlParserToken> Comments(IList<TSqlParserToken> tokens, int from, int to) =>
            tokens.Skip(from + 1).Take(Math.Max(0, to - from - 1)).Where(TokenScan.IsComment).ToList();

        static int LineStart(string sql, int offset) => offset == 0 ? 0 : sql.LastIndexOf('\n', offset - 1) + 1;

        /// <summary>Where the line containing <paramref name="offset"/> ends, before its line break.</summary>
        static int LineEnd(string sql, int offset)
        {
            int index = sql.IndexOf('\n', offset);
            if (index < 0) return sql.Length;
            return index > 0 && sql[index - 1] == '\r' ? index - 1 : index;
        }

        static string Edit(string sql, IList<TSqlParserToken> tokens, List<Insertion> insertions, int removal)
        {
            var output = new StringBuilder(sql.Length + insertions.Sum(i => i.Text.Length));
            int position = 0;
            int removalOffset = removal >= 0 ? tokens[removal].Offset : -1;
            foreach (var insertion in insertions.OrderBy(i => i.Offset))
            {
                if (removalOffset >= position && removalOffset < insertion.Offset)
                {
                    output.Append(sql, position, removalOffset - position);
                    position = removalOffset + 1;
                }
                output.Append(sql, position, insertion.Offset - position).Append(insertion.Text);
                position = insertion.Offset;
            }
            if (removalOffset >= position)
            {
                output.Append(sql, position, removalOffset - position);
                position = removalOffset + 1;
            }
            return output.Append(sql, position, sql.Length - position).ToString();
        }

        /// <summary>The input's tokens with the markers added and the semicolon removed: what the output must parse to.</summary>
        static List<TSqlParserToken> ExpectedTokens(IList<TSqlParserToken> tokens, List<Insertion> insertions, int removal)
        {
            var pending = new Queue<Insertion>(insertions.OrderBy(i => i.Offset));
            var expected = new List<TSqlParserToken>();
            for (int i = 0; i < tokens.Count; i++)
            {
                while (pending.Count > 0 && pending.Peek().Offset <= tokens[i].Offset) AddMarkers(expected, pending.Dequeue(), tokens[i]);
                if (i != removal) expected.Add(tokens[i]);
            }
            while (pending.Count > 0) AddMarkers(expected, pending.Dequeue(), tokens[tokens.Count - 1]);
            return expected;
        }

        static void AddMarkers(List<TSqlParserToken> expected, Insertion insertion, TSqlParserToken near)
        {
            foreach (string name in insertion.Markers)
                expected.Add(new TSqlParserToken(TSqlTokenType.SingleLineComment, insertion.Offset, Marker.Comment(name), near.Line, near.Column));
        }

        static TSqlParser CreateParser() => new TSql170Parser(initialQuotedIdentifiers: true);

        /// <summary><see cref="Text"/>, holding <see cref="Markers"/> in order, to be inserted at <see cref="Offset"/>.</summary>
        sealed class Insertion
        {
            public Insertion(int offset, string text, params string[] markers)
            {
                Offset = offset;
                Text = text;
                Markers = markers;
            }

            public int Offset { get; }
            public string Text { get; }
            public string[] Markers { get; }
        }
    }
}