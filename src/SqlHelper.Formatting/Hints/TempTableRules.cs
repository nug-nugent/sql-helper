using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>SH5xx: temp tables and table variables.</summary>
    internal static class TempTableRules
    {
        const int JoinsBeforeIndex = 2;

        public static void Check(HintContext c)
        {
            foreach (var declare in c.Tree.All<DeclareTableVariableStatement>()) TableVariable(c, declare);
            foreach (var create in c.Tree.All<CreateTableStatement>())
                if (IsTemp(create.SchemaObjectName) && c.Tree.Ancestor<WhileStatement>(create) != null)
                    c.Warning("SH502", create, "Creating a temp table inside a loop creates and drops it on every pass. Create it once before the loop and empty it (TRUNCATE TABLE) each time round.");
            foreach (var select in c.Tree.All<SelectStatement>())
                if (IsTemp(select.Into) && c.Tree.Ancestor<WhileStatement>(select) != null)
                    c.Warning("SH502", select.Into, "SELECT ... INTO a temp table inside a loop creates and drops it on every pass. Create it once before the loop and empty it (TRUNCATE TABLE) each time round.");
            foreach (var procedure in c.Tree.All<ProcedureStatementBody>()) TempTableAfterData(c, procedure);
            foreach (var select in c.Tree.All<SelectStatement>())
                if (IsTemp(select.Into)) JoinedWithoutIndex(c, select);
        }

        static bool IsTemp(SchemaObjectName name) => name?.BaseIdentifier != null && name.BaseIdentifier.Value.StartsWith("#");

        static bool IsName(SchemaObjectName name, string value) =>
            name?.BaseIdentifier != null && string.Equals(name.BaseIdentifier.Value, value, StringComparison.OrdinalIgnoreCase);

        /// <summary>SH501: a table variable filled from a table with INSERT ... SELECT and then joined.</summary>
        static void TableVariable(HintContext c, DeclareTableVariableStatement declare)
        {
            string name = declare.Body.VariableName.Value;
            var scope = (TSqlFragment)c.Tree.Ancestor<TSqlBatch>(declare) ?? c.Tree.Script;
            bool filledFromTable = c.Tree.Descendants(scope).OfType<InsertSpecification>().Any(insert =>
                insert.Target is VariableTableReference target && string.Equals(target.Variable.Name, name, StringComparison.OrdinalIgnoreCase)
                && insert.InsertSource is SelectInsertSource source
                && c.Tree.Descendants(source).OfType<NamedTableReference>().Any());
            bool joined = c.Tree.Descendants(scope).OfType<QualifiedJoin>().Any(join =>
                new[] { join.FirstTableReference, join.SecondTableReference }.OfType<VariableTableReference>()
                    .Any(v => string.Equals(v.Variable.Name, name, StringComparison.OrdinalIgnoreCase)));
            if (filledFromTable && joined)
                c.Suggestion("SH501", declare.Body.VariableName, $"{name} is filled from a query and then joined. Before SQL Server 2019 the optimiser assumes a table variable holds one row, which can give a poor join plan for many; a temp table has statistics.");
        }

        /// <summary>SH503: <c>CREATE TABLE #t</c> part-way through a procedure, after it has read or changed data.</summary>
        static void TempTableAfterData(HintContext c, ProcedureStatementBody procedure)
        {
            bool dataTouched = false;
            foreach (var statement in c.Tree.Descendants(procedure).OfType<TSqlStatement>())
            {
                if (statement is CreateTableStatement create && IsTemp(create.SchemaObjectName))
                {
                    // One inside a loop is SH502.
                    if (dataTouched && c.Tree.Ancestor<WhileStatement>(create) == null)
                        c.Suggestion("SH503", create, "Creating a temp table after the procedure has started reading or changing data can make it recompile part-way through. Create temp tables together at the start.");
                }
                else if (statement is DataModificationStatement
                    || (statement is SelectStatement select && c.Tree.Descendants(select).OfType<FromClause>().Any()))
                    dataTouched = true;
            }
        }

        /// <summary>SH504: a temp table filled with SELECT ... INTO, then joined more than once without an index.</summary>
        static void JoinedWithoutIndex(HintContext c, SelectStatement select)
        {
            string name = select.Into.BaseIdentifier.Value;
            var scope = (TSqlFragment)c.Module(select) ?? c.Tree.Script;
            var later = c.Tree.Descendants(scope).Where(f => f.StartOffset > select.StartOffset + select.FragmentLength).ToList();
            bool indexed = later.OfType<CreateIndexStatement>().Any(i => IsName(i.OnName, name))
                || later.OfType<AlterTableAddTableElementStatement>().Any(a => IsName(a.SchemaObjectName, name));
            if (indexed) return;
            int joins = later.OfType<QualifiedJoin>().Count(join =>
                new[] { join.FirstTableReference, join.SecondTableReference }.OfType<NamedTableReference>().Any(t => IsName(t.SchemaObject, name)));
            if (joins >= JoinsBeforeIndex)
                c.Suggestion("SH504", select.Into, $"{name} is joined {joins} times but has no index, so each join reads all of it. If it holds many rows, add a clustered index on the join columns after filling it.");
        }
    }
}