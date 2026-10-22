DECLARE @OrderId INT = 7
DECLARE @Lines INT

SELECT @Lines = COUNT(*) FROM dbo.OrderLines WHERE OrderId = @OrderId