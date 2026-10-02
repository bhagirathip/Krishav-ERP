IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceCharges') AND name = 'BillType')
BEGIN
    ALTER TABLE dbo.ServiceCharges ADD BillType NVARCHAR(100) NOT NULL DEFAULT '';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCharges WHERE Name = 'Emergency Charge')
    INSERT INTO dbo.ServiceCharges (Name, Price, BillType, IsActive) VALUES ('Emergency Charge', 500, 'Emergency', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCharges WHERE Name = 'Injection Charge')
    INSERT INTO dbo.ServiceCharges (Name, Price, BillType, IsActive) VALUES ('Injection Charge', 0, 'OPD', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCharges WHERE Name = 'Hospital Registration')
    INSERT INTO dbo.ServiceCharges (Name, Price, BillType, IsActive) VALUES ('Hospital Registration', 100, 'OPD', 1);
GO
