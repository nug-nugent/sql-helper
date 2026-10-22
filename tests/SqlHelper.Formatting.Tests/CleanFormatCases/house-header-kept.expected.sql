/* =====================================================================================================================
	Description
		Returns a widget

	Update History
		26/06/2024 : UE - Created
===================================================================================================================== */
CREATE PROCEDURE dbo.WidgetGet
	@WidgetId INT
AS
BEGIN
	SELECT
		w.WidgetId
		, w.[Name]
	FROM
		dbo.Widget AS w
	WHERE
		w.WidgetId = @WidgetId;
END;