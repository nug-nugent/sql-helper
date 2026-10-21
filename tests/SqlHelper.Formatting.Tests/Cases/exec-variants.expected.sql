EXEC dbo.usp_A

EXEC @rc = dbo.usp_B
	@x = 1
	, @y = @z OUTPUT