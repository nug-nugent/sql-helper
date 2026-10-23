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
  removed, and string literals and comments are kept exactly. (Those are the separate Clean Up and Add Semicolons
  commands.) Formatting only changes whitespace and keyword case, plus
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
  dashes; after the initials and after the date there may be ` - `, ` : ` or just spaces): converted. Each line
  becomes `<date> : <initials> - <what changed>`, in the same order, with the text after the date copied as written
  (trailing spaces aside), and the new line is added after them. The description is left empty. Lines that are just `--` are skipped; any other line (a full name, a short date, nothing after the date) means
  the block isn't recognised and is kept as it is.
- The empty description line is two tabs, and an added line with no note yet ends in `- `: both are where the cursor
  is left for typing, so they are the one exception to "no trailing whitespace".

## Semicolons

Not part of formatting: a separate command (**SQL Helper > Add Semicolons**, `sqlhelper semicolons`) that terminates
every statement in the script with `;`. It changes only semicolons; everything else is checked to be untouched. The
cases are in `tests/SqlHelper.Formatting.Tests/SemicolonCases`.

- The `;` goes directly after the statement's last token, before any trailing comment: `SET @x = 1; -- note`.
- Statements get one, including a single statement under `IF`, `ELSE` or `WHILE` (`RETURN;`, `IF @x = 5 BREAK;`).
  Blocks don't: `BEGIN … END`, `BEGIN TRY … END TRY`, `BEGIN CATCH … END CATCH` and the `END` of a `WHILE` or `IF`
  stay bare. The exception is a `CREATE` / `ALTER` of a procedure, function, view or trigger, which is itself a statement:
  its closing `END` becomes `END;`. One that ends with a plain statement (`CREATE VIEW … AS SELECT …`) gets a single `;`.
- Never before `ELSE`: a single statement under `IF` that is followed by `ELSE` stays bare.
- A statement already terminated is left as written, including a leading `;` on the next line (`;THROW`, `;WITH`),
  which terminates the statement before it.
- **Server-side final SQL:** the last statement before a `--##ServerSideFinalSqlEnd` comment (with only whitespace and
  comments between them) is never terminated, and a `;` it already has is removed. The marker is matched ignoring
  case and any spaces after the `--`.

## Server-side markers

Not part of formatting: a separate command (**SQL Helper > Add Server-Side Markers**, `sqlhelper markers`) that marks
up the first procedure in a script as a server-side grid procedure. It adds only marker comments, each on its own line
at the indent of the grid's `SELECT`, and removes that `SELECT`'s `;`; everything else is checked to be untouched. The
cases are in `tests/SqlHelper.Formatting.Tests/MarkerCases`.

```sql
CREATE PROCEDURE dbo.WidgetGridGet
	@UserId INT
AS
BEGIN
	--##ServerSideMark
	SET NOCOUNT ON;

	--##ServerSideFinalSqlStart

	SELECT
		w.WidgetId
	FROM
		dbo.Widget w

	--##ServerSideFinalSqlEnd
	--##ServerSideMark
END;
```

- The procedure's last statement is the grid's `SELECT`. If it isn't a `SELECT` (or the script has no procedure), the
  command refuses and changes nothing.
- `--##ServerSideMark` goes on the line directly after the body's `BEGIN`, or after `AS` when the body has no
  `BEGIN … END`, and again directly after `--##ServerSideFinalSqlEnd`.
- `--##ServerSideFinalSqlStart` goes at the start of the `SELECT`'s line followed by a blank line, so anything above the
  `SELECT` (a blank line, a comment) stays above the marker. `--##ServerSideFinalSqlEnd` goes after the end of the
  `SELECT`'s last line, after a blank line.
- Only missing markers are added; ones already there are left exactly where they are. The `;` is removed whether or not
  any markers were added (see Semicolons).
- The `BEGIN` / `AS` must end its line, and the `SELECT` must start and end its own lines, or the command refuses.

## Clean and Format

One command for everything (**SQL Helper > Clean and Format**, **Ctrl+Alt+C**, `sqlhelper cleanformat`): Clean Up,
then Add Semicolons, then Format SQL, each checked as it is on its own. On the whole document (not a selection) a
header in an old format, an SSMS template header or a dash history block, is then converted to the house header as
Add Header does, with the dated history line added. A house header is left exactly as it is, with no line added, and
a script with no header doesn't get one. If any step refuses, nothing changes. The cases are in
`tests/SqlHelper.Formatting.Tests/CleanFormatCases`.

## Clean Up

Not part of formatting: a separate command (**SQL Helper > Clean Up**, `sqlhelper cleanup`) that rewrites a few
tokens into the house's preferred form. Layout is left as typed; every change is checked to be exactly one of the
rewrites below. The cases are in `tests/SqlHelper.Formatting.Tests/CleanupCases`.

- **Joins:** `JOIN` becomes `INNER JOIN`, and `OUTER` is dropped: `LEFT JOIN`, `RIGHT JOIN`, `FULL JOIN` (a join hint
  stays: `LEFT HASH JOIN`). `CROSS JOIN` and `APPLY` are unchanged.
- **Full keywords:** `PROC` becomes `PROCEDURE` and `TRAN` becomes `TRANSACTION`, wherever they appear. `EXECUTE` running
  a procedure or batch becomes `EXEC`; `EXECUTE AS` and the `EXECUTE` permission are unchanged.
- `INSERT tbl` becomes `INSERT INTO tbl`, and `DELETE tbl` becomes `DELETE FROM tbl` (a `DELETE t FROM …` already
  has its `FROM` and is unchanged). `!=` becomes `<>`. `TOP 10` is left alone.
- **Column aliases** are written `expr AS Alias`: `Alias = expr` is turned round, a missing `AS` is added, and a string
  alias (`'Part Count'`) or double-quoted one becomes a name (`[Part Count]`). `SELECT @x = …` is an assignment and
  is unchanged.
- **Table aliases** get `AS` when it's missing, including derived tables: `dbo.Widget AS w`, `(…) AS stock`.
- **Brackets:** a table, column, schema, procedure or alias name is bracketed exactly when it needs to be. Brackets
  (or double quotes) come off a plain name, and are added to one that isn't plain:
  - A plain name starts with a letter or `_` and goes on with letters, digits, `_`, `@`, `$` or `#`: `[My Column]`,
    `[2020Sales]` and `[Reference.Country]` keep their brackets.
  - A name that is a keyword keeps or gains brackets: `[Order]`, `[Name]`, `[Date]`, `[Type]`, `[Size]`. The keywords
    are the ones SSMS knows, listed in `src/SqlHelper.Formatting/Rewrites/BracketedNames.txt` (non-reserved words of
    one or two letters, such as the date parts `d` and `mm`, are left out so they can be aliases).
  - Names starting with `#` or `@` are left as written.
  - Only names are touched: data types (`DATE`), function names, date parts (`DATEADD(day, …)`), variables and
    option words never get brackets.
- Inserted and replaced keywords are uppercase; everything else keeps the case it was typed in (`left outer join`
  becomes `left join`).

## Dynamic SQL

Not part of formatting: two separate commands that turn statements into dynamic SQL and back. Each works on the
selection, or the whole document when nothing is selected; variables' declarations are looked up in the whole
document either way. The cases are in `tests/SqlHelper.Formatting.Tests/ToDynamicCases` and `FromDynamicCases`.

**SQL Helper > Convert to Dynamic SQL** (`sqlhelper todynamic`) always writes a parameterised `sp_executesql` call:

```sql
	DECLARE @Sql NVARCHAR(MAX) = N'
	SELECT
		o.OrderId
	FROM
		dbo.Orders o
	WHERE
		o.CustomerId = @CustomerId
		AND o.Note <> ''it''''s''
	'

	EXEC sys.sp_executesql
		@Sql
		, N'@CustomerId INT'
		, @CustomerId = @CustomerId
```

- The statements are formatted in the house style and go on their own lines inside the string, at the indent of the
  `DECLARE`, which replaces the selection where it starts. Quotes inside are doubled.
- The string is `@Sql`, or `@Sql2`, `@Sql3`, … when that name is already declared in the batch.
- Every variable the statements use but don't declare themselves becomes a parameter, inputs first and then `OUTPUT`
  ones, each in order of first use, with the type it is declared with earlier in the batch (a `DECLARE` or the procedure's parameters). A table-type parameter is
  passed `READONLY`. A variable the statements assign (`SELECT @x = …`, `SET @x = …`, `EXEC … @x OUTPUT`) is passed
  `OUTPUT`.
- The command refuses when a variable's declaration can't be found, when a variable is a table variable (`DECLARE @t
  TABLE`, which `sp_executesql` can't take), or when the selection holds `GO`.

**SQL Helper > Convert from Dynamic SQL** (`sqlhelper fromdynamic`) replaces each dynamic call with the statements it
runs:

- It handles `EXEC sp_executesql` / `sys.sp_executesql` (also `EXECUTE`) and `EXEC (…)`. `EXEC @Sql` without brackets
  runs a procedure named by `@Sql`, so it is left alone.
- The SQL must be a string literal, a concatenation of literals, or a variable whose value is built only from literals
  (its `DECLARE … =`, `SET @Sql =` and `SET @Sql +=` statements, which are removed with the call). The variable may
  not be used anywhere else (a `PRINT @Sql` included). Calls whose SQL is built from anything else (`QUOTENAME(@t)`)
  are really dynamic and are left as they are; the command refuses only when it finds nothing it can convert.
- The statements keep the string's layout, moved to the call's indent; doubled quotes are undoubled.
- Arguments are matched to the parameter list by name or by position. A parameter passed a differently named variable
  is renamed to that variable (`@Id = @OrderId` makes `@Id` inside `@OrderId`). A parameter passed a literal, or not
  passed at all, makes the command refuse, as does a variable the statements declare that is already declared outside.

## Performance Hints

Not part of formatting: a separate command (**SQL Helper > Show Performance Hints**, `sqlhelper hints`) that reads the
selection, or the whole document, and lists patterns that are often slow or wrong. It never changes the text. Every
hint comes from the script alone (no schema, statistics or plan), so each one is a place to look, not a certain
fault; the ones that need the schema or the plan (implicit conversions, missing indexes, whether a column allows
NULLs) are left to SSMS's own tools and Copilot. The cases are in `tests/SqlHelper.Formatting.Tests/HintCases`.

- Each hint has a stable code (`SH` and three digits, grouped by hundreds), a severity and a message saying what's
  wrong and what to do instead. A **warning** is very likely a real problem; a **suggestion** depends on the data or
  the intent.
- A hint points at the code it's about: the function call, the `OR`, the `TOP`, the statement.
- In the extension the hints go to the Error List (warnings as warnings, suggestions as messages), each starting with
  its code; double-clicking one selects its code. On the command line each is a line of
  `file:line:column: warning|suggestion SHnnn: message`.
- **Option:** `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` (SH402) is flagged only when asked for (**Tools >
  Options > SQL Helper > General > Flag READ UNCOMMITTED isolation level**, `--read-uncommitted on`); off by default.
  A `NOLOCK` or `READUNCOMMITTED` table hint is always flagged (SH401).

### Conditions that stop an index seek (SH1xx)

These look only at `WHERE` and join `ON` conditions (through `AND`, `OR`, `NOT` and brackets), not `HAVING`, `CASE`
or select lists.

| Code | Severity | Flags | Not flagged |
| --- | --- | --- | --- |
| SH101 | warning | A function (`YEAR`, `UPPER`, `LEFT`, `CONVERT`, …) or `CASE` around a column on one side of a comparison, `LIKE`, `IN` or `BETWEEN` | `CAST(col AS DATE)` / `CONVERT(DATE, col)`, which can still seek; a function on the other side only (`col >= DATEADD(…)`); a user-defined function (SH203) |
| SH102 | warning | `ISNULL(col, x)` or `COALESCE(col, x)` | `ISNULL(@p, col)`, which is SH107 |
| SH103 | warning | `DATEDIFF(…, col, …)` | |
| SH104 | warning | Arithmetic on a column: `col + 1 = @x`, `col * 1.2 > @y`, `-col > 0` | |
| SH105 | warning | Strings built from columns: `First + ' ' + Last = @Name` (a `+` with a string literal in it) | |
| SH106 | warning | `LIKE` with a leading `%` or `_`, including `'%' + @p` | `LIKE 'AB%'`, `LIKE '[a-c]%'` |
| SH107 | suggestion | Optional-parameter conditions: `(@p IS NULL OR col = @p)`, `col = ISNULL(@p, col)`, `col = COALESCE(@p, col)` | The statement has `OPTION (RECOMPILE)` |
| SH108 | suggestion | `OR` between conditions on different columns in a `WHERE` (once per `OR` chain) | `OR` on the same column (`Status = 1 OR Status = 2`) |
| SH109 | warning | `OR` in a join's `ON` | |
| SH110 | suggestion | An `IN` list of more than 100 values | |
| SH111 | suggestion | A `WHERE` whose every `AND`ed condition is `<>`, `NOT LIKE` or `NOT IN (list)` | `NOT EXISTS`, which is an anti-join |

### Row-by-row work (SH2xx)

| Code | Severity | Flags | Not flagged |
| --- | --- | --- | --- |
| SH201 | warning | Any cursor (`DECLARE c CURSOR`, `SET @c = CURSOR`); the message adds "declare it `LOCAL FAST_FORWARD`" unless it is `LOCAL` and `FAST_FORWARD` or `STATIC` | |
| SH202 | suggestion | A `WHILE` loop whose body reads a permanent table or changes data | A cursor's `@@FETCH_STATUS` loop (SH201 covers it); a batching loop (`DELETE TOP (n)` / `UPDATE TOP (n)` inside) |
| SH203 | suggestion | A schema-qualified (user-defined) scalar function called in a select list or a `WHERE` / `ON` | `sys.` functions; methods of xml, hierarchyid and spatial values (`col.value(…)`) |
| SH204 | warning | Defining a multi-statement table-valued function (`RETURNS @t TABLE`) | Inline functions (`RETURNS TABLE AS RETURN`) |
| SH205 | suggestion | A subquery in the select list that refers to the outer query's tables | A subquery that doesn't (`(SELECT COUNT(*) FROM dbo.Product)`) |
| SH206 | warning | In a trigger, reading `inserted` / `deleted` into a variable (`SELECT @x = col FROM inserted`, `SET @x = (SELECT …)`) | Aggregates (`SELECT @n = COUNT(*) FROM inserted`); `IF EXISTS (SELECT … FROM deleted)` |
| SH207 | suggestion | A recursive CTE whose anchor reads no table (counting up numbers or dates) | Recursive CTEs over tables (hierarchies) |
| SH208 | warning | `ORDER BY NEWID()` | |

### Query shape (SH3xx)

| Code | Severity | Flags | Not flagged |
| --- | --- | --- | --- |
| SH301 | warning in a view, procedure, function or trigger; suggestion elsewhere | `SELECT *` and `t.*` | Inside `EXISTS`; over CTEs and derived tables only (their columns are already chosen) |
| SH302 | suggestion | A CTE read more than once in its statement | A recursive CTE's reference to itself |
| SH303 | suggestion | `(SELECT COUNT(*) …) > 0`, `<> 0`, `>= 1` (either way round) | `COUNT` with `GROUP BY` |
| SH304 | suggestion | `SELECT DISTINCT` on a query with joins | `DISTINCT` on one table |
| SH305 | suggestion | `UNION` (not `ALL`) where, in some column, every branch has a different constant | |
| SH306 | warning | `TOP 100 PERCENT … ORDER BY` in a view, subquery, derived table or CTE | The statement's own outer query |
| SH307 | warning | `TOP` without `ORDER BY` in a query that reads a table | Inside `EXISTS`; `TOP` in `UPDATE` / `DELETE`, which can't have one |
| SH308 | warning when some table isn't linked to the others by a `WHERE` condition (a cross join); suggestion otherwise | `FROM a, b` | |
| SH309 | warning | A `WHERE` condition on the optional side of a `LEFT` / `RIGHT JOIN`, which turns it into an inner join | Conditions with `IS NULL`, `ISNULL()` or `COALESCE()` in them, which let the missing rows through |
| SH310 | suggestion | A subquery in a join's `ON` | |

### Hints and locking (SH4xx)

| Code | Severity | Flags | Not flagged |
| --- | --- | --- | --- |
| SH401 | warning | `WITH (NOLOCK)` and `WITH (READUNCOMMITTED)` | |
| SH402 | warning, only with the option on | `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` | Off by default |
| SH403 | suggestion | Hints that fix the plan: `INDEX(…)`, `FORCESEEK`, `FORCESCAN`; join hints (`INNER HASH JOIN`); `OPTION (FORCE ORDER, MAXDOP, FAST, LOOP / HASH / MERGE JOIN, HASH / ORDER GROUP, CONCAT / HASH / MERGE UNION, KEEPFIXED PLAN, USE PLAN, TABLE HINT)` | `OPTION (RECOMPILE)`, `OPTIMIZE FOR`, `USE HINT` |
| SH404 | suggestion | `CREATE PROCEDURE … WITH RECOMPILE` | |
| SH405 | warning | `SET ROWCOUNT n` | `SET ROWCOUNT 0` |

### Temp tables and table variables (SH5xx)

| Code | Severity | Flags | Not flagged |
| --- | --- | --- | --- |
| SH501 | suggestion | A table variable filled by `INSERT … SELECT` from a table and joined later in the batch | |
| SH502 | warning | `CREATE TABLE #t` or `SELECT … INTO #t` inside a `WHILE` loop | |
| SH503 | suggestion | `CREATE TABLE #t` in a procedure after it has read or changed data | Inside a loop (SH502) |
| SH504 | suggestion | A temp table filled by `SELECT … INTO` and then joined twice or more with no `CREATE INDEX` or `ALTER TABLE … ADD` on it | |

### Procedures, batches and transactions (SH6xx)

| Code | Severity | Flags | Not flagged |
| --- | --- | --- | --- |
| SH601 | suggestion | A procedure without `SET NOCOUNT ON` | |
| SH602 | warning | A procedure named `sp_…` | |
| SH603 | warning | `EXEC (…)` whose SQL isn't a fixed string: `EXEC ('…' + @x)`, `EXEC (@Sql)` | `sp_executesql`; a fixed `EXEC ('…')`, which is SH610 |
| SH604 | suggestion | A table or procedure name with no schema | Temp tables, table variables, CTEs, aliases (`UPDATE t … FROM dbo.Widget AS t`), `inserted` / `deleted` in a trigger, `sp_` and `xp_` system procedures |
| SH605 | suggestion | In a procedure, a parameter copied into a local variable (`DECLARE @x INT = @p`, `SET @x = @p`) or changed (`SET @p = …`) | `OUTPUT` parameters |
| SH606 | warning | `BEGIN TRANSACTION` outside `TRY … CATCH` with no `SET XACT_ABORT ON` before it | |
| SH607 | warning | `BEGIN TRANSACTION` in a script with no `COMMIT` or `ROLLBACK` at all | |
| SH608 | warning | `WAITFOR DELAY` / `TIME` after `BEGIN TRANSACTION` and before its `COMMIT` / `ROLLBACK` (in script order) | `WAITFOR (RECEIVE …)` |
| SH609 | warning | `@@IDENTITY` | |
| SH610 | suggestion | `EXEC (…)` or `sp_executesql` whose SQL is a fixed string: literals and `+`, or a variable built only from them by its `DECLARE` and the `SET` / `SET +=` statements after it in the same statement list (a `PRINT @Sql` in between is fine) | SQL that doesn't parse; SQL that creates or alters a procedure, function, view, trigger or schema, uses `USE`, `EXECUTE AS`, a `SET` option, or a `#temp` table (`CREATE TABLE`, `SELECT … INTO`); a batch that ran `ALTER TABLE` or `CREATE TABLE` before the call; `EXEC (…) AT server`; a variable also assigned anywhere else in the batch (under an `IF`, by a `SELECT`, as an `OUTPUT` argument), whose SQL depends on the path taken |
| SH611 | warning | A value pasted into dynamic SQL: a non-literal piece after literal SQL that leaves a string open (an odd number of quotes) or ends with `=`, `<`, `>`, `LIKE`, `IN (`, `BETWEEN` or `TOP (` | `QUOTENAME(x)` and `QUOTENAME(x, ']')`, which make names (`QUOTENAME(x, '''')` is a value); pieces after anything else (`' AND ' + @Condition`, `'FROM dbo.' + @Table`) |

### Data changes (SH7xx)

| Code | Severity | Flags | Not flagged |
| --- | --- | --- | --- |
| SH701 | warning | `UPDATE` / `DELETE` of a permanent table with no `WHERE`, no `TOP` and no join; the `DELETE` message mentions `TRUNCATE TABLE` | Temp tables and table variables |
| SH702 | suggestion | `UPDATE … FROM` with a join | |
| SH703 | warning | `MERGE` whose permanent target has no `HOLDLOCK` / `SERIALIZABLE` hint | Temp tables and table variables |
| SH704 | warning | `INSERT` into a permanent table with no column list | Temp tables and table variables; `DEFAULT VALUES` |

### Correctness traps (SH8xx)

| Code | Severity | Flags | Not flagged |
| --- | --- | --- | --- |
| SH801 | warning | `NOT IN (subquery)` | `NOT IN (list)` |
| SH802 | warning | `= NULL`, `<> NULL` | |
| SH803 | warning | An end of day written as `23:59` / `23:59:59`: `BETWEEN … AND '… 23:59:59'`, `col <= '… 23:59:59.997'` | |
| SH804 | warning | `CHAR`, `VARCHAR`, `NCHAR`, `NVARCHAR`, `BINARY`, `VARBINARY` without a length: 30 in `CAST` / `CONVERT`, 1 elsewhere | |
| SH805 | suggestion | `SELECT @x = col FROM …` with no `WHERE`, `TOP`, `GROUP BY` or aggregate | `SELECT @s = @s + col` (building a string on purpose); triggers (SH206) |
| SH806 | suggestion | A string that is just a date written `d/m/y` or `m/d/y` (`/`, `-` or `.`), optionally with a time | `CONVERT(…, '01/02/2024', 103)`, whose style says which way round it is; `'yyyymmdd'` and `'yyyy-mm-dd'` |