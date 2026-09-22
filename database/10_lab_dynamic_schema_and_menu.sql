USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.LabTests', 'SchemaJson') IS NULL
BEGIN
    ALTER TABLE dbo.LabTests
    ADD SchemaJson NVARCHAR(MAX) NULL;
END
GO
