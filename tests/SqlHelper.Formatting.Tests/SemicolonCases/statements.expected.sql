CREATE PROCEDURE dbo.OrderProcess
	@OrderId INT
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @Total MONEY;
	DECLARE @Count INT;

	IF @OrderId IS NULL
		RETURN;

	IF EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = @OrderId)
		PRINT 'found'
	ELSE
		PRINT 'missing';

	IF @Count > 0
	BEGIN
		SELECT @Total = SUM(Amount) FROM dbo.OrderLines WHERE OrderId = @OrderId; -- running total
	END
	ELSE IF @Count = 0
	BEGIN
		SET @Total = 0;
	END

	WHILE @Count > 0
	BEGIN
		SET @Count -= 1;
		IF @Count = 5 BREAK;
	END

	BEGIN TRY
		BEGIN TRANSACTION;
		UPDATE dbo.Orders SET Total = @Total WHERE OrderId = @OrderId;
		COMMIT;
	END TRY
	BEGIN CATCH
		IF @@TRANCOUNT > 0 ROLLBACK
		;THROW;
	END CATCH

	;WITH Recent AS (
		SELECT OrderId FROM dbo.Orders WHERE OrderDate > '20260101'
	)
	SELECT OrderId FROM Recent;

	EXEC dbo.AuditWrite @OrderId = @OrderId; /* audit */
END;
GO

CREATE VIEW dbo.OpenOrders
AS
SELECT OrderId FROM dbo.Orders WHERE Closed = 0;
GO

CREATE PROCEDURE dbo.Ping
AS
SELECT 1;
GO

SELECT 1;
SELECT 2 ;
GO