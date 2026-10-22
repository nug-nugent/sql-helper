/* ====================================================================================================================
	Description
		Example procedure that supplies the rows for a grid

	Update History
		01/02/2023 : AB - Created
		15/06/2024 : CD - Added HideEditAction (hides the edit button)
		09/09/2025 : EF - Updated for server-side paging
======================================================================================================================= */
CREATE PROCEDURE [Sample].[WidgetGridGet]
	-- Standard parameters
	@UserId INT
AS
BEGIN
	--##ServerSideMark
	SET NOCOUNT ON;
	SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; -- The default isolation level for grids

	--##ServerSideFinalSqlStart

	SELECT 
		Widget.WidgetId
		, Widget.Name
		, Widget.Colour
		, (CASE WHEN maker.Active = 0 THEN CONCAT('[', ISNULL(maker.MakerName, ''), ']') ELSE ISNULL(maker.MakerName, '') END) AS Maker
		, (CASE WHEN ISJSON(ISNULL(Widget.PartIds, '')) = 1 THEN (
			SELECT 
				STRING_AGG(p.PartName, ', ') WITHIN GROUP (ORDER BY p.PartName)
			FROM 
				OPENJSON(Widget.PartIds) WITH (value INT '$') AS partIds
				INNER JOIN [Sample].[Reference.Part] p ON p.PartId = partIds.value
		) ELSE '' END) AS Parts
		, Widget.Active
		, ~Widget.Active AS Archived -- The opposite of Active
		, GETDATE() AS CreatedDateTime
		, NULL AS LastEditedById
		, CAST(Widget.WidgetId % 2 AS BIT) AS HideEditAction -- Example calculation for hiding the edit button
	FROM
		[Sample].Widget
		INNER JOIN [Sample].WidgetDetails ON Widget.WidgetId = WidgetDetails.WidgetId
		LEFT JOIN [Sample].[Reference.Maker] maker ON Widget.MakerId = maker.MakerId

	--##ServerSideFinalSqlEnd
	--##ServerSideMark
END;