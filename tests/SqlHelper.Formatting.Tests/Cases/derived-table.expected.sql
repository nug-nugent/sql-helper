SELECT
	x.Id
FROM
	(
		SELECT
			Id
			, Name
		FROM
			dbo.T
			INNER JOIN dbo.U ON U.Id = T.Id
		WHERE
			Name LIKE 'a%'
	) x
	INNER JOIN dbo.Y y ON y.Id = x.Id