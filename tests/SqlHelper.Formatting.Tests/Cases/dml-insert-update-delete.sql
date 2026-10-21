insert into dbo.T (A, B) values (1, 2)
update t set A = 1, B = 2 from dbo.T t inner join dbo.U u on u.Id = t.Id where t.C = 3
delete from dbo.T where A = 1
