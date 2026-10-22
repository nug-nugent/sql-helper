CREATE PROCEDURE dbo.MakerGridGet
AS
SET NOCOUNT ON;

SELECT
	m.MakerId
	, m.MakerName
FROM
	dbo.Maker m
GO