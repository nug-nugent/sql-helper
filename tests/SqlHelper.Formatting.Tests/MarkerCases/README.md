Spec cases for the server-side markers command. Each `name.sql` becomes `name.expected.sql`; only marker comments
(each on its own line) are added, and the final `SELECT` loses its `;`. Nothing else in the text moves. Scripts the command
refuses (no procedure, or a procedure whose last statement isn't a `SELECT`) are tested in `MarkerTests`.