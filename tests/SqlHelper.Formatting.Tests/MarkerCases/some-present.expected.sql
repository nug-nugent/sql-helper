CREATE PROCEDURE dbo.ColourGridGet
AS
BEGIN
	--##ServerSideMark
	SET NOCOUNT ON;

	--##ServerSideFinalSqlStart

	SELECT c.ColourId FROM dbo.Colour c

	--##ServerSideFinalSqlEnd
	--##ServerSideMark
END