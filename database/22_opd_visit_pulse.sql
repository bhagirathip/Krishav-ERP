USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.OpdVisits','Pulse') IS NULL
BEGIN
    ALTER TABLE dbo.OpdVisits ADD Pulse INT NULL;
END
GO
