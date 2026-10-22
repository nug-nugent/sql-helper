using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Verification;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Terminates every statement in a script with a semicolon (see docs/style-guide.md, Semicolons). Only semicolons
    /// are added, or removed before a <c>--##ServerSideFinalSqlEnd</c> marker; the rest of the text is checked to be
    /// unchanged.
    /// </summary>
    /// <remarks>
    /// Statements are terminated, blocks (<c>BEGIN … END</c>, <c>TRY … CATCH</c>, the body of <c>IF</c> / <c>WHILE</c>)
    /// are not, except that a procedure, function, view or trigger is itself a statement. Nothing is added directly
    /// before <c>ELSE</c>. The statement before the server-side marker is left open, because the server appends to it.
    /// </remarks>
    public static class Semicolons
    {
        const string FinalSqlEndMarker = "##ServerSideFinalSqlEnd";

        public static FormatResult Add(string sql)
        {
            sql = sql ?? "";
            if (sql.Trim().Length == 0) return FormatResult.Success(sql);

            TSqlScript script;
            using (var reader = new StringReader(sql))
            {
                script = (TSqlScript)CreateParser().Parse(reader, out IList<ParseError> errors);
                if (errors.Count > 0)
                    return FormatResult.Failure(sql, errors.Select(e => $"Line {e.Line}, column {e.Column}: {e.Message}").ToList());
            }

            var tokens = script.ScriptTokenStream;
            string output;
            List<TSqlParserToken> expected;
            try
            {
                var ends = new SortedSet<int>();
                foreach (var statement in script.Batches.SelectMany(b => b.Statements)) CollectEnds(statement, ends);

                var removals = new SortedSet<int>();
                foreach (int marker in Enumerable.Range(0, tokens.Count).Where(i => IsFinalSqlEndMarker(tokens[i])))
                {
                    int previous = PreviousCode(tokens, marker);
                    if (previous < 0) continue;
                    ends.Remove(previous);
                    if (tokens[previous].TokenType == TSqlTokenType.Semicolon) removals.Add(previous);
                }

                var insertions = ends.Where(end => NeedsSemicolon(tokens, end)).ToList();
                output = Edit(sql, tokens, insertions, removals);
                expected = ExpectedTokens(tokens, insertions, removals);
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

        /// <summary>The last token of every statement that should end in a semicolon, within and including this one.</summary>
        static void CollectEnds(TSqlStatement statement, ISet<int> ends)
        {
            switch (statement)
            {
                case null:
                case LabelStatement _:
                    return;
                case BeginEndBlockStatement block:
                    CollectEnds(block.StatementList, ends);
                    return;
                case TryCatchStatement tryCatch:
                    CollectEnds(tryCatch.TryStatements, ends);
                    CollectEnds(tryCatch.CatchStatements, ends);
                    return;
                case IfStatement ifStatement:
                    CollectEnds(ifStatement.ThenStatement, ends);
                    CollectEnds(ifStatement.ElseStatement, ends);
                    return;
                case WhileStatement whileStatement:
                    CollectEnds(whileStatement.Statement, ends);
                    return;
                case ProcedureStatementBodyBase body:
                    CollectEnds(body.StatementList, ends);
                    break;
                case TriggerStatementBody trigger:
                    CollectEnds(trigger.StatementList, ends);
                    break;
            }
            if (statement.LastTokenIndex >= 0) ends.Add(statement.LastTokenIndex);
        }

        static void CollectEnds(StatementList list, ISet<int> ends)
        {
            if (list == null) return;
            foreach (var statement in list.Statements) CollectEnds(statement, ends);
        }

        /// <summary>False when the statement already ends in a semicolon, or one would break an <c>IF … ELSE</c>.</summary>
        static bool NeedsSemicolon(IList<TSqlParserToken> tokens, int end)
        {
            if (tokens[end].TokenType == TSqlTokenType.Semicolon) return false;
            int next = NextCode(tokens, end);
            return next < 0 || (tokens[next].TokenType != TSqlTokenType.Semicolon && tokens[next].TokenType != TSqlTokenType.Else);
        }

        static bool IsFinalSqlEndMarker(TSqlParserToken token) =>
            token.TokenType == TSqlTokenType.SingleLineComment
            && string.Equals(token.Text.TrimEnd('\r', '\n').Substring(2).Trim(), FinalSqlEndMarker, StringComparison.OrdinalIgnoreCase);

        static string Edit(string sql, IList<TSqlParserToken> tokens, IEnumerable<int> insertions, IEnumerable<int> removals)
        {
            var edits = insertions.Select(i => (Offset: tokens[i].Offset + tokens[i].Text.Length, Remove: false))
                .Concat(removals.Select(i => (Offset: tokens[i].Offset, Remove: true)))
                .OrderByDescending(e => e.Offset);
            string output = sql;
            foreach (var edit in edits)
                output = edit.Remove ? output.Remove(edit.Offset, 1) : output.Insert(edit.Offset, ";");
            return output;
        }

        /// <summary>The input's tokens with the planned semicolons added and removed: what the output must parse to.</summary>
        static List<TSqlParserToken> ExpectedTokens(IList<TSqlParserToken> tokens, ICollection<int> insertions, ICollection<int> removals)
        {
            var expected = new List<TSqlParserToken>();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!removals.Contains(i)) expected.Add(tokens[i]);
                if (insertions.Contains(i))
                    expected.Add(new TSqlParserToken(TSqlTokenType.Semicolon, tokens[i].Offset + tokens[i].Text.Length, ";", tokens[i].Line, tokens[i].Column));
            }
            return expected;
        }

        static int NextCode(IList<TSqlParserToken> tokens, int index)
        {
            for (int i = index + 1; i < tokens.Count; i++)
                if (IsCode(tokens[i])) return i;
            return -1;
        }

        static int PreviousCode(IList<TSqlParserToken> tokens, int index)
        {
            for (int i = index - 1; i >= 0; i--)
                if (IsCode(tokens[i])) return i;
            return -1;
        }

        static bool IsCode(TSqlParserToken token) =>
            token.TokenType != TSqlTokenType.WhiteSpace && token.TokenType != TSqlTokenType.EndOfFile
            && token.TokenType != TSqlTokenType.SingleLineComment && token.TokenType != TSqlTokenType.MultilineComment;

        static TSqlParser CreateParser() => new TSql170Parser(initialQuotedIdentifiers: true);
    }
}