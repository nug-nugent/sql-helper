using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Dynamic;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Turns statements into a parameterised <c>sp_executesql</c> call and back (see docs/style-guide.md, Dynamic
    /// SQL). Both work on a selection of the document, or the whole document when the selection is empty, and return
    /// the whole document; declarations are looked up anywhere in the selection's batch.
    /// </summary>
    public static class DynamicSql
    {
        public static FormatResult ToDynamic(string sql, int selectionStart = 0, int selectionLength = 0) =>
            Run(sql, selectionStart, selectionLength, ToDynamicConversion.Apply);

        public static FormatResult FromDynamic(string sql, int selectionStart = 0, int selectionLength = 0) =>
            Run(sql, selectionStart, selectionLength, FromDynamicConversion.Apply);

        static FormatResult Run(string sql, int selectionStart, int selectionLength, Func<string, TSqlScript, int, int, FormatResult> apply)
        {
            sql = sql ?? "";
            if (selectionStart < 0 || selectionLength < 0 || selectionStart + selectionLength > sql.Length)
                throw new ArgumentOutOfRangeException(nameof(selectionLength));

            int start = selectionLength == 0 ? 0 : selectionStart;
            int end = selectionLength == 0 ? sql.Length : selectionStart + selectionLength;
            while (start < end && char.IsWhiteSpace(sql[start])) start++;
            while (end > start && char.IsWhiteSpace(sql[end - 1])) end--;
            if (start == end) return FormatResult.Failure(sql, new[] { "There's no SQL here to convert." });

            var script = Parse(sql, out IList<ParseError> errors);
            if (errors.Count > 0)
                return FormatResult.Failure(sql, errors.Select(e => $"Line {e.Line}, column {e.Column}: {e.Message}").ToList());

            try
            {
                return apply(sql, script, start, end);
            }
            catch (Exception ex)
            {
                return FormatResult.Failure(sql, new[] { "SQL Helper hit an internal error and left this unchanged: " + ex.Message });
            }
        }

        internal static TSqlScript Parse(string sql, out IList<ParseError> errors)
        {
            using (var reader = new StringReader(sql))
            {
                return (TSqlScript)CreateParser().Parse(reader, out errors);
            }
        }

        internal static TSqlParser CreateParser() => new TSql170Parser(initialQuotedIdentifiers: true);

        /// <summary>The tokens a piece of new code text parses to.</summary>
        internal static List<TSqlParserToken> Tokens(string text)
        {
            using (var reader = new StringReader(text))
            {
                return CreateParser().GetTokenStream(reader, out _).Where(t => t.TokenType != TSqlTokenType.EndOfFile).ToList();
            }
        }

        /// <summary>The batch holding <paramref name="offset"/>.</summary>
        internal static TSqlBatch BatchAt(TSqlScript script, int offset) =>
            script.Batches.LastOrDefault(b => b.StartOffset <= offset) ?? script.Batches.FirstOrDefault();

        /// <summary>The whitespace at the start of the line holding <paramref name="offset"/>.</summary>
        internal static string IndentOfLine(string sql, int offset)
        {
            int lineStart = offset == 0 ? 0 : sql.LastIndexOf('\n', offset - 1) + 1;
            int end = lineStart;
            while (end < sql.Length && (sql[end] == ' ' || sql[end] == '\t')) end++;
            return sql.Substring(lineStart, end - lineStart);
        }

        internal static int EndOffset(TSqlFragment fragment) => fragment.StartOffset + fragment.FragmentLength;
    }
}