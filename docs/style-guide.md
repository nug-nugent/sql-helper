# SQL Helper house style

This is the formatting spec. The test cases in `tests/SqlHelper.Formatting.Tests/Cases` are the worked examples:
where this guide and a case disagree, one of them is a bug.

## Basics

- **Indentation:** one tab per level. Nothing is aligned with spaces.
- **Line width:** no wrapping. Lines are as long as their content.
- **No final newline:** the output ends with the last character of its last line. No trailing whitespace anywhere.
- **Case:** keywords, data types, built-in functions (`COUNT`, `LEFT`, `ROW_NUMBER`) and system functions/variables
  (`@@TRANCOUNT`, `ERROR_MESSAGE()`) are UPPERCASE. Everything else keeps the case it was typed in: table, column and
  alias names (including ones that happen to be keywords, such as `Name` or `Date`), variables, and
  schema-qualified function calls (`dbo.fn_Thing()`), which are always user-defined.
- **What's never changed:** the tokens themselves. No keywords added or removed (`JOIN` stays `JOIN`, `INNER JOIN` stays
  `INNER JOIN`, `AS` before an alias appears only if you wrote it), no semicolons added or removed, no brackets added or
  removed, and string literals and comments are kept exactly. Formatting only changes whitespace and keyword case, plus
  one deliberate move: a trailing comment can hop back over a comma (see Comments). The formatter checks this on every
  run, comparing code and comments separately; if the check fails it refuses and leaves the text alone.
- **Spacing:** one space around operators (`a = 1`, `x + 1`), after commas inside a single line (`LEFT(Name, 3)`),
  none inside brackets, none before a function's opening bracket.

## Statements and blank lines

- Statements are separated by one blank line.
- **Exception:** consecutive single-line `DECLARE` statements are not separated, nor are consecutive single-line `SET`
  statements or consecutive single-line `PRINT` statements. A statement that spans several lines (a multi-variable
  `DECLARE`, a table variable) always gets a blank line on each side.
- No blank line directly after a block opener (`BEGIN`, `BEGIN TRY`, `BEGIN CATCH`, `BEGIN TRAN`, `AS` of a procedure)
  or directly before a block closer (`END`, `END TRY`, `END CATCH`, the `COMMIT`/`ROLLBACK` that closes a transaction).
- Blank lines you typed between statements are not preserved; the rules above decide.
- `GO` sits on its own line directly after the batch, followed by a blank line before the next batch.

## Clauses

- Each clause keyword (`SELECT`, `FROM`, `WHERE`, `GROUP BY`, `HAVING`, `ORDER BY`, `INTO`, `SET`, `VALUES`, …) starts
  its own line at the statement's indent, and its content goes on the following lines, one level in.
- `DISTINCT`, `TOP n` / `TOP (n)` stay on the `SELECT` line.
- **Lists are one item per line with leading commas:** `, ` at the start of each item after the first. This covers
  column lists, `GROUP BY` / `ORDER BY` lists, `SET` assignments, `INSERT` column lists, `IN (…)` lists, and procedure
  and `EXEC` parameters.
- **One-liners:** a `SELECT` whose only clause is the select list, and whose list has a single item, stays on one
  line: `SELECT 1`, `SELECT ERROR_MESSAGE()`, `SELECT @x = 2`, `SELECT 1 AS x`. Add a second item or any other clause
  (`FROM`, `WHERE`, `INTO`, …) and it expands as normal. This applies everywhere, including inside brackets: a
  subquery, derived table, `APPLY` or CTE body that is just `SELECT 1` still gets its own lines for the brackets, but
  the `SELECT` itself stays on one line.

## Brackets

- When a bracketed section spans several lines, the `(` ends the line that opens it, the content is one level deeper
  than that line, and the `)` goes on its own line **aligned with the line that opened it**. Anything after the `)`
  (an alias, `AS Name`, a following keyword) stays on the `)` line.
- Function-call brackets never expand: `COUNT(*)`, `LEFT(Name, 3)`, `IDENTITY(1, 1)`, `VARCHAR(50)`.
- Subqueries always expand: `EXISTS (…)`, `IN (SELECT …)`, scalar subqueries, derived tables, `APPLY (…)`, CTE bodies.
- `IN (1, 2, 3)` lists expand one item per line.

## Joins

- Each join is on its own line one level in from `FROM`, as `<join keywords> <table> <alias> ON <first condition>`.
- Further `AND` / `OR` conditions of the join go on their own lines one level deeper than the join.
- `CROSS APPLY` / `OUTER APPLY` follow the same layout; their subquery expands as a bracketed section.

## Conditions

- In `WHERE`, `HAVING`, join conditions and `IF` / `WHILE`, each `AND` / `OR` starts a new line at the same level as the
  condition before it.
- A bracketed group of conditions expands like any bracketed section, with its own `AND` / `OR` lines inside.
- The `AND` of a `BETWEEN … AND …` is not a condition break: `d BETWEEN @From AND @To` stays on one line.

## CASE

```sql
CASE
	WHEN x = 1
		THEN 'a'
	ELSE 'b'
END
```

`WHEN` and `ELSE` are one level in from the line that opened the `CASE`, `THEN` one level further, and `END` aligns with
the line that opened the `CASE` (the same rule as a closing bracket). A simple `CASE x WHEN …` keeps the input
expression on the `CASE` line.

## Window functions

`OVER (PARTITION BY … ORDER BY …)` stays on one line with its function.

## Common table expressions

```sql
WITH cte1
AS (
	SELECT …
)
, cte2
AS (
	SELECT …
)
SELECT …
```

## Set operators

`UNION`, `UNION ALL`, `EXCEPT` and `INTERSECT` sit on their own line with a blank line on each side.

## Data modification

- **INSERT:** `INSERT INTO <table> (` then the column list, then `)`. A single `VALUES` row expands the same way
  (`VALUES (` … `)`) so the values line up with the columns. Several rows put one row per line, each kept on one line:
  `VALUES` then `(1, 2)`, `, (3, 4)` one level in. `INSERT … SELECT` follows the column list with an ordinary
  `SELECT`.
- **UPDATE:** `UPDATE`, target one level in, then `SET`, `FROM`, `WHERE` as clauses.
- **DELETE:** `DELETE`, with the target one level in when there is one before `FROM`, then `FROM`, `WHERE` as clauses.
- **SELECT … INTO:** `INTO` is a clause like `FROM`, with the new table one level in.
- **MERGE:** `MERGE` and `USING` are clauses, with the target and source one level in and `ON` under the source:

```sql
MERGE
	dbo.Target AS t
USING
	dbo.Source AS s
	ON t.Id = s.Id
WHEN MATCHED THEN
	UPDATE
	SET
		t.A = s.A
WHEN NOT MATCHED THEN
	INSERT (
		Id
		, A
	)
	VALUES (
		s.Id
		, s.A
	);
```

## Control flow

- `IF` / `WHILE` condition on the keyword's line; multi-line conditions follow the condition rules.
- `BEGIN` and `END` align with their `IF` / `WHILE` / `ELSE`; the body is one level in.
- A single-statement body without `BEGIN … END` is one level in.
- `ELSE IF` stays on one line.
- `BEGIN TRY … END TRY` is followed directly by `BEGIN CATCH … END CATCH`, with no blank line between them.
- **Transactions:** statements between `BEGIN TRAN` and the `COMMIT` / `ROLLBACK` that closes it are one level further
  in, and the `COMMIT` / `ROLLBACK` aligns with its `BEGIN TRAN`. A `COMMIT` / `ROLLBACK` with no `BEGIN TRAN` above it
  in the same block (typically in a `CATCH`) just sits at the block's level.

## Declarations and execution

- `DECLARE @a INT` on one line. Several variables in one `DECLARE` make it a clause: `DECLARE` alone, then the
  variables one level in as a list (`@a INT = 1`, `, @b VARCHAR(10) = 'x'`).
- `EXEC proc` then each parameter on its own line one level in, leading commas.
- `CREATE PROCEDURE` / `CREATE FUNCTION`: name on the first line, parameters one per line one level in (inside brackets
  if you used them), then `AS` (or `RETURNS …` then `AS`) on its own line at the statement's level, then the body.
- `CREATE TABLE <name> (`, one column or constraint per line with leading commas, `)` aligned with `CREATE`.
- `CREATE INDEX <name>` on the first line, then `ON <table> (<columns>)` one level in, with any `INCLUDE (…)` on the
  same line; a filter `WHERE` is a clause.

## Comments

- A comment that followed code on the same line stays at the end of that code's line.
- Any other comment goes on its own line, at the indent of the code that follows it.
- In a list, a trailing comment belongs to the item before the comma: `a, -- note` becomes `a -- note` with `, b` on
  the next line.
- Block comments (`/* … */`) keep their internal text and line breaks exactly.

## Comment headers

Not part of formatting: a separate command (**SQL Helper > Add Header**, `sqlhelper header`) that adds or updates the
house header on the first procedure, function or view in a script. It changes only the header comment; the code is
checked to be untouched. The cases are in `tests/SqlHelper.Formatting.Tests/HeaderCases`.

```
/* =====================================================================================================================
	Description
		Returns a list of Matches

	Update History
		02/12/2013 : DH - Created
		21/10/2026 : DH - #12345: Fixed the date filter
===================================================================================================================== */
CREATE PROCEDURE dbo.MatchListGet
```

- Both rule lines are 120 characters. Headings are one tab in, their content two. History lines are
  `dd/MM/yyyy : initials - what changed`, oldest first.
- The header goes directly before the `CREATE` / `ALTER`.
- **No header:** a new one is added with an empty description line and `<today> : <you> - Created`. Other comments
  before the object are left where they are.
- **House header:** a history line `<today> : <you> - ` is added at the end; everything else is left exactly as it was.
- **SSMS template header** (`-- Author:` / `-- Create date:` / `-- Description:` between `-- ====` rules): converted.
  The description moves across (it may run over several `--` lines); author and date become the first history line,
  `<date> : <initials> - Created`, with the date copied as written and the author shortened to initials only when it is
  a plain "First Last" name; then the new line is added. A template never filled in (`<Author,,Name>`) counts as no
  header. Anything else in the block (another label, author without date) means it isn't recognised: it stays as it
  is and a new header is added below it, so no text is ever lost.
- **Dash history block** (lines of `-- <initials> - <dd/MM/yyyy> - <what changed>` between two lines of ten or more
  dashes): converted. Each line becomes `<date> : <initials> - <what changed>`, in the same order, with the text after
  the date copied as written (trailing spaces aside), and the new line is added after them. The description is left
  empty. Lines that are just `--` are skipped; any other line (a full name, a short date, nothing after the date) means
  the block isn't recognised and is kept as it is.
- The empty description line is two tabs, and an added line with no note yet ends in `- `: both are where the cursor
  is left for typing, so they are the one exception to "no trailing whitespace".