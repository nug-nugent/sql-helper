-----------------------------------------------------------------------------------------------------------------------
-- AB 24/11/2020 : PBI 10004 : TASK 10005 - Widget Detail > Ten Year Price List Sub List View
-- AB 10/09/2014 PBI 10006 : TASK 10007 - Widget-Detail-Price-Factors-and-5-List-Modules
-- CD 26/12/2024 PBI-10008: Widgets > Detail > Ten Year Price List > Edit View
-----------------------------------------------------------------------------------------------------------------------
CREATE PROCEDURE dbo.WidgetDetailGet
	@WidgetID INT
AS
BEGIN
	SELECT WidgetID FROM dbo.Widgets WHERE WidgetID = @WidgetID
END