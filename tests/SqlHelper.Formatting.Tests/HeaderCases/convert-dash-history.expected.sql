/* =====================================================================================================================
	Description
		

	Update History
		01/06/2023 : AB - PBI 10001 - Widget Summary > Latest Details
		28/06/2024 : CD - PBI 10002 - Widget Service > Framework V3 Upgrade
		20/05/2025 : EF - BUG-10003-Widget-Totals-Not-Updating-After-Saving-Line-and-Discount-Values
		21/10/2026 : DH - #12345: Example change
===================================================================================================================== */
CREATE PROCEDURE dbo.WidgetSummaryGet
	@WidgetID INT
AS
BEGIN
	SELECT WidgetID FROM dbo.Widgets WHERE WidgetID = @WidgetID
END