select a from dbo.T where (a = 1 or b = 2) and c not in (1, 2) and d is null and e like 'x%' and not exists (select 1 from dbo.U u where u.Id = T.Id)
