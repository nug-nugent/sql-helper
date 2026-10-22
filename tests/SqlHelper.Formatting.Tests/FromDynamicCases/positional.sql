DECLARE @OrderId INT = 7

EXECUTE sys.sp_executesql N'SELECT * FROM dbo.Orders WHERE OrderId = @Id', N'@Id INT', @OrderId