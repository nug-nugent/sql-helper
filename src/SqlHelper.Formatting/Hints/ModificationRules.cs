using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>SH7xx: INSERT, UPDATE, DELETE and MERGE.</summary>
    internal static class ModificationRules
    {
        public static void Check(HintContext c)
        {
            foreach (var update in c.Tree.All<UpdateSpecification>())
            {
                if (WholeTable(update.Target, update.WhereClause, update.TopRowFilter, update.FromClause))
                    c.Warning("SH701", update, "This UPDATE has no WHERE, so it changes every row in the table. Add a WHERE if that isn't meant.");
                if (update.FromClause != null && update.FromClause.TableReferences.Any(t => c.Tree.Descendants(t).Prepend(t).Any(d => d is QualifiedJoin)))
                    c.Suggestion("SH702", update.FromClause, "UPDATE ... FROM with a join: if a target row matches more than one row of the join, SQL Server picks one of them to update from, and which one can change. Make sure the join matches at most one row per target row.");
            }
            foreach (var delete in c.Tree.All<DeleteSpecification>())
                if (WholeTable(delete.Target, delete.WhereClause, delete.TopRowFilter, delete.FromClause))
                    c.Warning("SH701", delete, "This DELETE has no WHERE, so it removes every row in the table. Add a WHERE if that isn't meant; to empty a table, TRUNCATE TABLE is much faster (but needs more permissions, skips triggers and resets identity).");
            foreach (var merge in c.Tree.All<MergeSpecification>())
            {
                if (!(merge.Target is NamedTableReference target) || HintContext.IsTempOrVariable(target.SchemaObject)) continue;
                if (!target.TableHints.Any(h => h.HintKind == TableHintKind.HoldLock || h.HintKind == TableHintKind.Serializable))
                    c.Warning("SH703", merge, "MERGE without HOLDLOCK on its target can insert duplicates or fail on a key when two sessions run it at once. Add WITH (HOLDLOCK) to the target, or use separate UPDATE and INSERT statements.");
            }
            foreach (var insert in c.Tree.All<InsertSpecification>())
            {
                if (insert.Columns.Count > 0 || insert.InsertSource == null
                    || (insert.InsertSource is ValuesInsertSource values && values.IsDefaultValues)) continue;
                if (insert.Target is NamedTableReference target && !HintContext.IsTempOrVariable(target.SchemaObject))
                    c.Warning("SH704", insert, "INSERT without a column list depends on the table's column order, and breaks (or puts values in the wrong columns) when a column is added. List the columns.");
            }
        }

        /// <summary>
        /// SH701: an UPDATE or DELETE of a permanent table with no WHERE, no TOP and no join to limit it. Temp tables and
        /// table variables are often updated whole on purpose.
        /// </summary>
        static bool WholeTable(TableReference target, WhereClause where, TopRowFilter top, FromClause from)
        {
            if (where != null || top != null) return false;
            if (target is NamedTableReference named && HintContext.IsTempOrVariable(named.SchemaObject)) return false;
            if (target is VariableTableReference) return false;
            return from == null || (from.TableReferences.Count == 1 && from.TableReferences[0] is NamedTableReference);
        }
    }
}