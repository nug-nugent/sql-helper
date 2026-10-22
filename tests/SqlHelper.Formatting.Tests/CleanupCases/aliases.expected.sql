SELECT
	w.WidgetName AS [Name]
	, SUM(p.Price) AS Total
	, w.Colour AS Colour
	, COUNT(*) AS [Part Count]
	, w.[Size] AS [Size]
	, w.Code AS Code
	, w.Shape AS Shape
FROM
	dbo.Widget AS w
	INNER JOIN dbo.Part AS p ON p.WidgetId = w.WidgetId
	LEFT JOIN (
		SELECT
			s.WidgetId
		FROM
			dbo.Stock AS s
	) AS stock ON stock.WidgetId = w.WidgetId
GROUP BY
	w.WidgetName
	, w.Colour
	, w.[Size]
	, w.Code
	, w.Shape

SELECT @Total = SUM(Price) FROM dbo.Part

UPDATE w SET w.Code = 'X' FROM dbo.Widget AS w