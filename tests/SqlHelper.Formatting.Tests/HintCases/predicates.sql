SELECT
	o.OrderId
FROM
	dbo.Orders AS o
	INNER JOIN dbo.Customer AS c
		ON c.CustomerId = o.CustomerId OR c.ParentId = o.CustomerId
WHERE
	YEAR(o.OrderDate) = 2024
	AND ISNULL(o.Note, '') = ''
	AND DATEDIFF(day, o.ShippedDate, GETDATE()) < 7
	AND o.Total * 1.2 > @Limit
	AND c.FirstName + ' ' + c.LastName = @Name
	AND o.Reference LIKE '%123'
	AND c.Email LIKE '%' + @Domain
	AND (@Status IS NULL OR o.Status = @Status)
	AND (o.Priority = 1 OR c.IsVip = 1)
	AND (o.Status = 1 OR o.Status = 2)
	AND CAST(o.OrderDate AS DATE) = @Day
	AND o.OrderDate >= DATEADD(day, -7, GETDATE())
	AND UPPER(c.Code) LIKE 'AB%';