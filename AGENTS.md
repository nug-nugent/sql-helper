# SQL Helper: notes for coding agents working in this repo

T-SQL formatter for SSMS 22, Visual Studio and the command line. Read `docs/style-guide.md` (the house style),
`docs/architecture.md` (how the engine works) and `docs/TODO.md` (backlog) before changing anything.

## Commands

```bash
dotnet build SqlHelper.slnx          # also builds src/SqlHelper.VisualStudio/bin/<Config>/net48/SqlHelper.VisualStudio.vsix
dotnet test SqlHelper.slnx
dotnet run --project src/SqlHelper.Cli -- format path/to/script.sql
```

## Rules

- **The spec cases are the source of truth.** `tests/SqlHelper.Formatting.Tests/Cases/name.expected.sql` is written
  by hand and approved by the owner; never generate or "accept" it from formatter output. New or changed formatting
  starts as a spec case the owner reviews, then code. Where the style guide and a case disagree, ask.
- **Formatting may only change whitespace and keyword case** (plus a trailing comment hopping back over a comma).
  Never weaken `RoundTripCheck`; if something legitimately needs to change tokens, discuss it first.
- **No final newlines**: not in formatter output, not in files. `.editorconfig` is set accordingly.
- **Licence is MIT.** This project was rewritten from scratch to leave an AGPL codebase behind; don't copy code from
  Poor Man's T-SQL Formatter or other copyleft sources.
- Indent with tabs in SQL output; C# uses 4 spaces, UTF-8 with BOM.
- Layout decisions belong in `Layouter` / `ExpressionLayout` only; token-level casing and spacing in `TokenRules`.
- **`main` changes only through pull requests**, which must pass the Build workflow; every merge publishes a release.
  Work on a branch and open a PR; don't push to `main`.
- **Never bump the version by hand.** The patch is the commit count (`Directory.Build.targets`); only
  `VersionMajorMinor` in `Directory.Build.props` is edited, and only when the owner asks.

## Gotchas

- When a test fails, compare `Cases/name.received.sql` (written on mismatch, git-ignored) with the expected file.
- Before writing layout code for a construct, dump ScriptDom's tree for an example (fragment types, properties and
  token ranges, via reflection over `TSqlFragment`) to check its shape; many keywords belong to no fragment (see
  `docs/architecture.md`).
- The extension can't be tested headlessly; the owner installs the `.vsix` into SSMS 22 to try it.