SELECT
	*
FROM
	dbo.Widget AS w
WHERE
	w.WidgetId = 1;

UPDATE
	dbo.Widget
SET
	[Size] = 2
WHERE
	WidgetId = 1;