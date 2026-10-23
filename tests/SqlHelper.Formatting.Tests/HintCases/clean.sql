CREATE PROCEDURE dbo.OrderList
	@CustomerId INT
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		o.OrderId
		, o.OrderDate
	FROM
		dbo.Orders AS o
		INNER JOIN dbo.Customer AS c ON c.CustomerId = o.CustomerId
	WHERE
		o.CustomerId = @CustomerId
		AND o.OrderDate >= DATEADD(day, -30, GETDATE())
		AND CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
		AND NOT EXISTS (SELECT 1 FROM dbo.Refund AS r WHERE r.OrderId = o.OrderId)
	ORDER BY
		o.OrderDate DESC;
END;