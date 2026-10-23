CREATE VIEW dbo.RecentOrders
AS
SELECT TOP 100 PERCENT
	*
FROM
	dbo.Orders
ORDER BY
	OrderDate DESC;
GO
WITH Totals AS (
	SELECT l.OrderId, SUM(l.Amount) AS Amount FROM dbo.OrderLine AS l GROUP BY l.OrderId
)
SELECT
	t.*
FROM
	Totals AS t
	INNER JOIN Totals AS t2 ON t2.OrderId = t.OrderId;

IF (SELECT COUNT(*) FROM dbo.Orders) > 0 PRINT 'Some';

SELECT DISTINCT
	c.CustomerId
FROM
	dbo.Customer AS c
	INNER JOIN dbo.Orders AS o
		ON o.CustomerId = c.CustomerId
		AND o.Total > (SELECT AVG(o2.Total) FROM dbo.Orders AS o2);

SELECT 'Open' AS Kind, o.OrderId FROM dbo.Orders AS o WHERE o.Status = 1
UNION
SELECT 'Closed', o.OrderId FROM dbo.Orders AS o WHERE o.Status = 2;

SELECT TOP 10
	o.OrderId
FROM
	dbo.Orders AS o, dbo.Customer AS c;

SELECT
	o.OrderId
FROM
	dbo.Orders AS o, dbo.Customer AS c
WHERE
	c.CustomerId = o.CustomerId;

SELECT
	o.OrderId
FROM
	dbo.Orders AS o
	LEFT JOIN dbo.Shipment AS s ON s.OrderId = o.OrderId
	LEFT JOIN dbo.Refund AS r ON r.OrderId = o.OrderId
WHERE
	s.Carrier = 'DHL'
	AND r.RefundId IS NULL
	AND EXISTS (SELECT * FROM dbo.Customer AS c WHERE c.CustomerId = o.CustomerId);