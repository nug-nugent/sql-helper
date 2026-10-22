CREATE PROCEDURE dbo.WidgetGridGet
	@UserId INT
AS
BEGIN
	--##ServerSideMark
	SET NOCOUNT ON;
	SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

	--##ServerSideFinalSqlStart

	SELECT
		w.WidgetId
		, w.Name
	FROM
		dbo.Widget w
	WHERE
		w.Active = 1 -- active only

	--##ServerSideFinalSqlEnd
	--##ServerSideMark
END;