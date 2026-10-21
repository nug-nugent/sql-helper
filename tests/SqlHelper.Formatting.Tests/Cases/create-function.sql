create function dbo.fn_Add (@a int, @b int) returns int as begin return @a + @b end
GO
create function dbo.fn_Rows (@id int) returns table as return (select a, b from dbo.T where Id = @id)
GO
