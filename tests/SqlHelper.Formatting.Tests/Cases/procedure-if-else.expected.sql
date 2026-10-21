CREATE PROCEDURE dbo.usp_Test
	@Id INT
	, @Name VARCHAR(50) = NULL OUTPUT
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @x INT = 1;
	DECLARE @y INT;

	IF @Id IS NULL
	BEGIN
		SELECT @x = 2
	END
	ELSE IF @Id = 1
		SELECT @x = 3
	ELSE
	BEGIN
		SET @y = 4
	END

	EXEC dbo.usp_Other
		@a = 1
		, @b = 2
END
GO