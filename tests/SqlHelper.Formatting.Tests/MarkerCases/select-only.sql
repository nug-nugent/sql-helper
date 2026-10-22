CREATE PROCEDURE dbo.PartGridGet
AS
BEGIN
	-- Parts for the grid
	SELECT p.PartId, p.PartName FROM dbo.Part p
END