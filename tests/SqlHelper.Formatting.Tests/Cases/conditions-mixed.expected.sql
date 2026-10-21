SELECT
	a
FROM
	dbo.T
WHERE
	(
		a = 1
		OR b = 2
	)
	AND c NOT IN (
		1
		, 2
	)
	AND d IS NULL
	AND e LIKE 'x%'
	AND NOT EXISTS (
		SELECT
			1
		FROM
			dbo.U u
		WHERE
			u.Id = T.Id
	)