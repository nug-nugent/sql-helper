-- Adds two numbers. Changed by TJ for the 2015 season.
/* =====================================================================================================================
	Description
		

	Update History
		21/10/2026 : DH - Created
===================================================================================================================== */
CREATE FUNCTION dbo.fn_Add (@a INT, @b INT) RETURNS INT AS BEGIN RETURN @a + @b END