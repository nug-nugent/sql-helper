using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Rewrites;
using SqlHelper.Formatting.Verification;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Rewrites a few tokens into the house's preferred form (see docs/style-guide.md, Clean Up): full join and keyword
    /// spellings, <c>AS</c> on aliases, and brackets exactly where a name needs them. Layout is left as typed, and every
    /// change is checked to be one of those rewrites.
    /// </summary>
    /// <remarks>
    /// Rewrites can nest (an alias turned round may contain a subquery whose joins are rewritten too), so they are made
    /// in rounds: each round makes the edits that don't overlap, re-parses and checks the result, until nothing is left.
    /// Brackets come last, in a round of their own, so names moved by an earlier round are bracketed where they end up.
    /// </remarks>
    public static class Cleanup
    {
        const int MaxRounds = 20;

        public static FormatResult Apply(string sql)
        {
            sql = sql ?? "";
            if (sql.Trim().Length == 0) return FormatResult.Success(sql);

            string current = sql;
            try
            {
                for (int round = 0; ; round++)
                {
                    var script = Parse(current, out var errors);
                    if (errors.Count > 0)
                        return round == 0
                            ? FormatResult.Failure(sql, errors.Select(e => $"Line {e.Line}, column {e.Column}: {e.Message}").ToList())
                            : FormatResult.Failure(sql, new[] { $"SQL Helper left this unchanged because the cleaned-up SQL no longer parses ({errors[0].Message})." });

                    var edits = round < MaxRounds ? Disjoint(StructuralRewrites.Find(current, script)) : new List<TokenEdit>();
                    bool brackets = edits.Count == 0;
                    if (brackets) edits = BracketRewrites.Find(script);

                    if (edits.Count > 0)
                    {
                        var (text, expected) = TokenEdit.Apply(current, script.ScriptTokenStream, edits);
                        string problem = RoundTripCheck.FindProblem(expected, text, CreateParser);
                        if (problem != null)
                            return FormatResult.Failure(sql, new[] { "SQL Helper left this unchanged because " + problem });
                        current = text;
                    }
                    if (brackets) return FormatResult.Success(current);
                }
            }
            catch (Exception ex)
            {
                return FormatResult.Failure(sql, new[] { "SQL Helper hit an internal error and left this unchanged: " + ex.Message });
            }
        }

        /// <summary>The edits to make this round: the smallest first, leaving any that overlap them for the next round.</summary>
        static List<TokenEdit> Disjoint(IEnumerable<TokenEdit> edits)
        {
            var chosen = new List<TokenEdit>();
            foreach (var edit in edits.OrderBy(e => e.Last - e.First))
                if (!chosen.Any(c => c.Overlaps(edit))) chosen.Add(edit);
            return chosen;
        }

        static TSqlScript Parse(string sql, out IList<ParseError> errors)
        {
            using (var reader = new StringReader(sql))
            {
                return (TSqlScript)CreateParser().Parse(reader, out errors);
            }
        }

        static TSqlParser CreateParser() => new TSql170Parser(initialQuotedIdentifiers: true);
    }
}