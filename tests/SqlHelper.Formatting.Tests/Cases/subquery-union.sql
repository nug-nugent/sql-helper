select a.Id, (select max(b.X) from dbo.B b where b.AId = a.Id) as MaxX from dbo.A a where a.Id in (select Id from dbo.C) union all select 1, 2 
