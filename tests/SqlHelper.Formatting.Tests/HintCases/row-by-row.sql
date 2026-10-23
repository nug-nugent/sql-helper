CREATE PROCEDURE dbo.ProcessOrders
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE OrderCursor CURSOR FOR
		SELECT o.OrderId FROM dbo.Orders AS o;

	DECLARE @Id INT;
	OPEN OrderCursor;
	FETCH NEXT FROM OrderCursor INTO @Id;
	WHILE @@FETCH_STATUS = 0
	BEGIN
		FETCH NEXT FROM OrderCursor INTO @Id;
	END;

	WHILE EXISTS (SELECT 1 FROM dbo.Queue AS q)
	BEGIN
		DELETE FROM dbo.Queue WHERE QueueId = (SELECT MIN(QueueId) FROM dbo.Queue);
	END;

	WHILE 1 = 1
	BEGIN
		DELETE TOP (1000) FROM dbo.Log WHERE LoggedAt < @Cutoff;
		IF @@ROWCOUNT = 0 BREAK;
	END;

	SELECT
		o.OrderId
		, dbo.OrderTotal(o.OrderId) AS Total
		, (SELECT COUNT(*) FROM dbo.OrderLine AS l WHERE l.OrderId = o.OrderId) AS Lines
		, (SELECT COUNT(*) FROM dbo.Product AS p) AS Products
		, o.Details.value('(/d/@n)[1]', 'INT') AS N
	FROM
		dbo.Orders AS o
	WHERE
		dbo.IsOpen(o.Status) = 1
	ORDER BY
		NEWID();
END;