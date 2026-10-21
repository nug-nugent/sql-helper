# TODO

Roughly in priority order.

- [ ] **VS Code extension** that runs the `sqlhelper` command-line tool for Format Document / Format Selection.
- [ ] **Comment header**: add or update a standard comment header on procedures, functions and views.
- [ ] **Test harness app**: paste SQL, see the formatted result side by side, and save it as a new spec case
  (`name.sql` + `name.expected.sql`) once the output is right.
- [ ] **Snippets**: not designed yet.
- [ ] **`sqlhelper dump`**: print ScriptDom's tree (fragment types and token ranges) for a script, to help when adding
  layout rules. Could share code with the test harness.
- [ ] **Extension settings page** (Tools > Options) for indent string and the other formatting options.
- [ ] **Cursor declarations**: `DECLARE … CURSOR FOR SELECT …` is currently left as written; lay out its `SELECT`.
- [ ] **`GO n`** (SSMS's repeat-batch syntax) isn't T-SQL, so ScriptDom rejects scripts containing it and they're
  left unformatted. Could strip the count before parsing and restore it after.