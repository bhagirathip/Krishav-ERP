USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.Bills','WalkInPatientName') IS NULL
BEGIN
    ALTER TABLE dbo.Bills ADD WalkInPatientName NVARCHAR(200) NULL;
END
GO
