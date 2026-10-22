-----------------------------------------------------------------------
-- SR - 01/06/2023 - PBI 13217 - EMR Patient Summary > Latest Information
-- BD - 28/06/2024 - PBI 59898 - EMR Solution > Hicom.Engine V3 Upgrade 
-- DS - 20/05/2025 - BUG-90602-EMR-Episode-Graphs-Not-Updating-After-Saving-Blood-Glucose-and-Foot-Charcot-Values
-----------------------------------------------------------------------
CREATE PROCEDURE dbo.PatientSummaryGet
	@PatientID INT
AS
BEGIN
	SELECT PatientID FROM dbo.Patients WHERE PatientID = @PatientID
END