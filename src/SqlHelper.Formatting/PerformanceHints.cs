using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Hints;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Looks for patterns that are often slow or wrong (see docs/style-guide.md, Performance Hints) and says where they
    /// are. The script is only read, never changed. Every hint comes from the script alone: no schema, statistics or
    /// plan, so each one is something to look at rather than a certain fault.
    /// </summary>
    public static class PerformanceHints
    {
        public static HintResult Check(string sql, HintOptions options = null)
        {
            sql = sql ?? "";
            options = options ?? new HintOptions();
            if (sql.Trim().Length == 0) return HintResult.Success(new Hint[0]);

            TSqlScript script;
            IList<ParseError> errors;
            using (var reader = new StringReader(sql))
            {
                script = (TSqlScript)new TSql170Parser(initialQuotedIdentifiers: true).Parse(reader, out errors);
            }
            if (errors.Count > 0)
                return HintResult.Failure(errors.Select(e => $"Line {e.Line}, column {e.Column}: {e.Message}").ToList());

            try
            {
                var context = new HintContext(script, options);
                PredicateRules.Check(context);
                RowByRowRules.Check(context);
                QueryShapeRules.Check(context);
                LockingRules.Check(context);
                TempTableRules.Check(context);
                ProcedureRules.Check(context);
                DynamicSqlRules.Check(context);
                ModificationRules.Check(context);
                CorrectnessRules.Check(context);
                return HintResult.Success(context.Hints);
            }
            catch (Exception ex)
            {
                return HintResult.Failure(new[] { $"SQL Helper couldn't check this script ({ex.GetType().Name}: {ex.Message})." });
            }
        }
    }
}