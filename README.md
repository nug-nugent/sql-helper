# SQL Helper

A T-SQL formatter for SQL Server Management Studio, Visual Studio and VS Code, with an opinionated house style. It
also adds and updates a standard comment header on procedures, functions and views. Planned next: snippets.

Parsing is done by Microsoft's [ScriptDom](https://github.com/microsoft/SqlScriptDOM), the same T-SQL parser
Microsoft's own tools use. SQL Helper only ever changes whitespace and keyword case, and checks every result
before handing it back: if the formatted SQL wouldn't parse, or its code or comments differ from the input in any
other way, your text is left exactly as it was.

The style is described in [docs/style-guide.md](docs/style-guide.md).

## Layout

| Path | What it is |
| --- | --- |
| `src/SqlHelper.Formatting` | The formatter (`netstandard2.0`, so it loads in both the VS/SSMS host and modern .NET) |
| `src/SqlHelper.Cli` | `sqlhelper format [file]` and `sqlhelper header [file] --initials XX`: read a file or standard input, write to standard output |
| `src/SqlHelper.VisualStudio` | VS 2026 / SSMS 22 extension; builds `SqlHelper.VisualStudio.vsix` |
| `tests/SqlHelper.Formatting.Tests` | Spec tests: each `Cases/*.sql` must format to exactly its `*.expected.sql`; `HeaderCases/` likewise for the header command |
| `docs/` | The [style guide](docs/style-guide.md), [how the formatter works](docs/architecture.md), and the [TODO list](docs/TODO.md) |

## Build and test

Needs the .NET 10 SDK.

```bash
dotnet build SqlHelper.slnx
dotnet test SqlHelper.slnx
```

Format something from the command line:

```bash
dotnet run --project src/SqlHelper.Cli -- format path/to/script.sql
```

## Visual Studio and SSMS extension

Building the solution produces `src/SqlHelper.VisualStudio/bin/<Configuration>/net48/SqlHelper.VisualStudio.vsix`.
Double-click it to install into Visual Studio 2022 / 2026 or SQL Server Management Studio 22 (close them first).

It adds **SQL Helper > Format SQL** (under the Extensions menu, and on the editor's right-click menu), bound to
**Ctrl+Alt+F**. It formats the selection, or the whole document if nothing is selected, as a single undo step. If
the SQL can't be formatted, nothing changes and a message says why.

**SQL Helper > Add Header** (**Ctrl+Alt+H**) adds the house comment header to the procedure, function or view in the
document, or a dated history line to the header it already has (converting an SSMS template header), and leaves the
cursor where you type the change. Set your initials first under **Tools > Options > SQL Helper > General**. The header
format is in the [style guide](docs/style-guide.md#comment-headers).

Add a header from the command line (prints the result; the file isn't changed):

```bash
dotnet run --project src/SqlHelper.Cli -- header path/to/proc.sql --initials DH --note "#12345: What changed"
```

## Command-line exit codes

`0` done, `1` bad arguments or unreadable file, `2` left unchanged (the SQL doesn't parse, `header` found no
procedure, function or view, or the result failed the safety check); the reason goes to standard error and nothing to
standard output.

## Spec tests

Each case in `tests/SqlHelper.Formatting.Tests/Cases` is an input `name.sql` and the hand-written
`name.expected.sql` it must format to, byte for byte apart from line-ending style. A second test formats each expected
file and checks nothing changes. On a mismatch the actual output is written to `name.received.sql` for diffing.

To add a case, write both files. The expected file is the spec, so write it by hand rather than copying the
formatter's output.

## Licence

[MIT](LICENSE.txt)