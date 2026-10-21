SELECT
	CASE Status
		WHEN 1
			THEN 'Open'
		WHEN 2
			THEN 'Closed'
		ELSE 'Unknown'
	END AS StatusName
FROM
	dbo.T