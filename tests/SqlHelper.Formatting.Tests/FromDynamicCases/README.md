Spec cases for Convert from Dynamic SQL. Each `name.sql` becomes `name.expected.sql`. The text between `/*[*/` and
`/*]*/` is the selection (the markers are removed before converting); a case without them converts the whole document.
`sp_executesql` calls and `EXEC (…)` whose SQL is a literal, or a variable built only from literals, are replaced by
the statements they run; the variable's `DECLARE` / `SET` statements go with them.