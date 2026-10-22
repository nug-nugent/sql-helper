CREATE PROCEDURE dbo.MakerGridGet
AS
--##ServerSideMark
SET NOCOUNT ON;

--##ServerSideFinalSqlStart

SELECT
	m.MakerId
	, m.MakerName
FROM
	dbo.Maker m

--##ServerSideFinalSqlEnd
--##ServerSideMark
GO