-----------------------------------------------------------------------
-- AB - 01/06/2023 - PBI 10001 - Widget Summary > Latest Details
-- CD - 28/06/2024 - PBI 10002 - Widget Service > Framework V3 Upgrade 
-- EF - 20/05/2025 - BUG-10003-Widget-Totals-Not-Updating-After-Saving-Line-and-Discount-Values
-----------------------------------------------------------------------
CREATE PROCEDURE dbo.WidgetSummaryGet
	@WidgetID INT
AS
BEGIN
	SELECT WidgetID FROM dbo.Widgets WHERE WidgetID = @WidgetID
END