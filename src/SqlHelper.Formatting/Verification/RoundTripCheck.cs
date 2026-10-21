using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Verification
{
    /// <summary>
    /// The guarantee behind every format: the output must parse, and must contain the same code and the same comments,
    /// each in the same order, as the input. Only whitespace and keyword case may differ (comments are compared as a
    /// separate sequence because a trailing comment may move back over a comma).
    /// </summary>
    internal static class RoundTripCheck
    {
        public static string FindProblem(IList<TSqlParserToken> inputTokens, string output, Func<TSqlParser> createParser)
        {
            var parser = createParser();
            IList<TSqlParserToken> outputTokens;
            using (var reader = new StringReader(output))
            {
                parser.Parse(reader, out IList<ParseError> errors);
                if (errors.Count > 0)
                    return $"the formatted SQL no longer parses ({errors[0].Message} at line {errors[0].Line}).";
            }
            using (var reader = new StringReader(output))
            {
                outputTokens = parser.GetTokenStream(reader, out _);
            }

            return Compare("code", Code(inputTokens), Code(outputTokens))
                ?? Compare("comments", Comments(inputTokens), Comments(outputTokens));
        }

        static List<TSqlParserToken> Code(IList<TSqlParserToken> tokens) =>
            tokens.Where(t => t.TokenType != TSqlTokenType.WhiteSpace && t.TokenType != TSqlTokenType.EndOfFile && !IsComment(t)).ToList();

        static List<TSqlParserToken> Comments(IList<TSqlParserToken> tokens) => tokens.Where(IsComment).ToList();

        static bool IsComment(TSqlParserToken t) =>
            t.TokenType == TSqlTokenType.SingleLineComment || t.TokenType == TSqlTokenType.MultilineComment;

        static string Compare(string what, List<TSqlParserToken> expected, List<TSqlParserToken> actual)
        {
            int count = Math.Min(expected.Count, actual.Count);
            for (int i = 0; i < count; i++)
            {
                if (!Same(expected[i], actual[i]))
                    return $"the {what} changed at '{expected[i].Text.Trim()}' (line {expected[i].Line}).";
            }
            if (expected.Count != actual.Count)
                return $"the {what} changed: {expected.Count} tokens in, {actual.Count} out.";
            return null;
        }

        static bool Same(TSqlParserToken x, TSqlParserToken y)
        {
            if (x.TokenType != y.TokenType) return false;
            switch (x.TokenType)
            {
                case TSqlTokenType.AsciiStringLiteral:
                case TSqlTokenType.UnicodeStringLiteral:
                case TSqlTokenType.QuotedIdentifier:
                case TSqlTokenType.MultilineComment:
                    return x.Text == y.Text;
                case TSqlTokenType.SingleLineComment:
                    return x.Text.TrimEnd('\r', '\n') == y.Text.TrimEnd('\r', '\n');
                default:
                    return string.Equals(x.Text, y.Text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
