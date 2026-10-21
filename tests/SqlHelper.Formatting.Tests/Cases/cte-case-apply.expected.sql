WITH cte
AS (
	SELECT
		Id
		, CASE
			WHEN X = 1
				THEN 'a'
			WHEN X = 2
				THEN 'b'
			ELSE 'c'
		END AS Lbl
	FROM
		dbo.T
)
SELECT
	c.Id
	, c.Lbl
FROM
	cte c
	CROSS APPLY (
		SELECT TOP 1
			*
		FROM
			dbo.U u
		WHERE
			u.Id = c.Id
	) x
WHERE
	EXISTS (
		SELECT
			1
		FROM
			dbo.V v
		WHERE
			v.Id = c.Id
	)