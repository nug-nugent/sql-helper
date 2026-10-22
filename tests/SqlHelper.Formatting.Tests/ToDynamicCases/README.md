Spec cases for Convert to Dynamic SQL. Each `name.sql` becomes `name.expected.sql`. The text between `/*[*/` and
`/*]*/` is the selection (the markers are removed before converting); a case without them converts the whole document.
The statements become a house-formatted `@Sql` string, run by a parameterised `sys.sp_executesql` call whose
parameter types come from the variables' declarations elsewhere in the document.