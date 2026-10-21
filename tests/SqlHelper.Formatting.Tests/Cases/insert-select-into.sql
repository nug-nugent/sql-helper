insert into #tmp (Id, Name) select Id, Name from dbo.T where Id > 5
select Id into #t2 from dbo.T
