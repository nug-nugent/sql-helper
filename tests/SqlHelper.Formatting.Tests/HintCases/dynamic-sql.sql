CREATE PROCEDURE dbo.OrderSearch
	@Id INT
	, @Name NVARCHAR(100)
	, @Table SYSNAME
AS
BEGIN
	SET NOCOUNT ON;

	EXEC ('SELECT a FROM dbo.T');

	EXEC sys.sp_executesql N'SELECT a FROM dbo.T WHERE Id = @Id', N'@Id INT', @Id = @Id;

	DECLARE @Sql NVARCHAR(MAX) = N'SELECT a FROM dbo.T';
	SET @Sql += N' WHERE b = 1';
	PRINT @Sql;
	EXEC sys.sp_executesql @Sql;

	DECLARE @ById NVARCHAR(MAX) = N'SELECT a FROM dbo.T WHERE Id = '
		+ CAST(@Id AS NVARCHAR(10));
	EXEC sys.sp_executesql @ById;

	DECLARE @ByName NVARCHAR(MAX) = N'SELECT a FROM dbo.T WHERE Name = N'''
		+ @Name
		+ N'''';
	EXEC sys.sp_executesql @ByName;

	EXEC ('SELECT a FROM dbo.T WHERE Id = '
		+ @Id);

	DECLARE @Count NVARCHAR(MAX) = N'SELECT COUNT(*) FROM dbo.'
		+ QUOTENAME(@Table);
	EXEC sys.sp_executesql @Count;

	DECLARE @Search NVARCHAR(MAX) = N'SELECT a FROM dbo.T WHERE 1 = 1';
	IF @Name IS NOT NULL SET @Search += N' AND Name = @Name';
	EXEC sys.sp_executesql @Search, N'@Name NVARCHAR(100)', @Name = @Name;
END;