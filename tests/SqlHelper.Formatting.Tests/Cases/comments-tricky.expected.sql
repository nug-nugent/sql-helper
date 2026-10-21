/*
   Multi-line
   block comment
*/
SELECT
	a
FROM
	dbo.T
WHERE
	x = 1
	-- only active rows
	AND y = 2

IF @a = 1
BEGIN -- start
	-- leading comment
	SELECT 1
END