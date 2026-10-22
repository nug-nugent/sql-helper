CREATE PROCEDURE dbo.CustomerGrid
	@UserId INT
AS
BEGIN
	--##ServerSideMark
	SET NOCOUNT ON;

	--##ServerSideFinalSqlStart
	SELECT
		c.CustomerID
		, c.LastName
	FROM
		dbo.Customer c
	WHERE
		c.Active = 1 -- active only
	--##ServerSideFinalSqlEnd
	--##ServerSideMark
END;