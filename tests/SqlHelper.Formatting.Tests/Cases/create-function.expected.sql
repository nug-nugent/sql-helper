CREATE FUNCTION dbo.fn_Add (
	@a INT
	, @b INT
)
RETURNS INT
AS
BEGIN
	RETURN @a + @b
END
GO

CREATE FUNCTION dbo.fn_Rows (
	@id INT
)
RETURNS TABLE
AS
RETURN (
	SELECT
		a
		, b
	FROM
		dbo.T
	WHERE
		Id = @id
)
GO