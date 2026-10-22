# How the formatter works

`SqlFormatter.Format` runs four stages over Microsoft's ScriptDom parser:

1. **Parse** with `TSql170Parser`. Parse errors stop here and the input comes back unchanged with the errors.
2. **Plan** the layout in a `TokenLayout`: one entry per token of ScriptDom's token stream, saying what goes in front
   of it (`Default` space, `NewLine` or `BlankLine`), the indent level of its line, whether spaces around it are
   dropped, and whether it is uppercased.
   - `TokenRules` sets casing and spacing that don't depend on lines: keywords up, names as typed, data types and
     unqualified function names up, brackets hugging function names.
   - `Layouter` (statements, plus `.Queries` and `.Ddl` partials) walks the syntax tree and sets the line breaks and
     indents. This is the only place layout is decided. Its convention: a method laying out a fragment at `level`
     never sets the break before that fragment's first token; the caller does.
   - `ExpressionLayout` is a visitor for the things that can turn up anywhere inside an expression and span lines:
     subqueries, `CASE`, `IN (…)` lists, derived tables.
   - Statements `Layouter` has no rule for are added to `VerbatimRanges` and keep their original whitespace.
3. **Emit**: `Emitter` writes every code token from the original stream, in order, applying the plan. Because the
   output is built only from the original tokens, layout can change whitespace and case but not code. Comments are
   re-attached: on the same line as the previous code they stay trailing; otherwise they go on their own line before
   the next code. A trailing comment after a leading comma moves back in front of the comma.
4. **Verify**: `RoundTripCheck` re-parses the output and compares code tokens and comment tokens (as two separate
   sequences) with the input. Any difference, or any exception during stages 2–3, returns the input unchanged with a
   message, so a layout bug can never cost the user their text or crash the host editor.

## Comment header

`CommentHeader.Apply` is separate from formatting and doesn't use the layout plan. It parses the script, takes the
first procedure, function or view (`ProcedureStatementBody`, `FunctionStatementBody`, `ViewStatementBody`, which
cover `CREATE`, `ALTER` and `CREATE OR ALTER`), and looks at the comment tokens between that statement's first token
and the code before it. The result is one splice of the original text, so nothing outside it moves:

- a house header (`HeaderText.IsHouseHeader`) gets a line inserted before its closing rule;
- an SSMS template header (a run of `--` comments, one per line, between `-- ====` rules, read by
  `HeaderText.ParseSsms`) is replaced by a house header built from it;
- a dash history block (`-- AB - 01/06/2023 - …` lines between `----------` rules, read by
  `HeaderText.ParseDashHistory`) likewise, its lines becoming the history (both found by `FindRuledBlock`);
- otherwise a new header is inserted at the start of the object's line.

`RoundTripCheck.FindCodeProblem` then checks the code tokens (not the comments, which change on purpose) against the
input. `HeaderResult.CaretOffset` tells the editor where to leave the cursor. A `--` comment token doesn't include its
line break; the break is the whitespace token that follows.

## ScriptDom quirks worked around

- An `EXISTS` predicate's fragment starts at its `(`; the `EXISTS` keyword belongs to no fragment
  (`TokenLayout.ConditionStart`).
- `<>`, `>=`, `<=`, `!=` are separate single-character tokens; adjacency is preserved in `TokenRules`.
- Keywords that belong to no fragment and have to be found by scanning: `WHEN … THEN` in `MERGE`, `FOR` before
  `FOR XML`, `PIVOT` / `UNPIVOT`, `END TRY` / `BEGIN CATCH` / `END CATCH`, `RETURNS` and `AS` in functions.
- Many non-reserved keywords (`TRY`, `CATCH`, `RETURNS`, `USING`, `MATCHED`, `NOCOUNT`, `THROW`) arrive as plain
  identifier tokens. An identifier token is a keyword unless an `Identifier` fragment covers it.
- A `BEGIN … END` block's `StatementList` has no token range (-1); use the statements inside it.
- The `PIVOT` aggregate is a plain identifier, not a function call.
- `GO n` (repeat the batch) is SSMS syntax, not T-SQL, and doesn't parse.

## Semicolons

`Semicolons.Add` is also separate from formatting. It walks each batch's statements, descending into the ones that
hold statements (`BEGIN … END`, `TRY … CATCH`, `IF`, `WHILE`, and the bodies of procedures, functions and triggers)
without terminating those blocks themselves, and collects the last token of every other statement, plus the
procedure, function, view or trigger statement itself. A statement's token range already includes its `;` when it has
one (even a leading `;` on the next line), so those are skipped, as is any end followed by `ELSE`. The code token before
a `--##ServerSideFinalSqlEnd` comment gets no `;`, or loses the one it is. The edits are spliced into the original
text, and `RoundTripCheck.FindProblem` compares the output with the input's tokens plus and minus those semicolons.

## Clean Up

`Cleanup.Apply` is also separate from formatting, and is the one command that rewrites code tokens other than `;`.
Each rule produces a `TokenEdit` (`Rewrites/`): replace tokens *first* to *last* with some text, or insert text before
a token, along with the tokens that text parses to. `TokenEdit.Apply` splices the edits into the original text and
builds the expected token list (input tokens with the edited ranges swapped for the edits' tokens), which goes to
`RoundTripCheck.FindProblem`, so the output must be exactly the planned edits and nothing else.

- `StructuralRewrites` finds the join, keyword, `INTO` / `FROM`, `!=` and alias edits. Join keywords belong to no
  fragment: they are the tokens between a `QualifiedJoin`'s two table references. `Alias = expr` is a
  `SelectScalarExpression` whose `ColumnName` comes before its expression; turning it round is one edit spanning both.
- Edits can nest (an alias turned round may hold a subquery with its own joins), so `Cleanup` works in rounds: each
  round makes the smallest edits that don't overlap, re-parses and checks, until a round finds nothing.
- `BracketRewrites` runs last, in its own round. It collects identifiers only from name positions (`SchemaObjectName`
  outside a `DataTypeReference`, regular `ColumnReferenceExpression`s, aliases, column definitions, CTE names and
  columns), because ScriptDom also uses `Identifier` for data types, function names and option words. A date part
  (`DATEADD(day, …)`) is an `IdentifierLiteral`, so it isn't collected either.
- `Names.Write` decides the brackets: bare for a plain identifier that is neither in `Rewrites/BracketedNames.txt` (the
  keyword table of SSMS 22's own parser, `Microsoft.SqlServer.Management.SqlParser`, minus non-reserved words of one
  or two letters) nor read by ScriptDom as a keyword; bracketed otherwise.

## Server-side markers

`ServerSideMarkers.Add` takes the first `ProcedureStatementBody`. Its body is either one `BeginEndBlockStatement`
(markers go inside the `BEGIN`) or the statements after `AS`, and its last statement must be a `SelectStatement`.
Markers already present are found among the comment tokens before the `SELECT` (back to the `BEGIN` / `AS`) and after
it (up to the next code token). Missing ones are inserted at line starts and ends around the `SELECT`, and its `;`
token is removed. As with semicolons, the expected token list (input tokens, minus the `;`, plus the marker comments
at their offsets) goes to `RoundTripCheck.FindProblem`. Marker names and matching are shared with `Semicolons` in
`ServerSide/Marker.cs`; token scanning helpers shared by the splicing commands are in `TokenScan`.

## Dynamic SQL

`DynamicSql.ToDynamic` and `DynamicSql.FromDynamic` take the whole document and a selection (empty means the whole
document) and return the whole document, because declarations outside the selection matter: their types, and the
`DECLARE` / `SET` statements of an `@Sql` variable that goes. `Dynamic/VariableUse` collects a fragment's variable
references (not a named `EXEC` parameter, which belongs to the procedure called), the variables it assigns and its
declarations (procedure parameters are `DeclareVariableElement`s too); `Dynamic/Statements` finds statements and the
statement list each sits in.

- `ToDynamicConversion` requires the selection to start and end on statement boundaries and parse alone as one batch.
  It formats the selection with `SqlFormatter`, doubles its quotes and writes the `DECLARE` and `EXEC`. Two checks:
  the output's tokens are the input's with the selection swapped for the new code, and the `@Sql` literal's value
  has the selection's tokens.
- `FromDynamicConversion` looks at every `ExecuteStatement` inside the selection that sits in a statement list.
  `EXEC (a + b)` holds its pieces as a list, which is folded into one `+` expression. An expression resolves to a
  string when it is literals, `+` and brackets; a variable resolves through its `DECLARE` and the `SET` / `+=`
  statements after it in the same list, which are then removed (no other use of it in the batch is allowed). A call
  that doesn't resolve is skipped; any other problem throws a refusal for the whole command. The parameter list is
  read by parsing `CREATE PROCEDURE p <list> AS RETURN`. Renames are made on the inner script's tokens, and the
  whitespace between them is re-indented (so strings and comments spanning lines keep theirs). The expected tokens
  are the input's with removed statements dropped and each call swapped for its renamed inner tokens.

## Clean and Format

`CleanAndFormat.Apply` chains `Cleanup.Apply`, `Semicolons.Add` and `SqlFormatter.Format`, each on the previous
step's output, and returns the original text with the first failing step's errors. Last comes
`CommentHeader.Convert`, the same planning as `Apply` but only for an SSMS or dash block: a house header, no header
or no object returns the text unchanged with caret -1. It runs after formatting so its caret offset is into the final
text; the formatter leaves the block comment it writes as it is.

## Visual Studio / SSMS extension

`SqlHelper.VisualStudio` is a classic in-process `AsyncPackage` targeting .NET Framework 4.8, because `devenv.exe`
and `ssms.exe` run on .NET Framework; the formatter is `netstandard2.0` so the same assembly loads there and in
.NET 10. The project is SDK-style and gets its VSIX targets from the `Microsoft.VSSDK.BuildTools` package
(`VSSDKBuildToolsAutoSetup`), so `dotnet build` produces the `.vsix` without the VS extension workload.

SSMS 22 and VS 2026 ship their own ScriptDom with the same assembly version (18.0.0.0) as ours, so whichever copy is
already loaded satisfies our reference; `[ProvideBindingPath]` lets the host find ours when none is.

The SSMS 22 install target is `Microsoft.VisualStudio.Ssms` (setup reports the product as
`Microsoft.VisualStudio.Product.Ssms`, version 22.x). The top-level SQL Helper menu is moved under Extensions by the
host.

Settings live in two places, kept in step by the host. VS 2026 and SSMS 22 show them through Unified Settings, from
`registration.json` (registered by `[ProvideSettingsManifest]`); each setting's `migration` block points at the
registry value of the classic `GeneralOptionsPage`, which Visual Studio 2022 and classic-mode Options still show, and
`IsInUnifiedSettings` hides that page where Unified Settings is on. Code reads and writes settings only through the
`Settings` class, which tries Unified Settings first and falls back to the page. A new setting needs a property on
the page, a property on `Settings`, and an entry in `registration.json` whose `migration` path is
`DialogPage\SqlHelper.VisualStudio.GeneralOptionsPage\<Property>`.