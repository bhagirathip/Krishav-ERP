IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PharmacySaleItems') AND name = 'CgstPercent')
BEGIN
    ALTER TABLE dbo.PharmacySaleItems ADD CgstPercent DECIMAL(8,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PharmacySaleItems') AND name = 'SgstPercent')
BEGIN
    ALTER TABLE dbo.PharmacySaleItems ADD SgstPercent DECIMAL(8,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Hospital GST No')
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Hospital GST No', '', 'Billing', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Pharmacy GST No')
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Pharmacy GST No', '', 'Billing', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Bill HSN No')
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Bill HSN No', '9993', 'Billing', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Bill GST Rate')
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Bill GST Rate', '0', 'Billing', 1);
GO
