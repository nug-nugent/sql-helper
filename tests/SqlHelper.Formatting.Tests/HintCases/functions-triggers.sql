CREATE FUNCTION dbo.OpenOrders (@CustomerId INT)
RETURNS @Orders TABLE (OrderId INT)
AS
BEGIN
	INSERT INTO @Orders (OrderId)
	SELECT o.OrderId FROM dbo.Orders AS o WHERE o.CustomerId = @CustomerId;
	RETURN;
END;
GO
CREATE TRIGGER dbo.OrderChanged ON dbo.Orders AFTER UPDATE
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @Id INT, @Count INT;
	SELECT @Id = i.OrderId FROM inserted AS i;
	SET @Id = (SELECT d.OrderId FROM deleted AS d);
	SELECT @Count = COUNT(*) FROM inserted;
	IF EXISTS (SELECT 1 FROM deleted) RETURN;
END;
GO
WITH Numbers AS (
	SELECT 1 AS n
	UNION ALL
	SELECT n + 1 FROM Numbers WHERE n < 100
)
SELECT n FROM Numbers;