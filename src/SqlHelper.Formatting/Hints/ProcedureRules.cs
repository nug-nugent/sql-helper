using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>SH6xx: procedures, batches, dynamic SQL and transactions.</summary>
    internal static class ProcedureRules
    {
        public static void Check(HintContext c)
        {
            foreach (var procedure in c.Tree.All<ProcedureStatementBody>())
            {
                if (!c.Tree.Descendants(procedure).OfType<PredicateSetStatement>().Any(s => s.IsOn && s.Options.HasFlag(SetOptions.NoCount)))
                    c.Suggestion("SH601", procedure.ProcedureReference.Name, "The procedure doesn't SET NOCOUNT ON, so every statement sends a \"rows affected\" message back to the caller. Add SET NOCOUNT ON at the start.");
                if (procedure.ProcedureReference.Name.BaseIdentifier.Value.StartsWith("sp_", StringComparison.OrdinalIgnoreCase))
                    c.Warning("SH602", procedure.ProcedureReference.Name, "Procedure names starting with sp_ are looked for in master first on every call, and can clash with system procedures. Use another prefix, or none.");
                Parameters(c, procedure);
            }
            foreach (var strings in c.Tree.All<ExecutableStringList>())
                c.Warning("SH603", c.Tree.Parent(strings), "EXEC (...) runs SQL text that can't take parameters, so values are pasted into it: a new plan for every value, and a way in for SQL injection. Use sp_executesql with parameters.");
            foreach (var table in c.Tree.All<NamedTableReference>()) UnqualifiedTable(c, table);
            foreach (var procedure in c.Tree.All<ExecutableProcedureReference>()) UnqualifiedProcedure(c, procedure);
            Transactions(c);
            foreach (var identity in c.Tree.All<GlobalVariableExpression>())
                if (identity.Name.Equals("@@IDENTITY", StringComparison.OrdinalIgnoreCase))
                    c.Warning("SH609", identity, "@@IDENTITY returns the last identity value made in the session, including by a trigger on another table. SCOPE_IDENTITY() (or an OUTPUT clause) returns the one this statement made.");
        }

        /// <summary>SH604: a table with no schema (not a temp table, CTE, alias or trigger table).</summary>
        static void UnqualifiedTable(HintContext c, NamedTableReference table)
        {
            var name = table.SchemaObject;
            if (name.SchemaIdentifier != null || HintContext.IsTempOrVariable(name)) return;
            string value = name.BaseIdentifier.Value;
            if (c.Module(table) is TriggerStatementBody && (value.Equals("inserted", StringComparison.OrdinalIgnoreCase) || value.Equals("deleted", StringComparison.OrdinalIgnoreCase)))
                return;
            var statement = c.Statement(table) ?? (TSqlFragment)c.Tree.Script;
            bool cte = c.Tree.Ancestors(table).OfType<StatementWithCtesAndXmlNamespaces>().Any(s => s.WithCtesAndXmlNamespaces != null
                && s.WithCtesAndXmlNamespaces.CommonTableExpressions.Any(e => HintContext.NameIs(e.ExpressionName, value)));
            // UPDATE t ... FROM dbo.Widget t names its target by alias.
            bool alias = c.Tree.Descendants(statement).OfType<TableReferenceWithAlias>().Any(t => t != table && HintContext.NameIs(t.Alias, value));
            if (cte || alias) return;
            c.Suggestion("SH604", table, $"{value} has no schema, so SQL Server has to work out which one each caller means, and callers with different default schemas can't share a plan. Write it as dbo.{value} (or its schema).");
        }

        static void UnqualifiedProcedure(HintContext c, ExecutableProcedureReference reference)
        {
            var name = reference.ProcedureReference?.ProcedureReference?.Name;
            if (name == null || name.SchemaIdentifier != null) return;
            string value = name.BaseIdentifier.Value;
            // System procedures (sp_executesql, xp_cmdshell) are found in sys whatever the schema.
            if (value.StartsWith("sp_", StringComparison.OrdinalIgnoreCase) || value.StartsWith("xp_", StringComparison.OrdinalIgnoreCase)) return;
            c.Suggestion("SH604", name, $"{value} has no schema, so SQL Server has to work out which one each caller means. Write it as dbo.{value} (or its schema).");
        }

        /// <summary>SH605: a parameter changed before use, or copied into a local variable.</summary>
        static void Parameters(HintContext c, ProcedureStatementBody procedure)
        {
            var inputs = new HashSet<string>(procedure.Parameters.Where(p => p.Modifier != ParameterModifier.Output).Select(p => p.VariableName.Value),
                StringComparer.OrdinalIgnoreCase);
            if (inputs.Count == 0) return;
            foreach (var fragment in c.Tree.Descendants(procedure))
            {
                VariableReference target = null;
                ScalarExpression value = null;
                switch (fragment)
                {
                    case SetVariableStatement set:
                        target = set.Variable;
                        value = set.Expression;
                        break;
                    case SelectSetVariable select:
                        target = select.Variable;
                        value = select.Expression;
                        break;
                    case DeclareVariableElement declare when !(declare is ProcedureParameter) && declare.Value != null:
                        value = declare.Value;
                        if (HintContext.Strip(value) is VariableReference copied && inputs.Contains(copied.Name))
                            c.Suggestion("SH605", declare, $"Copying {copied.Name} into a local variable hides its value from the optimiser, which then plans for an average value. Use the parameter directly, unless this is a deliberate fix for parameter sniffing.");
                        continue;
                }
                if (target == null) continue;
                if (inputs.Contains(target.Name))
                    c.Suggestion("SH605", fragment, $"Changing {target.Name} inside the procedure doesn't change the plan, which was made for the value passed in. Put the new value in a local variable, or use OPTION (RECOMPILE) on the statements that use it.");
                else if (HintContext.Strip(value) is VariableReference source && inputs.Contains(source.Name))
                    c.Suggestion("SH605", fragment, $"Copying {source.Name} into a local variable hides its value from the optimiser, which then plans for an average value. Use the parameter directly, unless this is a deliberate fix for parameter sniffing.");
            }
        }

        /// <summary>SH606–SH608.</summary>
        static void Transactions(HintContext c)
        {
            var statements = c.Tree.All<TSqlStatement>().ToList();
            bool commits = statements.Any(s => s is CommitTransactionStatement || s is RollbackTransactionStatement);
            int depth = 0;
            bool xactAbort = false;
            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case PredicateSetStatement set when set.Options.HasFlag(SetOptions.XactAbort):
                        xactAbort = set.IsOn;
                        break;
                    case BeginTransactionStatement begin:
                        if (!commits)
                            c.Warning("SH607", begin, "This transaction is never committed or rolled back in the script, so it holds its locks until the connection closes. End it with COMMIT (and ROLLBACK on error).");
                        else if (!xactAbort && c.Tree.Ancestor<TryCatchStatement>(begin) == null)
                            c.Warning("SH606", begin, "Without TRY ... CATCH or SET XACT_ABORT ON, an error part-way through can leave this transaction open, holding its locks. Wrap it in TRY ... CATCH with a ROLLBACK, or SET XACT_ABORT ON first.");
                        depth++;
                        break;
                    case CommitTransactionStatement _:
                        depth = Math.Max(0, depth - 1);
                        break;
                    case RollbackTransactionStatement _:
                        depth = 0;
                        break;
                    case WaitForStatement wait when wait.Statement == null && depth > 0:
                        c.Warning("SH608", wait, "WAITFOR inside a transaction holds its locks for the whole wait, blocking anyone who needs those rows. Commit before waiting.");
                        break;
                }
            }
        }
    }
}