if exists (select 1 from dbo.T) begin update dbo.T set A = 1 end
while @i < 10 begin set @i = @i + 1 end
