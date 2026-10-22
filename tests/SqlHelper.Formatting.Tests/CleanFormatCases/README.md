Spec cases for the clean-and-format command (`CleanAndFormat`, tested by `CleanFormatCaseTests`). Each `name.sql`
becomes `name.expected.sql` after Clean Up, Add Semicolons and Format SQL, with an SSMS template header or dash history
block converted as Add Header does, run with the same options as `HeaderCases` (DH, 21/10/2026,
`#12345: Example change`). A house header, or no header, is left as it is.