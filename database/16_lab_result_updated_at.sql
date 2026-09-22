USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.LabOrderTests','ResultUpdatedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.LabOrderTests ADD ResultUpdatedAtUtc DATETIME2 NULL;
END
GO
