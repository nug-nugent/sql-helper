/* =====================================================================================================================
	Description
		

	Update History
		01/06/2023 : SR - PBI 13217 - EMR Patient Summary > Latest Information
		28/06/2024 : BD - PBI 59898 - EMR Solution > Hicom.Engine V3 Upgrade
		20/05/2025 : DS - BUG-90602-EMR-Episode-Graphs-Not-Updating-After-Saving-Blood-Glucose-and-Foot-Charcot-Values
		21/10/2026 : DH - #12345: Example change
===================================================================================================================== */
CREATE PROCEDURE dbo.PatientSummaryGet
	@PatientID INT
AS
BEGIN
	SELECT PatientID FROM dbo.Patients WHERE PatientID = @PatientID
END