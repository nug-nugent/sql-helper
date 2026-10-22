DECLARE @Status VARCHAR(10) = 'Open'
DECLARE @Count INT

DECLARE @Sql NVARCHAR(MAX) = N'
SELECT
	@Count = COUNT(*)
FROM
	dbo.Orders
WHERE
	Status = @Status
'

EXEC sys.sp_executesql
	@Sql
	, N'@Status VARCHAR(10), @Count INT OUTPUT'
	, @Status = @Status
	, @Count = @Count OUTPUT

PRINT @Count