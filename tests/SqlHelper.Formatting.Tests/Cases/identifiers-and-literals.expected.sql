SELECT
	t.Name
	, t.[Date]
	, t.[Order Total]
	, t.Type
	, dbo.fn_Format(t.Amount) AS Amt
	, UPPER(t.Code)
	, N'It''s'
	, 0x1A2b
	, 1.5e3
FROM
	dbo.[My Table] t
WHERE
	t.Status = 'Active'