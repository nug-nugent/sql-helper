/* =====================================================================================================================
	Description
		Returns a list of Matches

	Update History
		02/12/2013 : DH - Created
		21/10/2026 : DH - #12345: Example change
===================================================================================================================== */
CREATE PROCEDURE dbo.MatchListGet
	@DateFrom DATE = NULL
AS
BEGIN
	SELECT MatchID FROM dbo.Matches WHERE MatchDate >= @DateFrom
END