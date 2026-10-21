select a.Id from dbo.A a full outer join dbo.B b on b.Id = a.Id outer apply (select 1 as z) o right join dbo.C c on c.Id = a.Id join dbo.D d on d.Id = a.Id
