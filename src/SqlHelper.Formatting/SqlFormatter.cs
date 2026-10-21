using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Layout;
using SqlHelper.Formatting.Output;
using SqlHelper.Formatting.Verification;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Formats T-SQL in the house style. Parsing is done by Microsoft's ScriptDom; layout only ever changes whitespace
    /// and keyword case, and every result is checked before it is returned.
    /// </summary>
    public sealed class SqlFormatter
    {
        readonly FormattingOptions _options;

        public SqlFormatter(FormattingOptions options = null)
        {
            _options = options ?? new FormattingOptions();
        }

        public FormatResult Format(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return FormatResult.Success(sql ?? "");

            TSqlScript script;
            using (var reader = new StringReader(sql))
            {
                script = (TSqlScript)CreateParser().Parse(reader, out IList<ParseError> errors);
                if (errors.Count > 0)
                    return FormatResult.Failure(sql, errors.Select(e => $"Line {e.Line}, column {e.Column}: {e.Message}").ToList());
            }

            string output;
            try
            {
                var layout = new TokenLayout(script.ScriptTokenStream);
                TokenRules.Apply(script, layout);
                new Layouter(layout).LayOut(script);
                output = Emitter.Emit(layout, _options);
            }
            catch (Exception ex)
            {
                // A layout bug must never cost the user their text, or crash the editor hosting us.
                return FormatResult.Failure(sql, new[] { "SQL Helper hit an internal error and left this unformatted: " + ex.Message });
            }

            string problem = RoundTripCheck.FindProblem(script.ScriptTokenStream, output, CreateParser);
            if (problem != null)
                return FormatResult.Failure(sql, new[] { "SQL Helper left this unformatted because " + problem });

            return FormatResult.Success(output);
        }

        static TSqlParser CreateParser() => new TSql170Parser(initialQuotedIdentifiers: true);
    }
}
