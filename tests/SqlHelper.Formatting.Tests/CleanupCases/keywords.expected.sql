CREATE PROCEDURE dbo.WidgetSave
	@WidgetId INT
WITH EXECUTE AS OWNER
AS
BEGIN
	BEGIN TRANSACTION
	SAVE TRANSACTION BeforeLog
	INSERT INTO dbo.WidgetLog (WidgetId) VALUES (@WidgetId)
	INSERT INTO dbo.WidgetLog (WidgetId) VALUES (@WidgetId)
	DELETE FROM dbo.WidgetQueue WHERE WidgetId = @WidgetId
	DELETE FROM dbo.WidgetQueue WHERE WidgetId = @WidgetId
	DELETE q FROM dbo.WidgetQueue AS q WHERE q.WidgetId <> @WidgetId
	IF @@ERROR <> 0 ROLLBACK TRANSACTION BeforeLog
	EXEC dbo.AuditWrite @WidgetId
	EXEC dbo.AuditWrite @WidgetId
	COMMIT TRANSACTION
END
GO

GRANT EXECUTE ON dbo.WidgetSave TO WidgetUsers
GO

ALTER PROCEDURE dbo.WidgetPing
AS
SELECT 1
GO

DROP PROCEDURE dbo.WidgetPing