Spec cases for the comment header command (`CommentHeader`, tested by `HeaderCaseTests`). Each `name.sql` becomes
`name.expected.sql` when the command is run with today = 21/10/2026, initials = DH and the note
`#12345: Example change`. With no history to add to (no header, or an SSMS template never filled in) the line written
is `Created` instead.