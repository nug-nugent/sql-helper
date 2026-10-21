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