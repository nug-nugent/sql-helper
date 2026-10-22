using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlHelper.Formatting.Verification;

namespace SqlHelper.Formatting.Dynamic
{
    /// <summary>
    /// Convert to Dynamic SQL: the selected statements, house-formatted, become an <c>@Sql</c> string run by a
    /// parameterised <c>sys.sp_executesql</c> call. Each variable they use without declaring is passed, typed as it is
    /// declared earlier in the batch.
    /// </summary>
    /// <remarks>
    /// Two checks: the output must be the input with the selection's tokens swapped for the new code, and the string's
    /// value must be the selection's tokens again.
    /// </remarks>
    internal static class ToDynamicConversion
    {
        const string IndentUnit = "\t";

        public static FormatResult Apply(string sql, TSqlScript script, int start, int end)
        {
            var statements = Statements.All(script);
            if (!statements.Any(s => s.StartOffset == start) || !statements.Any(s => DynamicSql.EndOffset(s) == end)
                || statements.Any(s => Straddles(s, start, end)))
                return Refuse(sql, "Select whole statements to convert to dynamic SQL.");

            string selected = sql.Substring(start, end - start);
            var selection = DynamicSql.Parse(selected, out IList<ParseError> errors);
            if (errors.Count > 0) return Refuse(sql, "Select whole statements to convert to dynamic SQL.");
            if (selection.Batches.Count != 1)
                return Refuse(sql, "The selection holds more than one batch (GO), but dynamic SQL runs as a single batch.");

            var inner = VariableUse.In(selection);
            var local = new HashSet<string>(
                inner.Declarations.Select(d => d.VariableName.Value).Concat(inner.TableVariables.Select(t => t.VariableName.Value)),
                StringComparer.OrdinalIgnoreCase);
            var outer = VariableUse.In(DynamicSql.BatchAt(script, start));

            var parameters = new List<string>();
            var arguments = new List<string>();
            // Inputs first, then OUTPUT parameters, each in order of first use.
            var used = inner.References.OrderBy(r => r.StartOffset).Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => inner.Assigned.Contains(name));
            foreach (string name in used)
            {
                if (local.Contains(name)) continue;
                if (outer.TableVariables.Any(t => t.StartOffset < start && Same(t.VariableName.Value, name)))
                    return Refuse(sql, $"{name} is a table variable, which sp_executesql can't take as a parameter.");
                var declaration = outer.Declarations.LastOrDefault(d => d.StartOffset < start && Same(d.VariableName.Value, name));
                if (declaration == null)
                    return Refuse(sql, $"Can't find where {name} is declared, so its type for sp_executesql isn't known.");

                string type = sql.Substring(declaration.DataType.StartOffset, declaration.DataType.FragmentLength);
                bool readOnly = declaration is ProcedureParameter parameter && parameter.Modifier == ParameterModifier.ReadOnly;
                bool isOutput = inner.Assigned.Contains(name);
                parameters.Add(name + " " + type + (readOnly ? " READONLY" : "") + (isOutput ? " OUTPUT" : ""));
                arguments.Add(name + " = " + name + (isOutput ? " OUTPUT" : ""));
            }

            var taken = new HashSet<string>(
                outer.Declarations.Select(d => d.VariableName.Value)
                    .Concat(outer.TableVariables.Select(t => t.VariableName.Value))
                    .Concat(inner.References.Select(r => r.Name)),
                StringComparer.OrdinalIgnoreCase);
            string sqlVariable = "@Sql";
            for (int n = 2; taken.Contains(sqlVariable); n++) sqlVariable = "@Sql" + n;

            var formatted = new SqlFormatter(new FormattingOptions { IndentString = IndentUnit, NewLine = "\n" }).Format(selected);
            if (!formatted.Succeeded) return FormatResult.Failure(sql, formatted.Errors);

            string text = Write(sqlVariable, formatted.Text, parameters, arguments, DynamicSql.IndentOfLine(sql, start), TokenScan.LineBreakIn(sql));
            string output = sql.Substring(0, start) + text + sql.Substring(end);

            var tokens = script.ScriptTokenStream;
            var expected = tokens.Where(t => t.Offset < start)
                .Concat(DynamicSql.Tokens(text))
                .Concat(tokens.Where(t => t.Offset >= end))
                .ToList();
            string problem = RoundTripCheck.FindProblem(expected, output, DynamicSql.CreateParser)
                ?? CheckString(output, sqlVariable, selection.ScriptTokenStream);
            if (problem != null) return Refuse(sql, "SQL Helper left this unchanged because " + problem);

            return FormatResult.Success(output);
        }

        static string Write(string sqlVariable, string statements, List<string> parameters, List<string> arguments, string indent, string lineBreak)
        {
            var text = new StringBuilder();
            text.Append("DECLARE ").Append(sqlVariable).Append(" NVARCHAR(MAX) = N'").Append(lineBreak);
            foreach (string line in statements.Split('\n'))
            {
                if (line.Length > 0) text.Append(indent).Append(line.Replace("'", "''"));
                text.Append(lineBreak);
            }
            text.Append(indent).Append('\'').Append(lineBreak).Append(lineBreak);

            string argumentIndent = lineBreak + indent + IndentUnit;
            text.Append(indent).Append("EXEC sys.sp_executesql").Append(argumentIndent).Append(sqlVariable);
            if (parameters.Count > 0)
            {
                text.Append(argumentIndent).Append(", N'").Append(string.Join(", ", parameters)).Append('\'');
                foreach (string argument in arguments) text.Append(argumentIndent).Append(", ").Append(argument);
            }
            return text.ToString();
        }

        /// <summary>The string the output declares must hold the selected statements' tokens.</summary>
        static string CheckString(string output, string sqlVariable, IList<TSqlParserToken> selectionTokens)
        {
            var script = DynamicSql.Parse(output, out _);
            var declaration = VariableUse.In(script).Declarations.FirstOrDefault(d => d.VariableName.Value == sqlVariable);
            if (!(declaration?.Value is StringLiteral literal)) return $"{sqlVariable} wasn't declared as expected.";
            string problem = RoundTripCheck.FindProblem(selectionTokens, literal.Value, DynamicSql.CreateParser);
            return problem == null ? null : "inside the string, " + problem;
        }

        /// <summary>Whether the statement is cut by the selection: neither inside it nor round it.</summary>
        static bool Straddles(TSqlStatement statement, int start, int end)
        {
            int statementEnd = DynamicSql.EndOffset(statement);
            bool overlaps = statement.StartOffset < end && statementEnd > start;
            bool inside = statement.StartOffset >= start && statementEnd <= end;
            bool around = statement.StartOffset <= start && statementEnd >= end;
            return overlaps && !inside && !around;
        }

        static bool Same(string x, string y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        static FormatResult Refuse(string sql, string reason) => FormatResult.Failure(sql, new[] { reason });
    }
}