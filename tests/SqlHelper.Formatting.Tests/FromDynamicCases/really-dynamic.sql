DECLARE @Table SYSNAME = N'Orders'
DECLARE @Sql NVARCHAR(MAX) = N'SELECT * FROM dbo.' + QUOTENAME(@Table)

EXEC sys.sp_executesql @Sql

EXEC ('DELETE FROM dbo.Log WHERE LoggedAt < DATEADD(DAY, -30, GETDATE())')