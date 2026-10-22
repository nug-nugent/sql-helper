CREATE PROCEDURE dbo.usp_Orders
	@CustomerId INT
	, @From DATE
AS
BEGIN
	SET NOCOUNT ON;

	/*[*/SELECT o.OrderId, o.Total FROM dbo.Orders o WHERE o.CustomerId = @CustomerId AND o.OrderDate >= @From AND o.Note <> 'it''s'/*]*/
END