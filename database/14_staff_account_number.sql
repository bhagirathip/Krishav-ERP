USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.StaffMembers','AccountNumber') IS NULL
BEGIN
    ALTER TABLE dbo.StaffMembers ADD AccountNumber NVARCHAR(50) NULL;
END
GO
