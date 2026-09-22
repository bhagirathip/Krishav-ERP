USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.Patients','Address') IS NULL
BEGIN
    ALTER TABLE dbo.Patients ADD Address NVARCHAR(500) NULL;
END
GO
