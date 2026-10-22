CREATE PROCEDURE dbo.usp_OrdersById
	@Ids dbo.IdList READONLY
AS
DECLARE @Sql NVARCHAR(MAX) = N'
SELECT
	o.OrderId
FROM
	dbo.Orders o
	INNER JOIN @Ids i ON i.Id = o.OrderId
'

EXEC sys.sp_executesql
	@Sql
	, N'@Ids dbo.IdList READONLY'
	, @Ids = @Ids