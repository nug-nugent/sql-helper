CREATE PROCEDURE dbo.WidgetGridGet
	@UserId INT
AS
BEGIN
	SET NOCOUNT ON;
	SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

	SELECT
		w.WidgetId
		, w.Name
	FROM
		dbo.Widget w
	WHERE
		w.Active = 1; -- active only
END;