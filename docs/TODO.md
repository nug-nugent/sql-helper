# TODO

Roughly in priority order.

- [ ] **VS Code extension** that runs the `sqlhelper` command-line tool for Format Document / Format Selection.
- [ ] **Test harness app**: paste SQL, see the formatted result side by side, and save it as a new spec case
  (`name.sql` + `name.expected.sql`) once the output is right.
- [ ] **Snippets**: not designed yet.
- [ ] **`sqlhelper dump`**: print ScriptDom's tree (fragment types and token ranges) for a script, to help when adding
  layout rules. Could share code with the test harness.
- [ ] **Formatting settings**: add indent string and the other formatting options to the extension's Tools > Options
  page (SQL Helper > General, which so far only has your initials for comment headers).
- [ ] **Cursor declarations**: `DECLARE … CURSOR FOR SELECT …` is currently left as written; lay out its `SELECT`.
- [ ] **`GO n`** (SSMS's repeat-batch syntax) isn't T-SQL, so ScriptDom rejects scripts containing it and they're
  left unformatted. Could strip the count before parsing and restore it after.
- [ ] **Automatic updates**: have the `.vsix` register its own extension gallery (a `.pkgdef` writing
  `ExtensionManager\Repositories\{guid}` with `"Protocol"="Atom Feed"`) and have the release workflow publish an Atom
  feed (e.g. on GitHub Pages) pointing at the latest `.vsix`, so SSMS and VS pick up new releases after the first
  manual install. Works whether or not SSMS checks the VS Marketplace.