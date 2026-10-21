SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
/* =====================================================================================================================
	Description
		Returns a list of players for a given match,
		including substitutes.

	Update History
		05/12/2013 : dhuggett - Created
		21/10/2026 : DH - #12345: Example change
===================================================================================================================== */
ALTER PROCEDURE dbo.MatchListGet
	@DateFrom DATE = NULL
AS
BEGIN
	SELECT MatchID FROM dbo.Matches WHERE MatchDate >= @DateFrom
END