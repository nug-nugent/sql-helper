using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Verification;

namespace SqlHelper.Formatting.Dynamic
{
    /// <summary>
    /// Convert from Dynamic SQL: each <c>sp_executesql</c> or <c>EXEC (…)</c> call whose SQL is built only from string
    /// literals is replaced by the statements it runs, its parameters renamed to the variables passed for them. The
    /// <c>DECLARE</c> and <c>SET</c> statements that built the SQL are removed.
    /// </summary>
    /// <remarks>
    /// A call whose SQL is built from anything else is really dynamic and is skipped. Anything else that stops a call
    /// being converted (a literal argument, a parameter not passed, a clashing declaration) refuses the whole command.
    /// The output must be the input's tokens with the removed statements dropped and each call swapped for the tokens
    /// of the SQL it ran, renames made.
    /// </remarks>
    internal static class FromDynamicConversion
    {
        /// <summary>Replace the text <see cref="Start"/> to <see cref="End"/>, which holds tokens <see cref="FirstToken"/> to <see cref="LastToken"/>.</summary>
        sealed class Splice
        {
            public int Start, End, FirstToken, LastToken;
            public string Text = "";
            public List<TSqlParserToken> Tokens = new List<TSqlParserToken>();
        }

        sealed class RefusalException : Exception
        {
            public RefusalException(string message) : base(message) { }
        }

        /// <summary>One call being converted: where it is, and what its SQL is built from.</summary>
        sealed class Call
        {
            public string Sql;
            public IList<TSqlParserToken> Tokens;
            public TSqlBatch Batch;
            public ExecuteStatement Statement;
            public IList<TSqlStatement> List;
            public readonly List<TSqlStatement> Removed = new List<TSqlStatement>();
            public readonly List<string> Variables = new List<string>();
        }

        public static FormatResult Apply(string sql, TSqlScript script, int start, int end)
        {
            var containers = Statements.Containers(script);
            var calls = Statements.All(script).OfType<ExecuteStatement>()
                .Where(e => e.StartOffset >= start && DynamicSql.EndOffset(e) <= end)
                .OrderBy(e => e.StartOffset);

            var splices = new List<Splice>();
            var skipped = new List<string>();
            try
            {
                foreach (var statement in calls)
                {
                    containers.TryGetValue(statement, out IList<TSqlStatement> list);
                    var call = new Call
                    {
                        Sql = sql,
                        Tokens = script.ScriptTokenStream,
                        Batch = DynamicSql.BatchAt(script, statement.StartOffset),
                        Statement = statement,
                        List = list,
                    };
                    string skip = Convert(call, splices);
                    if (skip != null) skipped.Add($"The call on line {statement.StartLine} {skip}, so it was left alone.");
                }
            }
            catch (RefusalException refusal)
            {
                return FormatResult.Failure(sql, new[] { refusal.Message });
            }

            if (splices.Count == 0)
                return FormatResult.Failure(sql, skipped.Count > 0 ? skipped : new List<string> { "There's no sp_executesql or EXEC (…) call here to convert." });

            splices = splices.OrderBy(s => s.Start).ToList();
            for (int i = 1; i < splices.Count; i++)
                if (splices[i].Start < splices[i - 1].End)
                    return FormatResult.Failure(sql, new[] { "Two of these calls share the statements that build their SQL, so SQL Helper left this unchanged." });

            var output = new StringBuilder(sql.Length);
            var expected = new List<TSqlParserToken>();
            int position = 0;
            int next = 0;
            var tokens = script.ScriptTokenStream;
            foreach (var splice in splices)
            {
                output.Append(sql, position, splice.Start - position).Append(splice.Text);
                position = splice.End;
                for (; next < splice.FirstToken; next++) expected.Add(tokens[next]);
                expected.AddRange(splice.Tokens);
                next = splice.LastToken + 1;
            }
            output.Append(sql, position, sql.Length - position);
            for (; next < tokens.Count; next++) expected.Add(tokens[next]);

            string problem = RoundTripCheck.FindProblem(expected, output.ToString(), DynamicSql.CreateParser);
            if (problem != null)
                return FormatResult.Failure(sql, new[] { "SQL Helper left this unchanged because " + problem });

            return FormatResult.Success(output.ToString());
        }

        /// <summary>Adds the call's splices, or says why it is skipped; null for a call that isn't dynamic SQL.</summary>
        static string Convert(Call call, List<Splice> splices)
        {
            var specification = call.Statement.ExecuteSpecification;
            if (specification.Variable != null || specification.LinkedServer != null || specification.ExecuteContext != null)
                return null;

            ScalarExpression statementArgument = null;
            ScalarExpression parametersArgument = null;
            var arguments = new List<ExecuteParameter>();
            switch (specification.ExecutableEntity)
            {
                case ExecutableStringList strings:
                    if (strings.Parameters.Count > 0) return null;
                    statementArgument = Concatenate(strings.Strings);
                    break;
                case ExecutableProcedureReference procedure when IsExecuteSql(procedure):
                    int positional = 0;
                    foreach (var parameter in procedure.Parameters)
                    {
                        string name = parameter.Variable?.Name;
                        if (name == null && positional < 2)
                        {
                            if (positional++ == 0) statementArgument = parameter.ParameterValue;
                            else parametersArgument = parameter.ParameterValue;
                        }
                        else if (Same(name, "@stmt")) statementArgument = parameter.ParameterValue;
                        else if (Same(name, "@params")) parametersArgument = parameter.ParameterValue;
                        else arguments.Add(parameter);
                    }
                    if (statementArgument == null) return null;
                    break;
                default:
                    return null;
            }

            if (call.List == null) return "is the whole body of an IF, ELSE or WHILE (put it inside BEGIN … END)";

            string text = Resolve(call, statementArgument, null, null);
            if (text == null) return "builds its SQL from more than string literals";
            string parameterList = parametersArgument == null ? "" : Resolve(call, parametersArgument, null, null);
            if (parameterList == null) return "builds its parameter list from more than string literals";

            CheckOtherUses(call);
            var renames = MatchArguments(call, parameterList, arguments);
            var (body, bodyTokens) = Body(call, text, renames);

            var semicolon = call.Tokens[call.Statement.LastTokenIndex];
            if (semicolon.TokenType == TSqlTokenType.Semicolon
                && bodyTokens.LastOrDefault(TokenScan.IsCode)?.TokenType != TSqlTokenType.Semicolon)
            {
                body += ";";
                bodyTokens.Add(semicolon);
            }

            splices.Add(new Splice
            {
                Start = call.Statement.StartOffset,
                End = DynamicSql.EndOffset(call.Statement),
                FirstToken = call.Statement.FirstTokenIndex,
                LastToken = call.Statement.LastTokenIndex,
                Text = body,
                Tokens = bodyTokens,
            });
            foreach (var removed in call.Removed) splices.Add(Removal(call.Sql, removed));
            return null;
        }

        static bool IsExecuteSql(ExecutableProcedureReference procedure)
        {
            var name = procedure.ProcedureReference?.ProcedureReference?.Name;
            return name != null && name.ServerIdentifier == null && name.DatabaseIdentifier == null
                && Same(name.BaseIdentifier.Value, "sp_executesql")
                && (name.SchemaIdentifier == null || Same(name.SchemaIdentifier.Value, "sys"));
        }

        /// <summary><c>EXEC (a + b)</c> holds its pieces as a list; as one expression they resolve like any other.</summary>
        static ScalarExpression Concatenate(IList<ValueExpression> pieces)
        {
            ScalarExpression result = pieces[0];
            for (int i = 1; i < pieces.Count; i++)
                result = new BinaryExpression { BinaryExpressionType = BinaryExpressionType.Add, FirstExpression = result, SecondExpression = pieces[i] };
            return result;
        }

        /// <summary>
        /// The string the expression makes, or null if it isn't built only from literals. Inside a <c>SET</c> of
        /// <paramref name="self"/>, the variable stands for its value so far, <paramref name="current"/>.
        /// </summary>
        static string Resolve(Call call, ScalarExpression expression, string self, string current)
        {
            switch (expression)
            {
                case StringLiteral literal:
                    return literal.Value;
                case ParenthesisExpression parenthesis:
                    return Resolve(call, parenthesis.Expression, self, current);
                case BinaryExpression binary when binary.BinaryExpressionType == BinaryExpressionType.Add:
                    string first = Resolve(call, binary.FirstExpression, self, current);
                    string second = first == null ? null : Resolve(call, binary.SecondExpression, self, current);
                    return second == null ? null : first + second;
                case VariableReference variable when self != null:
                    return Same(variable.Name, self) ? current : null;
                case VariableReference variable:
                    return ResolveVariable(call, variable.Name);
                default:
                    return null;
            }
        }

        /// <summary>
        /// The value a variable has at the call, from its <c>DECLARE</c> and the <c>SET</c> statements after it in the
        /// call's statement list, which are marked for removal.
        /// </summary>
        static string ResolveVariable(Call call, string name)
        {
            int callIndex = call.List.IndexOf(call.Statement);
            int declareIndex = -1;
            for (int i = 0; i < callIndex; i++)
                if (call.List[i] is DeclareVariableStatement d && d.Declarations.Any(x => Same(x.VariableName.Value, name)))
                    declareIndex = i;
            if (declareIndex < 0) return null;

            var declare = (DeclareVariableStatement)call.List[declareIndex];
            var element = declare.Declarations.First(x => Same(x.VariableName.Value, name));
            string value = element.Value == null ? null : Resolve(call, element.Value, name, null);
            if (element.Value != null && value == null) return null;

            var sets = new List<TSqlStatement>();
            for (int i = declareIndex + 1; i < callIndex; i++)
            {
                if (!(call.List[i] is SetVariableStatement set) || !Same(set.Variable.Name, name)) continue;
                string piece = Resolve(call, set.Expression, name, value);
                if (piece == null) return null;
                if (set.AssignmentKind == AssignmentKind.Equals) value = piece;
                else if (set.AssignmentKind == AssignmentKind.AddEquals && value != null) value += piece;
                else return null;
                sets.Add(set);
            }
            if (value == null) return null;

            if (declare.Declarations.Count > 1)
                throw new RefusalException($"{name} is declared together with other variables on line {declare.StartLine}; give it a DECLARE of its own first.");
            call.Removed.Add(declare);
            call.Removed.AddRange(sets);
            call.Variables.Add(name);
            return value;
        }

        /// <summary>A variable that builds the SQL is removed, so nothing else may use it.</summary>
        static void CheckOtherUses(Call call)
        {
            foreach (string name in call.Variables)
            {
                int inCall = 0;
                for (int i = call.Batch.FirstTokenIndex; i <= call.Batch.LastTokenIndex; i++)
                {
                    var token = call.Tokens[i];
                    if (token.TokenType != TSqlTokenType.Variable || !Same(token.Text, name)) continue;
                    if (call.Removed.Any(s => s.FirstTokenIndex <= i && i <= s.LastTokenIndex)) continue;
                    if (call.Statement.FirstTokenIndex <= i && i <= call.Statement.LastTokenIndex && inCall++ == 0) continue;
                    throw new RefusalException($"{name} is also used on line {token.Line}, so SQL Helper can't remove it. Take that use out first.");
                }
            }
        }

        /// <summary>Each parameter in the list mapped to the variable passed for it.</summary>
        static Dictionary<string, string> MatchArguments(Call call, string parameterList, List<ExecuteParameter> arguments)
        {
            var parameters = new List<ProcedureParameter>();
            if (parameterList.Trim().Length > 0)
            {
                var script = DynamicSql.Parse("CREATE PROCEDURE p " + parameterList + " AS RETURN", out IList<ParseError> errors);
                var procedure = script.Batches.SelectMany(b => b.Statements).OfType<ProcedureStatementBody>().FirstOrDefault();
                if (errors.Count > 0 || procedure == null)
                    throw new RefusalException($"SQL Helper can't read the parameter list of the call on line {call.Statement.StartLine}.");
                parameters.AddRange(procedure.Parameters);
            }

            var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int position = 0;
            foreach (var argument in arguments)
            {
                var parameter = argument.Variable == null
                    ? (position < parameters.Count ? parameters[position++] : null)
                    : parameters.FirstOrDefault(p => Same(p.VariableName.Value, argument.Variable.Name));
                if (parameter == null)
                    throw new RefusalException($"An argument of the call on line {argument.StartLine} isn't in its parameter list.");
                string name = parameter.VariableName.Value;
                if (!(argument.ParameterValue is VariableReference variable))
                    throw new RefusalException($"{name} is passed a value rather than a variable on line {argument.StartLine}; declare a variable for it first.");
                renames[name] = variable.Name;
            }

            var missing = parameters.FirstOrDefault(p => !renames.ContainsKey(p.VariableName.Value));
            if (missing != null)
                throw new RefusalException($"{missing.VariableName.Value} isn't passed to the call on line {call.Statement.StartLine}.");
            return renames;
        }

        /// <summary>The SQL the call runs, renamed and moved to the call's indent, and the tokens it must parse to.</summary>
        static (string Text, List<TSqlParserToken> Tokens) Body(Call call, string text, Dictionary<string, string> renames)
        {
            int line = call.Statement.StartLine;
            var script = DynamicSql.Parse(text, out IList<ParseError> errors);
            if (errors.Count > 0)
                throw new RefusalException($"The SQL run on line {line} doesn't parse ({errors[0].Message}).");
            if (script.Batches.Count != 1)
                throw new RefusalException($"The SQL run on line {line} isn't a single batch.");

            var inner = VariableUse.In(script);
            var declaredInside = new HashSet<string>(
                inner.Declarations.Select(d => d.VariableName.Value).Concat(inner.TableVariables.Select(t => t.VariableName.Value)),
                StringComparer.OrdinalIgnoreCase);
            var stray = inner.References.FirstOrDefault(r => !renames.ContainsKey(r.Name) && !declaredInside.Contains(r.Name));
            if (stray != null)
                throw new RefusalException($"{stray.Name} is used in the SQL run on line {line} but isn't one of its parameters.");

            var outer = VariableUse.In(call.Batch);
            var clash = outer.Declarations.Select(d => d.VariableName.Value).Concat(outer.TableVariables.Select(t => t.VariableName.Value))
                .FirstOrDefault(declaredInside.Contains);
            if (clash != null)
                throw new RefusalException($"{clash} is declared both in the SQL run on line {line} and outside it.");

            var renamed = new Dictionary<int, string>();
            foreach (var reference in inner.References)
                if (renames.TryGetValue(reference.Name, out string to) && to != reference.Name)
                    renamed[reference.FirstTokenIndex] = to;

            var tokens = new List<TSqlParserToken>();
            var source = script.ScriptTokenStream;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].TokenType == TSqlTokenType.EndOfFile) continue;
                tokens.Add(renamed.TryGetValue(i, out string to) ? new TSqlParserToken(source[i].TokenType, to) : source[i]);
            }

            return (Reindent(tokens, DynamicSql.IndentOfLine(call.Sql, call.Statement.StartOffset), TokenScan.LineBreakIn(call.Sql)), tokens);
        }

        /// <summary>
        /// The tokens as text with the blank lines round them dropped and their common indent replaced by
        /// <paramref name="indent"/> (except on the first line, which starts where the call did). Only whitespace between
        /// tokens changes, so a string or comment spanning lines keeps its own.
        /// </summary>
        static string Reindent(List<TSqlParserToken> tokens, string indent, string lineBreak)
        {
            // Runs of whitespace between non-whitespace tokens: pieces[0], token, pieces[1], token, … pieces[n].
            var code = new List<string>();
            var gaps = new List<string> { "" };
            foreach (var token in tokens)
            {
                if (token.TokenType == TSqlTokenType.WhiteSpace) gaps[gaps.Count - 1] += token.Text;
                else
                {
                    code.Add(token.Text);
                    gaps.Add("");
                }
            }

            string LineIndent(string gap) => gap.Substring(gap.LastIndexOf('\n') + 1);
            var indents = new List<string> { LineIndent(gaps[0]) };
            for (int i = 1; i < code.Count; i++)
                if (gaps[i].IndexOf('\n') >= 0) indents.Add(LineIndent(gaps[i]));
            string common = indents.Aggregate((a, b) =>
            {
                int n = 0;
                while (n < a.Length && n < b.Length && a[n] == b[n]) n++;
                return a.Substring(0, n);
            });

            var text = new StringBuilder();
            for (int i = 0; i < code.Count; i++)
            {
                if (i > 0)
                {
                    string[] lines = gaps[i].Replace("\r\n", "\n").Split('\n');
                    if (lines.Length == 1) text.Append(lines[0]);
                    else
                    {
                        for (int l = 0; l < lines.Length - 1; l++)
                            text.Append(l == 0 ? lines[0] : "").Append(lineBreak);
                        text.Append(indent).Append(lines[lines.Length - 1].Substring(common.Length));
                    }
                }
                text.Append(code[i]);
            }
            return text.ToString();
        }

        /// <summary>
        /// A statement's removal: from the start of its line (when only indent comes before it) to the start of the next
        /// line with anything on it, so the blank line after it goes too.
        /// </summary>
        static Splice Removal(string sql, TSqlStatement statement)
        {
            int start = statement.StartOffset;
            int end = DynamicSql.EndOffset(statement);

            int after = SkipSpaces(sql, end);
            if (after < sql.Length && sql[after] != '\r' && sql[after] != '\n')
                return new Splice { Start = start, End = after, FirstToken = statement.FirstTokenIndex, LastToken = statement.LastTokenIndex };

            // The statement ends its line: take the line break, and any blank lines after it.
            end = after;
            while (end < sql.Length)
            {
                int nextLine = sql.IndexOf('\n', end);
                nextLine = nextLine < 0 ? sql.Length : nextLine + 1;
                end = nextLine;
                int content = SkipSpaces(sql, end);
                if (content < sql.Length && sql[content] != '\r' && sql[content] != '\n') break;
            }

            int lineStart = start == 0 ? 0 : sql.LastIndexOf('\n', start - 1) + 1;
            if (SkipSpaces(sql, lineStart) == start) start = lineStart;
            return new Splice { Start = start, End = end, FirstToken = statement.FirstTokenIndex, LastToken = statement.LastTokenIndex };
        }

        static int SkipSpaces(string sql, int offset)
        {
            while (offset < sql.Length && (sql[offset] == ' ' || sql[offset] == '\t')) offset++;
            return offset;
        }

        static bool Same(string x, string y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }
}