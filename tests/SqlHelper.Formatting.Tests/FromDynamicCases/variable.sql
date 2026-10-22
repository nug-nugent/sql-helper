CREATE PROCEDURE dbo.usp_Orders
	@CustomerId INT
	, @From DATE
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @Sql NVARCHAR(MAX) = N'
	SELECT
		o.OrderId
		, o.Total
	FROM
		dbo.Orders o
	WHERE
		o.CustomerId = @CustomerId
		AND o.OrderDate >= @From
		AND o.Note <> ''it''''s''
	'

	EXEC sys.sp_executesql
		@Sql
		, N'@CustomerId INT, @From DATE'
		, @CustomerId = @CustomerId
		, @From = @From
END