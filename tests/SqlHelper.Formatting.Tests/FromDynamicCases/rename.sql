DECLARE @OrderId INT = 7
DECLARE @Lines INT

EXEC sp_executesql N'SELECT @n = COUNT(*) FROM dbo.OrderLines WHERE OrderId = @Id', N'@Id INT, @n INT OUTPUT', @Id = @OrderId, @n = @Lines OUTPUT