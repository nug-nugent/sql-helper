-- =============================================
-- Author:		David Huggett
-- Create date: 02/12/2013
-- Description:	Returns a list of Matches
-- =============================================
CREATE PROCEDURE dbo.MatchListGet
	@DateFrom DATE = NULL
AS
BEGIN
	SELECT MatchID FROM dbo.Matches WHERE MatchDate >= @DateFrom
END