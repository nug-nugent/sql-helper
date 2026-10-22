SELECT
	Name = w.WidgetName
	, Total = SUM(p.Price)
	, w.Colour 'Colour'
	, COUNT(*) AS 'Part Count'
	, w.Size Size
	, w.Code AS Code
	, w.Shape "Shape"
FROM
	dbo.Widget w
	INNER JOIN dbo.Part AS p ON p.WidgetId = w.WidgetId
	LEFT JOIN (
		SELECT
			s.WidgetId
		FROM
			dbo.Stock s
	) stock ON stock.WidgetId = w.WidgetId
GROUP BY
	w.WidgetName
	, w.Colour
	, w.Size
	, w.Code
	, w.Shape

SELECT @Total = SUM(Price) FROM dbo.Part

UPDATE w SET w.Code = 'X' FROM dbo.Widget w