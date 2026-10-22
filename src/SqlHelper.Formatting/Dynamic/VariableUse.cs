using System;
using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Dynamic
{
    /// <summary>
    /// The variables a fragment declares, reads and assigns. A named <c>EXEC</c> parameter (<c>@x</c> in
    /// <c>EXEC p @x = 1</c>) is the procedure's parameter, not a variable, so it isn't a reference.
    /// </summary>
    internal sealed class VariableUse : TSqlFragmentVisitor
    {
        readonly HashSet<VariableReference> _parameterNames = new HashSet<VariableReference>();

        /// <summary>Every reference to a variable, in the order visited.</summary>
        public List<VariableReference> References { get; } = new List<VariableReference>();

        /// <summary>Variables given a value: <c>SET</c>, <c>SELECT @x =</c>, <c>OUTPUT</c>, <c>EXEC @rc =</c>, <c>FETCH INTO</c>.</summary>
        public HashSet<string> Assigned { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary><c>DECLARE</c> elements and procedure and function parameters.</summary>
        public List<DeclareVariableElement> Declarations { get; } = new List<DeclareVariableElement>();

        /// <summary><c>DECLARE @t TABLE</c>, and a table-valued function's <c>RETURNS @t TABLE</c>.</summary>
        public List<DeclareTableVariableBody> TableVariables { get; } = new List<DeclareTableVariableBody>();

        public static VariableUse In(TSqlFragment fragment)
        {
            var use = new VariableUse();
            fragment.Accept(use);
            return use;
        }

        public override void Visit(ExecuteParameter node)
        {
            if (node.Variable != null) _parameterNames.Add(node.Variable);
            if (node.IsOutput && node.ParameterValue is VariableReference variable) Assigned.Add(variable.Name);
        }

        public override void Visit(VariableReference node)
        {
            if (!_parameterNames.Contains(node)) References.Add(node);
        }

        public override void Visit(SetVariableStatement node) => Assigned.Add(node.Variable.Name);

        public override void Visit(SelectSetVariable node) => Assigned.Add(node.Variable.Name);

        public override void Visit(AssignmentSetClause node)
        {
            if (node.Variable != null) Assigned.Add(node.Variable.Name);
        }

        public override void Visit(ExecuteSpecification node)
        {
            if (node.Variable != null) Assigned.Add(node.Variable.Name);
        }

        public override void Visit(FetchCursorStatement node)
        {
            foreach (var variable in node.IntoVariables) Assigned.Add(variable.Name);
        }

        public override void Visit(DeclareVariableElement node) => Declarations.Add(node);

        public override void Visit(DeclareTableVariableBody node) => TableVariables.Add(node);
    }
}