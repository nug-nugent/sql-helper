/* =====================================================================================================================
	Description
		Returns a widget

	Update History
		26/06/2024 : UE - Created
===================================================================================================================== */
create procedure dbo.WidgetGet @WidgetId int as
begin
select w.WidgetId, w.[Name] from dbo.Widget w where w.WidgetId = @WidgetId
end