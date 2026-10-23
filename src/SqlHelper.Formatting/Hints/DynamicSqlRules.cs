using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>
    /// SH603, SH610 and SH611: <c>EXEC (…)</c> and <c>sp_executesql</c> calls, whose SQL is followed back through the
    /// <c>DECLARE</c> and <c>SET</c> statements that build it.
    /// </summary>
    internal static class DynamicSqlRules
    {
        /// <summary>What the literal SQL before a pasted piece ends with when the piece is a value, not a name.</summary>
        static readonly string[] ValueContexts = { "=", "<", ">", " LIKE", "IN (", "IN(", " BETWEEN", "TOP (", "TOP(" };

        /// <summary>One piece of the SQL: literal text, or an expression that isn't a literal.</summary>
        sealed class Piece
        {
            public string Text;
            public ScalarExpression Expression;
        }

        public static void Check(HintContext c)
        {
            foreach (var call in c.Tree.All<ExecuteStatement>()) Call(c, call);
        }

        static void Call(HintContext c, ExecuteStatement call)
        {
            var specification = call.ExecuteSpecification;
            if (specification.Variable != null) return;
            var pieces = new List<Piece>();
            bool execString = false;
            switch (specification.ExecutableEntity)
            {
                case ExecutableStringList strings:
                    execString = true;
                    foreach (var part in strings.Strings) Expand(c, call, part, pieces, null, null);
                    break;
                case ExecutableProcedureReference procedure when IsExecuteSql(procedure):
                    var statement = procedure.Parameters.FirstOrDefault(p => p.Variable == null || Same(p.Variable.Name, "@stmt"));
                    if (statement?.ParameterValue == null) return;
                    Expand(c, call, statement.ParameterValue, pieces, null, null);
                    break;
                default:
                    return;
            }

            if (pieces.All(p => p.Expression == null))
            {
                if (specification.LinkedServer == null && CanRunDirectly(c, call, string.Concat(pieces.Select(p => p.Text))))
                    c.Suggestion("SH610", call, "The SQL run here is a fixed string, so running it as dynamic SQL only adds a separate compile and scope, and hides it from tools that read the code. Run the statements directly (SQL Helper > Convert from Dynamic SQL does this).");
                return;
            }

            if (execString)
                c.Warning("SH603", specification, "EXEC (...) runs SQL text that can't take parameters, so values are pasted into it: a new plan for every value, and a way in for SQL injection. Use sp_executesql with parameters.");
            string before = "";
            foreach (var piece in pieces)
            {
                if (piece.Expression == null)
                {
                    before += piece.Text;
                    continue;
                }
                if (IsValue(before, piece.Expression))
                    c.Warning("SH611", piece.Expression, "This pastes a value into the dynamic SQL: a new plan for every value, and a way in for SQL injection. Pass it as an sp_executesql parameter instead: a @Parameter in the SQL, declared in the parameter list and passed after it.");
                // What the piece adds isn't known; assume a name or number, with no quotes.
                before += "x";
            }
        }

        static bool IsExecuteSql(ExecutableProcedureReference procedure)
        {
            var name = procedure.ProcedureReference?.ProcedureReference?.Name;
            return name != null && name.ServerIdentifier == null && name.DatabaseIdentifier == null
                && HintContext.NameIs(name.BaseIdentifier, "sp_executesql")
                && (name.SchemaIdentifier == null || HintContext.NameIs(name.SchemaIdentifier, "sys"));
        }

        /// <summary>
        /// Adds the pieces an expression makes. A variable is followed to its <c>DECLARE</c> and the <c>SET</c>s after it
        /// in the call's own statement list, as long as nothing else in the batch assigns it; inside one of those,
        /// <paramref name="self"/> stands for the pieces so far, <paramref name="current"/>.
        /// </summary>
        static void Expand(HintContext c, ExecuteStatement call, ScalarExpression expression, List<Piece> pieces, string self, List<Piece> current)
        {
            switch (expression)
            {
                case StringLiteral literal:
                    pieces.Add(new Piece { Text = literal.Value });
                    return;
                case ParenthesisExpression parenthesis:
                    Expand(c, call, parenthesis.Expression, pieces, self, current);
                    return;
                case BinaryExpression binary when binary.BinaryExpressionType == BinaryExpressionType.Add:
                    Expand(c, call, binary.FirstExpression, pieces, self, current);
                    Expand(c, call, binary.SecondExpression, pieces, self, current);
                    return;
                case VariableReference variable when self != null && string.Equals(variable.Name, self, StringComparison.OrdinalIgnoreCase):
                    if (current == null) pieces.Add(new Piece { Expression = variable });
                    else pieces.AddRange(current);
                    return;
                case VariableReference variable when self == null:
                    var value = Resolve(c, call, variable.Name);
                    if (value == null) pieces.Add(new Piece { Expression = variable });
                    else pieces.AddRange(value);
                    return;
                default:
                    pieces.Add(new Piece { Expression = expression });
                    return;
            }
        }

        static List<Piece> Resolve(HintContext c, ExecuteStatement call, string name)
        {
            var parent = c.Tree.Parent(call);
            var list = parent is StatementList statements ? statements.Statements : (parent as TSqlBatch)?.Statements;
            if (list == null) return null;
            int callIndex = list.IndexOf(call);
            int declareIndex = -1;
            for (int i = 0; i < callIndex; i++)
                if (list[i] is DeclareVariableStatement d && d.Declarations.Any(x => HintContext.NameIs(x.VariableName, name)))
                    declareIndex = i;
            if (declareIndex < 0) return null;

            var used = new List<TSqlFragment>();
            var element = ((DeclareVariableStatement)list[declareIndex]).Declarations.First(x => HintContext.NameIs(x.VariableName, name));
            used.Add(element);
            List<Piece> value = null;
            if (element.Value != null)
            {
                value = new List<Piece>();
                Expand(c, call, element.Value, value, name, null);
            }
            for (int i = declareIndex + 1; i < callIndex; i++)
            {
                if (!(list[i] is SetVariableStatement set) || !string.Equals(set.Variable.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                var piece = new List<Piece>();
                Expand(c, call, set.Expression, piece, name, value);
                if (set.AssignmentKind == AssignmentKind.Equals) value = piece;
                else if (set.AssignmentKind == AssignmentKind.AddEquals && value != null) value = value.Concat(piece).ToList();
                else return null;
                used.Add(set);
            }
            if (value == null) return null;

            // Assigned anywhere else (under an IF, by a SELECT, as an OUTPUT parameter): the SQL depends on the path taken.
            var batch = (TSqlFragment)c.Tree.Ancestor<TSqlBatch>(call) ?? c.Tree.Script;
            bool elsewhere = c.Tree.Descendants(batch).Any(f => !used.Contains(f) && Assigns(f, name));
            return elsewhere ? null : value;
        }

        static bool Assigns(TSqlFragment fragment, string name)
        {
            switch (fragment)
            {
                case SetVariableStatement set: return Same(set.Variable.Name, name);
                case SelectSetVariable select: return Same(select.Variable.Name, name);
                case DeclareVariableElement declare: return Same(declare.VariableName.Value, name);
                case ExecuteParameter parameter: return parameter.IsOutput && parameter.ParameterValue is VariableReference v && Same(v.Name, name);
                default: return false;
            }
        }

        static bool Same(string x, string y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Whether fixed SQL could be run as it is, in place of the call. Not when it doesn't parse (it may be for a
        /// newer server), when it must start a batch, uses another database, or keeps a temp table or SET option to
        /// itself, nor when the batch changed a table earlier (deferred name resolution).
        /// </summary>
        static bool CanRunDirectly(HintContext c, ExecuteStatement call, string sql)
        {
            TSqlScript inner;
            using (var reader = new StringReader(sql))
            {
                inner = (TSqlScript)new TSql170Parser(initialQuotedIdentifiers: true).Parse(reader, out IList<ParseError> errors);
                if (errors.Count > 0) return false;
            }
            bool needsItsOwnScope = new SyntaxIndex(inner).All<TSqlFragment>().Any(f =>
                f is ProcedureStatementBody || f is FunctionStatementBody || f is ViewStatementBody || f is TriggerStatementBody
                || f is CreateSchemaStatement || f is UseStatement || f is ExecuteAsStatement
                || f is SetOnOffStatement || f is SetTransactionIsolationLevelStatement || f is SetCommandStatement
                || (f is CreateTableStatement create && IsTemp(create.SchemaObjectName))
                || (f is SelectStatement select && IsTemp(select.Into)));
            if (needsItsOwnScope) return false;

            var batch = (TSqlFragment)c.Tree.Ancestor<TSqlBatch>(call) ?? c.Tree.Script;
            return !c.Tree.Descendants(batch).OfType<TSqlStatement>().Any(s => s.StartOffset < call.StartOffset
                && (s is AlterTableStatement || (s is CreateTableStatement create && !IsTemp(create.SchemaObjectName))));
        }

        static bool IsTemp(SchemaObjectName name) => name?.BaseIdentifier != null && name.BaseIdentifier.Value.StartsWith("#");

        /// <summary>
        /// Whether a pasted piece is a value: the SQL before it leaves a string open, or ends with a comparison,
        /// <c>LIKE</c>, <c>IN (</c>, <c>BETWEEN</c> or <c>TOP (</c>. <c>QUOTENAME(x)</c> makes a name.
        /// </summary>
        static bool IsValue(string before, ScalarExpression piece)
        {
            if (HintContext.Strip(piece) is FunctionCall call && call.CallTarget == null && HintContext.NameIs(call.FunctionName, "QUOTENAME")
                && !(call.Parameters.Count == 2 && call.Parameters[1] is StringLiteral quote && quote.Value == "'"))
                return false;
            if (before.Count(ch => ch == '\'') % 2 == 1) return true;
            string end = before.TrimEnd().ToUpperInvariant();
            return ValueContexts.Any(context => end.EndsWith(context, StringComparison.Ordinal));
        }
    }
}