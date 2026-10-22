CREATE TABLE dbo.Widget (
	WidgetId INT NOT NULL
	, [Name] NVARCHAR(50) NOT NULL
	, [Type] INT NULL
	, Created DATE NOT NULL
)
GO

DECLARE @Since DATE = DATEADD(day, -7, GETDATE())

SELECT
	w.WidgetId
	, w.[Order]
	, w.[My Column]
	, w.[2020Sales]
	, w._Archived
	, w.[Name]
	, w.Status
	, w.Code
	, CAST(w.Created AS DATE) AS CreatedOn
	, DATEDIFF(day, w.Created, GETDATE()) AS Age
FROM
	dbo.Widget AS w
	INNER JOIN Geo.[Reference.Country] AS rc ON rc.CountryId = w.CountryId
	INNER JOIN [#Work] AS t ON t.WidgetId = w.WidgetId
WHERE
	w.Created >= @Since