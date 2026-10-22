CREATE PROCEDURE dbo.usp_OrdersById
	@Ids dbo.IdList READONLY
AS
/*[*/SELECT o.OrderId FROM dbo.Orders o INNER JOIN @Ids i ON i.Id = o.OrderId/*]*/