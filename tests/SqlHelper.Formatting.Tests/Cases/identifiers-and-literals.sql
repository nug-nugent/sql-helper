select t.Name, t.[Date], t.[Order Total], t.Type, dbo.fn_Format(t.Amount) as Amt, upper(t.Code), N'It''s', 0x1A2b, 1.5e3 from dbo.[My Table] t where t.Status = 'Active'
