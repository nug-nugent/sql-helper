SELECT DISTINCT TOP 10
	a.Id
	, b.Name
	, COUNT(*) AS Cnt
	, 1 AS One
FROM
	dbo.TableA a
	INNER JOIN dbo.TableB b ON a.Id = b.AId
		AND b.Active = 1
	LEFT JOIN dbo.C c ON c.Id = a.CId
WHERE
	a.X = 1
	AND (
		b.Y = 2
		OR b.Z IN (
			1
			, 2
			, 3
		)
	)
GROUP BY
	a.Id
	, b.Name
HAVING
	COUNT(*) > 1
ORDER BY
	b.Name