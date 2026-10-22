SELECT
	w.WidgetId
FROM
	dbo.Widget AS w
	JOIN dbo.Part AS p ON p.WidgetId = w.WidgetId
	LEFT OUTER JOIN dbo.Maker AS m ON m.MakerId = w.MakerId
	RIGHT OUTER JOIN dbo.Colour AS c ON c.ColourId = w.ColourId
	FULL OUTER JOIN dbo.Shelf AS f ON f.ShelfId = w.ShelfId
	INNER JOIN dbo.Stock AS k ON k.WidgetId = w.WidgetId
	LEFT OUTER HASH JOIN dbo.Region AS r ON r.RegionId = k.RegionId
	CROSS JOIN dbo.Depot AS d
	left outer join dbo.Item AS i ON i.ItemId = p.ItemId