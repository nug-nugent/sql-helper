-- =============================================
-- Author:		David Huggett
-- Create date: 02/12/2013
-- Description:	Returns a list of Matches
-- =============================================
create proc dbo.MatchListGet @DateFrom date = null as
begin
set nocount on
select MatchCount = count(*), m.MatchDate from dbo.Matches m join dbo.Teams t on t.TeamID = m.HomeTeamID where m.MatchDate >= @DateFrom and m.HomeScore != m.AwayScore group by m.MatchDate
end