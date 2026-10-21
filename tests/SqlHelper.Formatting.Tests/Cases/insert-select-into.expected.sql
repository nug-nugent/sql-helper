INSERT INTO #tmp (
	Id
	, Name
)
SELECT
	Id
	, Name
FROM
	dbo.T
WHERE
	Id > 5

SELECT
	Id
INTO
	#t2
FROM
	dbo.T