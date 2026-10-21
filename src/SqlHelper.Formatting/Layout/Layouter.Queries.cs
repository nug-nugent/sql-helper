using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Layout
{
    // SELECT, INSERT, UPDATE, DELETE, MERGE and the clauses they share.
    internal sealed partial class Layouter
    {
        void LayOutSelectStatement(SelectStatement statement, int level)
        {
            LayOutCtes(statement.WithCtesAndXmlNamespaces, statement.QueryExpression, level);
            LayOutQuery(statement.QueryExpression, level, statement.Into);

            if (statement.OptimizerHints.Count > 0)
            {
                int option = _layout.FindTopLevel(statement.QueryExpression.LastTokenIndex + 1, statement.LastTokenIndex, i => _layout.Is(i, TSqlTokenType.Option));
                _layout.NewLine(option, level);
            }
        }

        /// <summary>
        /// WITH name AS ( … ), name AS ( … ), then <paramref name="body"/> starts a new line at <paramref name="level"/>.
        /// </summary>
        void LayOutCtes(WithCtesAndXmlNamespaces with, TSqlFragment body, int level)
        {
            if (with == null) return;

            var ctes = with.CommonTableExpressions;
            for (int k = 0; k < ctes.Count; k++)
            {
                var query = ctes[k].QueryExpression;
                if (k > 0) _layout.NewLine(_layout.PreviousCode(ctes[k].FirstTokenIndex), level);

                int openBracket = _layout.PreviousCode(query.FirstTokenIndex);
                _layout.NewLine(_layout.PreviousCode(openBracket), level); // AS
                _layout.NewLine(query.FirstTokenIndex, level + 1);
                LayOutQuery(query, level + 1);
                _layout.NewLine(_layout.NextCode(query.LastTokenIndex), level);
            }
            _layout.NewLine(body.FirstTokenIndex, level);
        }

        internal void LayOutQuery(QueryExpression query, int level, SchemaObjectName into = null)
        {
            switch (query)
            {
                case QuerySpecification spec:
                    LayOutQuerySpecification(spec, level, into);
                    return; // its ORDER BY is laid out with its other clauses

                case BinaryQueryExpression binary:
                    LayOutQuery(binary.FirstQueryExpression, level, into);
                    _layout.BlankLine(_layout.NextCode(binary.FirstQueryExpression.LastTokenIndex), level); // UNION [ALL] etc.
                    _layout.BlankLine(binary.SecondQueryExpression.FirstTokenIndex, level);
                    LayOutQuery(binary.SecondQueryExpression, level);
                    break;

                case QueryParenthesisExpression parenthesised:
                    _layout.NewLine(parenthesised.QueryExpression.FirstTokenIndex, level + 1);
                    LayOutQuery(parenthesised.QueryExpression, level + 1, into);
                    _layout.NewLine(_layout.NextCode(parenthesised.QueryExpression.LastTokenIndex), level);
                    break;
            }

            LayOutOrderBy(query.OrderByClause, level);
        }

        void LayOutQuerySpecification(QuerySpecification spec, int level, SchemaObjectName into)
        {
            bool oneLiner = spec.SelectElements.Count == 1
                && into == null
                && spec.FromClause == null
                && spec.WhereClause == null
                && spec.GroupByClause == null
                && spec.HavingClause == null
                && spec.OrderByClause == null
                && spec.OffsetClause == null
                && spec.ForClause == null;

            if (oneLiner)
            {
                _expressions.LayOut(spec.SelectElements[0], level);
                return;
            }

            LayOutList(spec.SelectElements, level + 1);

            if (into != null)
            {
                _layout.NewLine(_layout.PreviousCode(into.FirstTokenIndex), level);
                _layout.NewLine(into.FirstTokenIndex, level + 1);
            }

            LayOutFrom(spec.FromClause, level);
            LayOutWhere(spec.WhereClause, level);

            if (spec.GroupByClause != null)
            {
                _layout.NewLine(spec.GroupByClause.FirstTokenIndex, level);
                LayOutList(spec.GroupByClause.GroupingSpecifications, level + 1);
            }

            if (spec.HavingClause != null)
            {
                _layout.NewLine(spec.HavingClause.FirstTokenIndex, level);
                _layout.NewLine(_layout.ConditionStart(spec.HavingClause.SearchCondition), level + 1);
                LayOutCondition(spec.HavingClause.SearchCondition, level + 1, level + 1);
            }

            LayOutOrderBy(spec.OrderByClause, level);

            if (spec.OffsetClause != null) _layout.NewLine(spec.OffsetClause.FirstTokenIndex, level);
            if (spec.ForClause != null)
            {
                // FOR XML / FOR JSON / FOR BROWSE: the FOR keyword sits outside the clause's fragment.
                int forKeyword = spec.ForClause.FirstTokenIndex;
                while (forKeyword > spec.FirstTokenIndex && !_layout.Is(forKeyword, TSqlTokenType.For)) forKeyword--;
                _layout.NewLine(forKeyword, level);
            }
        }

        /// <summary>OUTPUT as a clause: its columns one per line, then INTO and the target table.</summary>
        void LayOutOutput(OutputClause output, OutputIntoClause outputInto, int level)
        {
            if (output != null)
            {
                _layout.NewLine(output.FirstTokenIndex, level);
                LayOutList(output.SelectColumns, level + 1);
            }
            if (outputInto != null)
            {
                _layout.NewLine(outputInto.FirstTokenIndex, level);
                LayOutList(outputInto.SelectColumns, level + 1);
                _layout.NewLine(_layout.PreviousCode(outputInto.IntoTable.FirstTokenIndex), level); // INTO
                _layout.NewLine(outputInto.IntoTable.FirstTokenIndex, level + 1);
            }
        }

        void LayOutOrderBy(OrderByClause orderBy, int level)
        {
            if (orderBy == null) return;
            _layout.NewLine(orderBy.FirstTokenIndex, level);
            LayOutList(orderBy.OrderByElements, level + 1);
        }

        void LayOutFrom(FromClause from, int level)
        {
            if (from == null) return;
            _layout.NewLine(from.FirstTokenIndex, level);
            LayOutTableReferences(from.TableReferences, level + 1);
        }

        void LayOutWhere(WhereClause where, int level)
        {
            if (where == null || where.SearchCondition == null) return; // WHERE CURRENT OF has no condition
            _layout.NewLine(where.FirstTokenIndex, level);
            _layout.NewLine(_layout.ConditionStart(where.SearchCondition), level + 1);
            LayOutCondition(where.SearchCondition, level + 1, level + 1);
        }

        void LayOutTableReferences(IList<TableReference> tables, int level)
        {
            for (int k = 0; k < tables.Count; k++)
            {
                if (k == 0) _layout.NewLine(tables[k].FirstTokenIndex, level);
                else _layout.NewLine(_layout.PreviousCode(tables[k].FirstTokenIndex), level);
                LayOutTableReference(tables[k], level);
            }
        }

        /// <summary>Each join starts its own line at <paramref name="level"/>; its extra ON conditions go one level further in.</summary>
        void LayOutTableReference(TableReference table, int level)
        {
            switch (table)
            {
                case JoinTableReference join:
                    LayOutTableReference(join.FirstTableReference, level);
                    _layout.NewLine(_layout.NextCode(join.FirstTableReference.LastTokenIndex), level);
                    LayOutTableReference(join.SecondTableReference, level);
                    if (join is QualifiedJoin qualified) LayOutCondition(qualified.SearchCondition, level, level + 1);
                    break;

                case QueryDerivedTable derived:
                    _layout.NewLine(derived.QueryExpression.FirstTokenIndex, level + 1);
                    LayOutQuery(derived.QueryExpression, level + 1);
                    _layout.NewLine(_layout.NextCode(derived.QueryExpression.LastTokenIndex), level);
                    break;

                case PivotedTableReference pivoted:
                    LayOutTableReference(pivoted.TableReference, level);
                    _layout.NewLine(_layout.NextCode(pivoted.TableReference.LastTokenIndex), level); // PIVOT
                    break;

                case UnpivotedTableReference unpivoted:
                    LayOutTableReference(unpivoted.TableReference, level);
                    _layout.NewLine(_layout.NextCode(unpivoted.TableReference.LastTokenIndex), level); // UNPIVOT
                    break;

                default:
                    _expressions.LayOut(table, level);
                    break;
            }
        }

        /// <summary>
        /// Lays out a search condition whose first token is on a line at <paramref name="lineLevel"/>: each further
        /// AND / OR starts a new line at <paramref name="operatorLevel"/>, and bracketed groups expand.
        /// </summary>
        void LayOutCondition(BooleanExpression condition, int lineLevel, int operatorLevel)
        {
            if (condition is BooleanBinaryExpression binary)
            {
                LayOutCondition(binary.FirstExpression, lineLevel, operatorLevel);
                _layout.NewLine(_layout.NextCode(binary.FirstExpression.LastTokenIndex), operatorLevel);
                LayOutCondition(binary.SecondExpression, operatorLevel, operatorLevel);
            }
            else if (condition is BooleanNotExpression not && not.Expression is BooleanParenthesisExpression)
            {
                LayOutCondition(not.Expression, lineLevel, operatorLevel); // NOT ( … ) expands like ( … )
            }
            else if (condition is BooleanParenthesisExpression bracketed)
            {
                _layout.NewLine(_layout.ConditionStart(bracketed.Expression), lineLevel + 1);
                LayOutCondition(bracketed.Expression, lineLevel + 1, lineLevel + 1);
                _layout.NewLine(bracketed.LastTokenIndex, lineLevel);
            }
            else
            {
                _expressions.LayOut(condition, lineLevel);
            }
        }

        void LayOutInsert(InsertSpecification insert, int level)
        {
            LayOutBracketedList(insert.Columns, level);
            LayOutInsertSource(insert.InsertSource, level);
            LayOutOutput(insert.OutputClause, insert.OutputIntoClause, level);
        }

        void LayOutInsertSource(InsertSource source, int level)
        {
            switch (source)
            {
                case ValuesInsertSource values:
                    _layout.NewLine(values.FirstTokenIndex, level);
                    if (values.RowValues.Count == 1)
                    {
                        // A single row lines up with the column list.
                        LayOutBracketedList(values.RowValues[0].ColumnValues, level);
                    }
                    else
                    {
                        // Several rows: one row per line, each kept on one line.
                        LayOutList(values.RowValues, level + 1);
                    }
                    break;

                case SelectInsertSource select:
                    _layout.NewLine(select.Select.FirstTokenIndex, level);
                    LayOutQuery(select.Select, level);
                    break;

                case ExecuteInsertSource execute:
                    _layout.NewLine(execute.FirstTokenIndex, level);
                    _expressions.LayOut(execute, level);
                    break;
            }
        }

        void LayOutUpdate(UpdateSpecification update, int level)
        {
            _layout.NewLine(update.Target.FirstTokenIndex, level + 1);
            _expressions.LayOut(update.Target, level + 1);
            LayOutSet(update.SetClauses, level);
            LayOutOutput(update.OutputClause, update.OutputIntoClause, level);
            LayOutFrom(update.FromClause, level);
            LayOutWhere(update.WhereClause, level);
        }

        void LayOutSet(IList<SetClause> setClauses, int level)
        {
            if (setClauses.Count == 0) return;
            _layout.NewLine(_layout.PreviousCode(setClauses[0].FirstTokenIndex), level);
            LayOutList(setClauses, level + 1);
        }

        void LayOutDelete(DeleteSpecification delete, int level)
        {
            // DELETE FROM dbo.T has FROM but no FROM clause; DELETE t FROM dbo.T t has both.
            int beforeTarget = _layout.PreviousCode(delete.Target.FirstTokenIndex);
            if (delete.FromClause == null && _layout.Is(beforeTarget, TSqlTokenType.From))
                _layout.NewLine(beforeTarget, level);
            _layout.NewLine(delete.Target.FirstTokenIndex, level + 1);
            _expressions.LayOut(delete.Target, level + 1);
            LayOutOutput(delete.OutputClause, delete.OutputIntoClause, level);
            LayOutFrom(delete.FromClause, level);
            LayOutWhere(delete.WhereClause, level);
        }

        void LayOutMerge(MergeSpecification merge, int level)
        {
            LayOutOutput(merge.OutputClause, merge.OutputIntoClause, level);
            _layout.NewLine(merge.Target.FirstTokenIndex, level + 1);

            _layout.NewLine(_layout.PreviousCode(merge.TableReference.FirstTokenIndex), level); // USING
            _layout.NewLine(merge.TableReference.FirstTokenIndex, level + 1);
            LayOutTableReference(merge.TableReference, level + 1);

            _layout.NewLine(_layout.PreviousCode(_layout.ConditionStart(merge.SearchCondition)), level + 1); // ON
            LayOutCondition(merge.SearchCondition, level + 1, level + 2);

            foreach (var clause in merge.ActionClauses)
            {
                // The WHEN … THEN tokens belong to no fragment; WHEN is the nearest one before the action.
                int when = clause.Action.FirstTokenIndex;
                while (when >= 0 && !_layout.Is(when, TSqlTokenType.When)) when--;
                _layout.NewLine(when, level);
                if (clause.SearchCondition != null) _expressions.LayOut(clause.SearchCondition, level);

                _layout.NewLine(clause.Action.FirstTokenIndex, level + 1);
                switch (clause.Action)
                {
                    case UpdateMergeAction update:
                        LayOutSet(update.SetClauses, level + 1);
                        break;
                    case InsertMergeAction insert:
                        LayOutBracketedList(insert.Columns, level + 1);
                        LayOutInsertSource(insert.Source, level + 1);
                        break;
                }
            }
        }
    }
}
