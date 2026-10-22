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

Download `SqlHelper.VisualStudio.vsix` from the [latest release](https://github.com/nug-nugent/sql-helper/releases/latest),
or build it yourself (`src/SqlHelper.VisualStudio/bin/<Configuration>/net48/SqlHelper.VisualStudio.vsix`).
Double-click it to install into Visual Studio 2022 / 2026 or SQL Server Management Studio 22 (close them first).

To have Visual Studio keep it up to date, add the release feed under **Tools > Options > Environment > Extensions >
Additional Extension Galleries**: `https://github.com/nug-nugent/sql-helper/releases/latest/download/atom.xml`. Each new
release is then offered under **Extensions > Manage Extensions > Updates**. SSMS 22 shows the same setting but doesn't
appear to check galleries for updates, so update it by running the new `.vsix`.

It adds **SQL Helper > Format SQL** (under the Extensions menu, and on the query editor's right-click menu), bound to
**Ctrl+Alt+F**. It formats the selection, or the whole document if nothing is selected, as a single undo step. If
the SQL can't be formatted, nothing changes and a message says why.

**SQL Helper > Add Header** (**Ctrl+Alt+H**) adds the house comment header to the procedure, function or view in the
document, or a dated history line to the header it already has (converting an SSMS template header), and leaves the
cursor where you type the change. Your initials are taken from your Windows account name the first time (David Huggett
gives DH); check or change them under **Tools > Options > SQL Helper > General**. The header format is in the
[style guide](docs/style-guide.md#comment-headers).

### Updating from a local gallery

Every build also copies the `.vsix` into `gallery\` at the repository root and writes a feed for it,
`gallery\atom.xml` (by `build\Update-Gallery.ps1`; the folder isn't committed). Add the feed once in Visual Studio
under **Tools > Options > Environment > Extensions > Additional Extension Galleries**, with the URL
`file:///C:/Dev/sql-helper/gallery/atom.xml`, for trying local builds. Visual Studio offers a build as an update when
its version is higher than the installed one; a new commit raises it (see [Versions](#versions)).

If an install finishes with "a post-installation step failed" and the extension then doesn't appear, close Visual
Studio and run `devenv.exe /updateconfiguration` once.

### Comment headers from the command line

Add a header from the command line (prints the result; the file isn't changed):

```bash
dotnet run --project src/SqlHelper.Cli -- header path/to/proc.sql --initials DH --note "#12345: What changed"
```

## Pull requests and releases

`main` only changes through pull requests, and a pull request can only merge once the **Build** workflow
(`.github/workflows/build.yml`) has built it and passed the tests on Windows. Each run keeps the `.vsix` as a
download, to try the change before merging.

Merging doesn't release anything. When `main` is ready to ship, run the **Release** workflow
(`.github/workflows/release.yml`) by hand: **Actions > Release > Run workflow**, on `main`. It builds and tests again
and publishes a GitHub release tagged `v<version>` with the `.vsix`, the command-line tool (`sqlhelper-<version>.zip`,
needs the .NET 10 runtime) and the `atom.xml` feed for Visual Studio. Its notes list the pull requests merged since
the last release. It refuses to run if nothing has been merged since then.

### Versions

Versions are `<major>.<minor>.<patch>`. Major and minor are `VersionMajorMinor` in `Directory.Build.props`; change them
there by hand. The patch is the number of commits on the branch being built, so every merge to `main` produces a
higher one; it is never set by hand, and releases skip the numbers of merges that weren't released. Local builds work
it out the same way from git, and Debug builds of the `.vsix` add a fourth part that rises every minute, so a build
with uncommitted changes still installs over the last one.

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