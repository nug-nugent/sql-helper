DECLARE @CustomerId INT = 42

DECLARE @Sql NVARCHAR(MAX) = N'
DECLARE @Total MONEY

SELECT
	@Total = SUM(Total)
FROM
	dbo.Orders
WHERE
	CustomerId = @CustomerId

UPDATE
	dbo.Customers
SET
	Total = @Total
WHERE
	CustomerId = @CustomerId
'

EXEC sys.sp_executesql
	@Sql
	, N'@CustomerId INT'
	, @CustomerId = @CustomerId