USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.Patients','AgeUnit') IS NULL
BEGIN
    ALTER TABLE dbo.Patients ADD AgeUnit NVARCHAR(20) NOT NULL DEFAULT 'Years';
END
GO
