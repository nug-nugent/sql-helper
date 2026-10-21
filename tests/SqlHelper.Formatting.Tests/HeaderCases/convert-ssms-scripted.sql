SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		dhuggett
-- Create date: 05/12/2013
-- Description:	Returns a list of players for a given match,
--				including substitutes.
-- =============================================
ALTER PROCEDURE dbo.MatchListGet
	@DateFrom DATE = NULL
AS
BEGIN
	SELECT MatchID FROM dbo.Matches WHERE MatchDate >= @DateFrom
END