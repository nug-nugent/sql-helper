using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>SH4xx: query hints, table hints and locking.</summary>
    internal static class LockingRules
    {
        /// <summary>Query hints that fix part of the plan. RECOMPILE, OPTIMIZE FOR and the like only guide it.</summary>
        static readonly Dictionary<OptimizerHintKind, string> PlanHints = new Dictionary<OptimizerHintKind, string>
        {
            [OptimizerHintKind.ForceOrder] = "FORCE ORDER",
            [OptimizerHintKind.MaxDop] = "MAXDOP",
            [OptimizerHintKind.Fast] = "FAST",
            [OptimizerHintKind.LoopJoin] = "LOOP JOIN",
            [OptimizerHintKind.HashJoin] = "HASH JOIN",
            [OptimizerHintKind.MergeJoin] = "MERGE JOIN",
            [OptimizerHintKind.HashGroup] = "HASH GROUP",
            [OptimizerHintKind.OrderGroup] = "ORDER GROUP",
            [OptimizerHintKind.ConcatUnion] = "CONCAT UNION",
            [OptimizerHintKind.HashUnion] = "HASH UNION",
            [OptimizerHintKind.MergeUnion] = "MERGE UNION",
            [OptimizerHintKind.KeepFixedPlan] = "KEEPFIXED PLAN",
            [OptimizerHintKind.UsePlan] = "USE PLAN",
            [OptimizerHintKind.TableHints] = "TABLE HINT",
        };

        const string PlanHintAdvice = " fixes part of the plan, so it stays the same when the data or indexes change. Make sure it still helps, and say why in a comment.";

        public static void Check(HintContext c)
        {
            foreach (var hint in c.Tree.All<TableHint>())
            {
                switch (hint.HintKind)
                {
                    case TableHintKind.NoLock:
                    case TableHintKind.ReadUncommitted:
                        c.Warning("SH401", hint, $"{(hint.HintKind == TableHintKind.NoLock ? "NOLOCK" : "READUNCOMMITTED")} reads uncommitted data, and can return rows twice or miss them while pages move. If readers block writers, consider READ COMMITTED SNAPSHOT instead.");
                        break;
                    case TableHintKind.Index:
                    case TableHintKind.ForceSeek:
                    case TableHintKind.ForceScan:
                        c.Suggestion("SH403", hint, HintContext.Text(hint).Split('(')[0].Trim().ToUpperInvariant() + PlanHintAdvice);
                        break;
                }
            }
            foreach (var join in c.Tree.All<QualifiedJoin>())
                if (join.JoinHint != JoinHint.None)
                    c.Suggestion("SH403", join.SecondTableReference, $"The {join.JoinHint.ToString().ToUpperInvariant()} join hint{PlanHintAdvice} It also fixes the join order.");
            foreach (var hint in c.Tree.All<OptimizerHint>())
                if (PlanHints.TryGetValue(hint.HintKind, out string name))
                    c.AddAtToken("SH403", HintSeverity.Suggestion, KeywordOf(hint), $"OPTION ({name}){PlanHintAdvice}");

            if (c.Options.FlagReadUncommittedIsolation)
                foreach (var isolation in c.Tree.All<SetTransactionIsolationLevelStatement>())
                    if (isolation.Level == IsolationLevel.ReadUncommitted)
                        c.Warning("SH402", isolation, "READ UNCOMMITTED reads uncommitted data for every table that follows, and can return rows twice or miss them while pages move. If readers block writers, consider READ COMMITTED SNAPSHOT instead.");

            foreach (var procedure in c.Tree.All<ProcedureStatementBody>())
                foreach (var option in procedure.Options)
                    if (option.OptionKind == ProcedureOptionKind.Recompile)
                        c.Suggestion("SH404", option, "WITH RECOMPILE compiles the whole procedure on every call. If only one statement needs a fresh plan, put OPTION (RECOMPILE) on that statement instead.");

            foreach (var rowCount in c.Tree.All<SetRowCountStatement>())
                if (!(HintContext.Strip(rowCount.NumberRows) is IntegerLiteral literal && literal.Value == "0"))
                    c.Warning("SH405", rowCount, "SET ROWCOUNT is deprecated for INSERT, UPDATE and DELETE, and the optimiser can't see it when it plans the query. Use TOP (n) on the statement instead.");
        }

        /// <summary>
        /// The token a query hint starts at. ScriptDom's fragment for <c>MAXDOP 1</c> or <c>FAST 10</c> covers only the
        /// number, so the keyword is the code token before it.
        /// </summary>
        static TSqlParserToken KeywordOf(OptimizerHint hint)
        {
            int index = hint.FirstTokenIndex;
            if (hint is LiteralOptimizerHint)
                for (int i = index - 1; i >= 0; i--)
                {
                    var type = hint.ScriptTokenStream[i].TokenType;
                    if (type == TSqlTokenType.WhiteSpace || type == TSqlTokenType.SingleLineComment || type == TSqlTokenType.MultilineComment) continue;
                    index = i;
                    break;
                }
            return hint.ScriptTokenStream[index];
        }
    }
}