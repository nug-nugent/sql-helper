CREATE PROCEDURE dbo.ColourGridGet
AS
BEGIN
	--##ServerSideMark
	SET NOCOUNT ON;

	SELECT c.ColourId FROM dbo.Colour c;
	--##ServerSideMark
END