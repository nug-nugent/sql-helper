-- Adds two numbers. Changed by TJ for the 2015 season.
CREATE FUNCTION dbo.fn_Add (@a INT, @b INT) RETURNS INT AS BEGIN RETURN @a + @b END