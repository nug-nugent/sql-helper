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
	SET NOCOUNT ON;

	SELECT
		COUNT(*) AS MatchCount
		, m.MatchDate
	FROM
		dbo.Matches AS m
		INNER JOIN dbo.Teams AS t ON t.TeamID = m.HomeTeamID
	WHERE
		m.MatchDate >= @DateFrom
		AND m.HomeScore <> m.AwayScore
	GROUP BY
		m.MatchDate;
END;