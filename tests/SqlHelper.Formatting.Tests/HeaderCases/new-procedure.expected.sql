/* =====================================================================================================================
	Description
		

	Update History
		21/10/2026 : DH - Created
===================================================================================================================== */
CREATE PROCEDURE dbo.MatchListGet
	@DateFrom DATE = NULL
AS
BEGIN
	SELECT MatchID FROM dbo.Matches WHERE MatchDate >= @DateFrom
END