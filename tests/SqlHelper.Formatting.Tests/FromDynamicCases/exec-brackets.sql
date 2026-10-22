DECLARE @Sql NVARCHAR(MAX)

SET @Sql = N'UPDATE dbo.Orders SET Status = ''Closed'''
SET @Sql += N' WHERE Status = ''Open'''

EXEC (@Sql)