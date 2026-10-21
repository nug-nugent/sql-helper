select 'a' as x, 1 as y
select [Id], Name from dbo.T
select case when a = 1 then 1 end as c, b from dbo.T
select (select 1) as s, b from dbo.T
select -1 as n, b from dbo.T
select @v as n, b from dbo.T
select t.*, u.Id from dbo.T t
