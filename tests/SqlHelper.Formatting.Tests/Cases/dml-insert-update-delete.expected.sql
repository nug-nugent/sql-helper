INSERT INTO dbo.T (
	A
	, B
)
VALUES (
	1
	, 2
)

UPDATE
	t
SET
	A = 1
	, B = 2
FROM
	dbo.T t
	INNER JOIN dbo.U u ON u.Id = t.Id
WHERE
	t.C = 3

DELETE
FROM
	dbo.T
WHERE
	A = 1