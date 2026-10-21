WITH a
AS (
	SELECT 1 AS x
)
, b
AS (
	SELECT
		x
	FROM
		a
)
SELECT
	*
FROM
	b