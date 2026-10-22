DECLARE @Sql NVARCHAR(MAX) = N'
SELECT
	Name
FROM
	dbo.Customers
WHERE
	Name LIKE ''O''''%''
'

EXEC sys.sp_executesql
	@Sql