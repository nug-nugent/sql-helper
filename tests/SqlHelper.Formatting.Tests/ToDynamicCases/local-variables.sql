DECLARE @CustomerId INT = 42

/*[*/DECLARE @Total MONEY
SELECT @Total = SUM(Total) FROM dbo.Orders WHERE CustomerId = @CustomerId
UPDATE dbo.Customers SET Total = @Total WHERE CustomerId = @CustomerId/*]*/