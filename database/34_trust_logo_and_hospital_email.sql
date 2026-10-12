USE KrishavERPv52;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Trust Logo')
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Trust Logo', '', 'Branding', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Hospital Email')
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Hospital Email', 'krishavhospital@gmail.com', 'Branding', 1);
GO
