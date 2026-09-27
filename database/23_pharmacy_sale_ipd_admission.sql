USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.PharmacySales','IpdAdmissionId') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacySales ADD IpdAdmissionId INT NULL;
END
GO
