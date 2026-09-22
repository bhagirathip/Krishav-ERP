USE KrishavERPv52;
GO

IF NOT EXISTS(SELECT 1 FROM dbo.AppSettings WHERE Name='Lab Signature')
    INSERT INTO dbo.AppSettings(Name,Value,Type,IsActive) VALUES('Lab Signature','','Branding',1);
GO
IF NOT EXISTS(SELECT 1 FROM dbo.AppSettings WHERE Name='Lab Signatory Name')
    INSERT INTO dbo.AppSettings(Name,Value,Type,IsActive) VALUES('Lab Signatory Name','','Generic',1);
GO
IF NOT EXISTS(SELECT 1 FROM dbo.AppSettings WHERE Name='Lab Signatory Qualification')
    INSERT INTO dbo.AppSettings(Name,Value,Type,IsActive) VALUES('Lab Signatory Qualification','','Generic',1);
GO
