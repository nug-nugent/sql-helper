SELECT
	'a' AS x
	, 1 AS y

SELECT
	[Id]
	, Name
FROM
	dbo.T

SELECT
	CASE
		WHEN a = 1
			THEN 1
	END AS c
	, b
FROM
	dbo.T

SELECT
	(
		SELECT 1
	) AS s
	, b
FROM
	dbo.T

SELECT
	-1 AS n
	, b
FROM
	dbo.T

SELECT
	@v AS n
	, b
FROM
	dbo.T

SELECT
	t.*
	, u.Id
FROM
	dbo.T t