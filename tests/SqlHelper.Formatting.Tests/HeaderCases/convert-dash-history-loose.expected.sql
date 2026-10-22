/* =====================================================================================================================
	Description
		

	Update History
		24/11/2020 : AB - PBI 10004 : TASK 10005 - Widget Detail > Ten Year Price List Sub List View
		10/09/2014 : AB - PBI 10006 : TASK 10007 - Widget-Detail-Price-Factors-and-5-List-Modules
		26/12/2024 : CD - PBI-10008: Widgets > Detail > Ten Year Price List > Edit View
		21/10/2026 : DH - #12345: Example change
===================================================================================================================== */
CREATE PROCEDURE dbo.WidgetDetailGet
	@WidgetID INT
AS
BEGIN
	SELECT WidgetID FROM dbo.Widgets WHERE WidgetID = @WidgetID
END