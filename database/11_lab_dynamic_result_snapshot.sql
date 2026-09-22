USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.LabOrderTests', 'ResultSchemaJson') IS NULL
BEGIN
    ALTER TABLE dbo.LabOrderTests
    ADD ResultSchemaJson NVARCHAR(MAX) NULL;
END
GO
