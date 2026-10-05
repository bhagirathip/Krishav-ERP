IF NOT EXISTS (SELECT 1 FROM dbo.AppRoles WHERE Name = 'Manager')
    INSERT INTO dbo.AppRoles (Name, Description, IsActive) VALUES ('Manager', 'Can apply Manager Discount on bills (capped %, configurable in Settings)', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppRoles WHERE Name = 'General Manager')
    INSERT INTO dbo.AppRoles (Name, Description, IsActive) VALUES ('General Manager', 'Can apply a direct discount amount/percentage on bills (no dropdown), capped in Settings', 1);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Manager Discount Max Percent')
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Manager Discount Max Percent', '15', 'Billing', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'General Manager Discount Max Percent')
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('General Manager Discount Max Percent', '10', 'Billing', 1);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.DiscountTypes WHERE Name = 'Manager Discount')
    INSERT INTO dbo.DiscountTypes (Name, DiscountMode, Value, Scope, IsActive) VALUES ('Manager Discount', 'Percent', 0, 'Bulk', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.DiscountTypes WHERE Name = 'Admin Discount')
    INSERT INTO dbo.DiscountTypes (Name, DiscountMode, Value, Scope, IsActive) VALUES ('Admin Discount', 'Percent', 100, 'Bulk', 1);
GO
