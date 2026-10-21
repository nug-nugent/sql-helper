using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Layout
{
    /// <summary>
    /// Lays out the parts of an expression that span lines (subqueries, CASE, IN lists) wherever they turn up, and
    /// leaves everything else on the current line.
    /// </summary>
    internal sealed class ExpressionLayout : TSqlFragmentVisitor
    {
        readonly Layouter _layouter;
        int _level;

        public ExpressionLayout(Layouter layouter)
        {
            _layouter = layouter;
        }

        TokenLayout Layout => _layouter.TokenLayout;

        /// <summary>Lays out <paramref name="fragment"/>, whose first token is on a line indented to <paramref name="level"/>.</summary>
        public void LayOut(TSqlFragment fragment, int level)
        {
            if (fragment == null) return;
            int saved = _level;
            _level = level;
            fragment.Accept(this);
            _level = saved;
        }

        public override void ExplicitVisit(ScalarSubquery node)
        {
            // ( stays on the current line, the query goes one level in, ) comes back.
            Layout.NewLine(node.QueryExpression.FirstTokenIndex, _level + 1);
            _layouter.LayOutQuery(node.QueryExpression, _level + 1);
            Layout.NewLine(Layout.NextCode(node.QueryExpression.LastTokenIndex), _level);
        }

        public override void ExplicitVisit(InPredicate node)
        {
            LayOut(node.Expression, _level);
            if (node.Subquery != null)
            {
                LayOut(node.Subquery, _level);
                return;
            }

            var values = node.Values;
            for (int k = 0; k < values.Count; k++)
            {
                if (k == 0) Layout.NewLine(values[k].FirstTokenIndex, _level + 1);
                else Layout.NewLine(Layout.PreviousCode(values[k].FirstTokenIndex), _level + 1);
                LayOut(values[k], _level + 1);
            }
            Layout.NewLine(Layout.NextCode(values[values.Count - 1].LastTokenIndex), _level);
        }

        public override void ExplicitVisit(SearchedCaseExpression node)
        {
            foreach (var when in node.WhenClauses)
                LayOutWhen(when, when.WhenExpression);
            LayOutCaseEnd(node, node.ElseExpression);
        }

        public override void ExplicitVisit(SimpleCaseExpression node)
        {
            LayOut(node.InputExpression, _level);
            foreach (var when in node.WhenClauses)
                LayOutWhen(when, when.WhenExpression);
            LayOutCaseEnd(node, node.ElseExpression);
        }

        // WHEN one level in from the CASE line, THEN one further.
        void LayOutWhen(WhenClause when, TSqlFragment condition)
        {
            Layout.NewLine(when.FirstTokenIndex, _level + 1);
            LayOut(condition, _level + 1);
            Layout.NewLine(Layout.PreviousCode(when.ThenExpression.FirstTokenIndex), _level + 2);
            LayOut(when.ThenExpression, _level + 2);
        }

        // ELSE one level in, END back on the CASE line's level.
        void LayOutCaseEnd(CaseExpression node, ScalarExpression elseExpression)
        {
            if (elseExpression != null)
            {
                Layout.NewLine(Layout.PreviousCode(elseExpression.FirstTokenIndex), _level + 1);
                LayOut(elseExpression, _level + 1);
            }
            int end = node.LastTokenIndex;
            while (end > node.FirstTokenIndex && !Layout.Is(end, TSqlTokenType.End)) end--;
            Layout.NewLine(end, _level);
        }

        public override void ExplicitVisit(QueryDerivedTable node)
        {
            Layout.NewLine(node.QueryExpression.FirstTokenIndex, _level + 1);
            _layouter.LayOutQuery(node.QueryExpression, _level + 1);
            Layout.NewLine(Layout.NextCode(node.QueryExpression.LastTokenIndex), _level);
        }
    }
}
