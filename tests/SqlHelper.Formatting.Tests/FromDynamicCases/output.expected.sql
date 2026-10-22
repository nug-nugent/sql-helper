DECLARE @Status VARCHAR(10) = 'Open'
DECLARE @Count INT

SELECT
	@Count = COUNT(*)
FROM
	dbo.Orders
WHERE
	Status = @Status

PRINT @Count