IF EXISTS (
	SELECT
		1
	FROM
		dbo.T
)
BEGIN
	UPDATE
		dbo.T
	SET
		A = 1
END

WHILE @i < 10
BEGIN
	SET @i = @i + 1
END