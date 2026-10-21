/* =====================================================================================================================
	Description
		Returns a list of Matches

	Update History
		26/06/2024 : UE - Created
		21/02/2025 : TJ - Added @DateFrom
===================================================================================================================== */
CREATE PROCEDURE dbo.MatchListGet
	@DateFrom DATE = NULL
AS
BEGIN
	SELECT MatchID FROM dbo.Matches WHERE MatchDate >= @DateFrom
END