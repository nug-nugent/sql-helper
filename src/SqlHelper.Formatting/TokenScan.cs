using System;
using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting
{
    /// <summary>Finding code tokens and line breaks in a script, for the commands that splice into its text.</summary>
    internal static class TokenScan
    {
        /// <summary>The index of the next code token after <paramref name="index"/>, or -1.</summary>
        public static int NextCode(IList<TSqlParserToken> tokens, int index)
        {
            for (int i = index + 1; i < tokens.Count; i++)
                if (IsCode(tokens[i])) return i;
            return -1;
        }

        /// <summary>The index of the last code token before <paramref name="index"/>, or -1.</summary>
        public static int PreviousCode(IList<TSqlParserToken> tokens, int index)
        {
            for (int i = index - 1; i >= 0; i--)
                if (IsCode(tokens[i])) return i;
            return -1;
        }

        public static bool IsCode(TSqlParserToken token) =>
            token.TokenType != TSqlTokenType.WhiteSpace && token.TokenType != TSqlTokenType.EndOfFile && !IsComment(token);

        public static bool IsComment(TSqlParserToken token) =>
            token.TokenType == TSqlTokenType.SingleLineComment || token.TokenType == TSqlTokenType.MultilineComment;

        /// <summary>The script's own line break, so edits don't mix CRLF and LF.</summary>
        public static string LineBreakIn(string sql)
        {
            int index = sql.IndexOf('\n');
            if (index < 0) return Environment.NewLine;
            return index > 0 && sql[index - 1] == '\r' ? "\r\n" : "\n";
        }
    }
}