CREATE PROCEDURE dbo.sp_GetOrders
	@CustomerId INT
	, @From DATE = NULL
	, @Count INT OUTPUT
AS
BEGIN
	DECLARE @Customer INT = @CustomerId;
	SET @From = ISNULL(@From, '20200101');
	SET @Count = 0;

	SELECT o.OrderId FROM Orders AS o WHERE o.CustomerId = @Customer AND o.OrderDate >= @From;

	DECLARE @Sql NVARCHAR(MAX) = N'SELECT * FROM dbo.Orders WHERE CustomerId = ' + CAST(@CustomerId AS NVARCHAR(10));
	EXEC (@Sql);
	EXEC sp_executesql @Sql;
	EXEC GetCustomer @CustomerId;

	BEGIN TRANSACTION;
	UPDATE dbo.Orders SET Status = 2 WHERE CustomerId = @CustomerId;
	WAITFOR DELAY '00:00:05';
	COMMIT TRANSACTION;

	BEGIN TRY
		BEGIN TRANSACTION;
		INSERT INTO dbo.Audit (CustomerId) VALUES (@CustomerId);
		SELECT @Count = @@IDENTITY;
		COMMIT TRANSACTION;
	END TRY
	BEGIN CATCH
		ROLLBACK TRANSACTION;
	END CATCH;
END;