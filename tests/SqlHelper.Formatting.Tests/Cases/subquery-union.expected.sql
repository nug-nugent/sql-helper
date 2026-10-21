SELECT
	a.Id
	, (
		SELECT
			MAX(b.X)
		FROM
			dbo.B b
		WHERE
			b.AId = a.Id
	) AS MaxX
FROM
	dbo.A a
WHERE
	a.Id IN (
		SELECT
			Id
		FROM
			dbo.C
	)

UNION ALL

SELECT
	1
	, 2