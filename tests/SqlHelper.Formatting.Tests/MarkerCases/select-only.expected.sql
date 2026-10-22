CREATE PROCEDURE dbo.PartGridGet
AS
BEGIN
	--##ServerSideMark
	-- Parts for the grid
	--##ServerSideFinalSqlStart

	SELECT p.PartId, p.PartName FROM dbo.Part p

	--##ServerSideFinalSqlEnd
	--##ServerSideMark
END