DECLARE @Name VARCHAR = 'Smith';
DECLARE @Code NVARCHAR(20) = CAST(@Name AS NVARCHAR);
DECLARE @Total INT;

SELECT
	o.OrderId
FROM
	dbo.Orders AS o
WHERE
	o.CustomerId NOT IN (SELECT b.CustomerId FROM dbo.Blocked AS b)
	AND o.CancelledAt = NULL
	AND o.OrderDate BETWEEN '20240101' AND '20240131 23:59:59'
	AND o.ShippedAt <= '2024-01-31 23:59:59.997'
	AND o.CreatedAt >= '01/02/2024'
	AND o.DueAt > CONVERT(DATETIME, '01/02/2024', 103);

SELECT @Total = o.Total FROM dbo.Orders AS o;
SELECT @Total = SUM(o.Total) FROM dbo.Orders AS o;
SELECT @Name = @Name + o.Reference FROM dbo.Orders AS o;
SELECT @Total = o.Total FROM dbo.Orders AS o WHERE o.OrderId = 1;