create procedure dbo.usp_Test @Id int, @Name varchar(50) = null output as begin set nocount on; declare @x int = 1; declare @y int; if @Id is null begin select @x = 2 end else if @Id = 1 select @x = 3 else begin set @y = 4 end exec dbo.usp_Other @a = 1, @b = 2 end
GO
