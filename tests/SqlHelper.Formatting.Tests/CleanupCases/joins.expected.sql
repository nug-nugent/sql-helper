SELECT
	w.WidgetId
FROM
	dbo.Widget AS w
	INNER JOIN dbo.Part AS p ON p.WidgetId = w.WidgetId
	LEFT JOIN dbo.Maker AS m ON m.MakerId = w.MakerId
	RIGHT JOIN dbo.Colour AS c ON c.ColourId = w.ColourId
	FULL JOIN dbo.Shelf AS f ON f.ShelfId = w.ShelfId
	INNER JOIN dbo.Stock AS k ON k.WidgetId = w.WidgetId
	LEFT HASH JOIN dbo.Region AS r ON r.RegionId = k.RegionId
	CROSS JOIN dbo.Depot AS d
	left join dbo.Item AS i ON i.ItemId = p.ItemId