SELECT
	a.Id
FROM
	dbo.A a
	FULL OUTER JOIN dbo.B b ON b.Id = a.Id
	OUTER APPLY (
		SELECT 1 AS z
	) o
	RIGHT JOIN dbo.C c ON c.Id = a.Id
	JOIN dbo.D d ON d.Id = a.Id