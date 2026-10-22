using System;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.ServerSide
{
    /// <summary>The comments that mark up a server-side grid procedure (see docs/style-guide.md, Server-side markers).</summary>
    internal static class Marker
    {
        public const string Mark = "##ServerSideMark";
        public const string FinalSqlStart = "##ServerSideFinalSqlStart";
        public const string FinalSqlEnd = "##ServerSideFinalSqlEnd";

        /// <summary>The marker as written by SQL Helper: <c>--##ServerSideMark</c>.</summary>
        public static string Comment(string name) => "--" + name;

        /// <summary>Whether the token is the <paramref name="name"/> marker, ignoring case and spaces after the <c>--</c>.</summary>
        public static bool Is(TSqlParserToken token, string name) =>
            token.TokenType == TSqlTokenType.SingleLineComment
            && string.Equals(token.Text.TrimEnd('\r', '\n').Substring(2).Trim(), name, StringComparison.OrdinalIgnoreCase);
    }
}