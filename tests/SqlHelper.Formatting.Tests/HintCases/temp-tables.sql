CREATE PROCEDURE dbo.Rebuild
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @Recent TABLE (OrderId INT PRIMARY KEY);
	INSERT INTO @Recent (OrderId)
	SELECT o.OrderId FROM dbo.Orders AS o WHERE o.OrderDate > @Since;

	SELECT o.OrderId, o.CustomerId INTO #Orders FROM dbo.Orders AS o;

	SELECT r.OrderId FROM @Recent AS r INNER JOIN #Orders AS t ON t.OrderId = r.OrderId;
	SELECT c.CustomerId FROM dbo.Customer AS c INNER JOIN #Orders AS t ON t.CustomerId = c.CustomerId;

	CREATE TABLE #Late (OrderId INT);

	DECLARE @i INT = 0;
	WHILE @i < 10
	BEGIN
		CREATE TABLE #Batch (OrderId INT);
		DROP TABLE #Batch;
		SET @i += 1;
	END;
END;