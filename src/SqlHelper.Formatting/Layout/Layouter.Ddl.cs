using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Layout
{
    // CREATE / ALTER of procedures, functions, views, tables and indexes.
    internal sealed partial class Layouter
    {
        void LayOutProcedure(ProcedureStatementBody procedure, int level)
        {
            LayOutParameters(procedure.Parameters, level);
            LayOutModuleBody(procedure.StatementList, level);
        }

        void LayOutFunction(FunctionStatementBody function, int level)
        {
            LayOutParameters(function.Parameters, level);

            int afterParameters = function.Parameters.Count > 0
                ? function.Parameters[function.Parameters.Count - 1].LastTokenIndex
                : function.Name.LastTokenIndex;
            int returns = afterParameters;
            while (returns >= 0 && returns <= function.LastTokenIndex && !_layout.IsWord(returns, "RETURNS")) returns = _layout.NextCode(returns);
            if (returns <= function.LastTokenIndex) _layout.NewLine(returns, level);

            switch (function.ReturnType)
            {
                case TableValuedFunctionReturnType table:
                    LayOutTableDefinition(table.DeclareTableVariableBody.Definition, level);
                    break;

                case SelectFunctionReturnType select:
                    // RETURNS TABLE AS RETURN ( SELECT … )
                    int returnKeyword = _layout.PreviousCode(select.SelectStatement.FirstTokenIndex);
                    _layout.NewLine(_layout.PreviousCode(returnKeyword), level); // AS
                    _layout.NewLine(returnKeyword, level);
                    LayOutSelectStatement(select.SelectStatement, level);
                    return;
            }

            LayOutModuleBody(function.StatementList, level);
        }

        /// <summary>Parameters one per line one level in, inside brackets if they were written with them.</summary>
        void LayOutParameters(IList<ProcedureParameter> parameters, int level)
        {
            if (parameters.Count == 0) return;
            if (_layout.Is(_layout.PreviousCode(parameters[0].FirstTokenIndex), TSqlTokenType.LeftParenthesis))
                LayOutBracketedList(parameters, level);
            else
                LayOutList(parameters, level + 1);
        }

        /// <summary>AS on its own line, then the module's statements at the same level.</summary>
        void LayOutModuleBody(StatementList body, int level)
        {
            if (body == null || body.Statements.Count == 0) return;
            _layout.NewLine(_layout.PreviousCode(body.Statements[0].FirstTokenIndex), level); // AS
            LayOutStatements(body.Statements, level);
        }

        void LayOutView(ViewStatementBody view, int level)
        {
            _layout.NewLine(_layout.PreviousCode(view.SelectStatement.FirstTokenIndex), level); // AS
            _layout.NewLine(view.SelectStatement.FirstTokenIndex, level);
            LayOutSelectStatement(view.SelectStatement, level);
        }

        /// <summary>Columns and constraints one per line, as a bracketed list.</summary>
        void LayOutTableDefinition(TableDefinition definition, int level)
        {
            if (definition == null) return;
            var items = definition.ColumnDefinitions.Cast<TSqlFragment>()
                .Concat(definition.TableConstraints)
                .Concat(definition.Indexes)
                .OrderBy(item => item.FirstTokenIndex)
                .ToList();
            LayOutBracketedList(items, level);
        }

        void LayOutCreateIndex(CreateIndexStatement index, int level)
        {
            _layout.NewLine(_layout.PreviousCode(index.OnName.FirstTokenIndex), level + 1); // ON
            if (index.FilterPredicate != null)
            {
                _layout.NewLine(_layout.PreviousCode(_layout.ConditionStart(index.FilterPredicate)), level); // WHERE
                _layout.NewLine(_layout.ConditionStart(index.FilterPredicate), level + 1);
                LayOutCondition(index.FilterPredicate, level + 1, level + 1);
            }
        }
    }
}
