CREATE PROC dbo.WidgetSave
	@WidgetId INT
WITH EXECUTE AS OWNER
AS
BEGIN
	BEGIN TRAN
	SAVE TRAN BeforeLog
	INSERT dbo.WidgetLog (WidgetId) VALUES (@WidgetId)
	INSERT INTO dbo.WidgetLog (WidgetId) VALUES (@WidgetId)
	DELETE dbo.WidgetQueue WHERE WidgetId = @WidgetId
	DELETE FROM dbo.WidgetQueue WHERE WidgetId = @WidgetId
	DELETE q FROM dbo.WidgetQueue AS q WHERE q.WidgetId != @WidgetId
	IF @@ERROR != 0 ROLLBACK TRAN BeforeLog
	EXECUTE dbo.AuditWrite @WidgetId
	EXEC dbo.AuditWrite @WidgetId
	COMMIT TRAN
END
GO

GRANT EXECUTE ON dbo.WidgetSave TO WidgetUsers
GO

ALTER PROC dbo.WidgetPing
AS
SELECT 1
GO

DROP PROC dbo.WidgetPing