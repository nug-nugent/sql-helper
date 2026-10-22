DECLARE @Sql NVARCHAR(MAX) = N'SELECT 1'
DECLARE @Id INT = 1

DECLARE @Sql2 NVARCHAR(MAX) = N'
DELETE
FROM
	dbo.Orders
WHERE
	OrderId = @Id
'

EXEC sys.sp_executesql
	@Sql2
	, N'@Id INT'
	, @Id = @Id