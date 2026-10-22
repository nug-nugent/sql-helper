using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Rewrites
{
    /// <summary>
    /// How a name is written: bare when it is a plain identifier, otherwise in square brackets (see docs/style-guide.md,
    /// Clean Up). The keywords that are bracketed are those SSMS knows, from BracketedNames.txt, plus anything
    /// ScriptDom itself reads as a keyword.
    /// </summary>
    internal static class Names
    {
        static readonly HashSet<string> Keywords = LoadKeywords();
        static readonly Dictionary<string, bool> Reserved = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary><paramref name="value"/> as it should be written: <c>Code</c>, <c>[Name]</c>, <c>[My Column]</c>.</summary>
        public static string Write(string value) => NeedsBrackets(value) ? "[" + value.Replace("]", "]]") + "]" : value;

        static bool NeedsBrackets(string value) =>
            !IsPlain(value) || Keywords.Contains(value) || IsReserved(value);

        /// <summary>Starts with a letter or <c>_</c>, and goes on with letters, digits, <c>_</c>, <c>@</c>, <c>$</c> or <c>#</c>.</summary>
        static bool IsPlain(string value) =>
            value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_')
            && value.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '$' || c == '#');

        static bool IsReserved(string value)
        {
            lock (Reserved)
            {
                if (!Reserved.TryGetValue(value, out bool reserved))
                {
                    using (var reader = new StringReader(value))
                    {
                        var tokens = new TSql170Parser(initialQuotedIdentifiers: true).GetTokenStream(reader, out _)
                            .Where(t => t.TokenType != TSqlTokenType.EndOfFile).ToList();
                        reserved = tokens.Count != 1 || tokens[0].TokenType != TSqlTokenType.Identifier;
                    }
                    Reserved[value] = reserved;
                }
                return reserved;
            }
        }

        static HashSet<string> LoadKeywords()
        {
            using (var stream = typeof(Names).Assembly.GetManifestResourceStream("SqlHelper.Formatting.BracketedNames.txt"))
            using (var reader = new StreamReader(stream))
            {
                return new HashSet<string>(
                    reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()),
                    StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}