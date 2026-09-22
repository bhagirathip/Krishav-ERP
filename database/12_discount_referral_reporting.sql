USE KrishavERPv52;
GO

IF OBJECT_ID('dbo.DiscountTypes','U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscountTypes
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(256) NOT NULL,
        DiscountMode NVARCHAR(20) NOT NULL,
        Value DECIMAL(18,2) NOT NULL DEFAULT 0,
        Scope NVARCHAR(20) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
    CREATE UNIQUE INDEX UX_DiscountTypes_Name ON dbo.DiscountTypes(Name);
END
GO

IF OBJECT_ID('dbo.Referrers','U') IS NULL
BEGIN
    CREATE TABLE dbo.Referrers
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ReferrerCode NVARCHAR(30) NOT NULL DEFAULT '',
        Name NVARCHAR(256) NOT NULL,
        Phone NVARCHAR(50) NULL,
        Address NVARCHAR(MAX) NULL,
        ReferrerType NVARCHAR(30) NOT NULL DEFAULT 'Person',
        DoctorId INT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_Referrers_Code ON dbo.Referrers(ReferrerCode) WHERE ReferrerCode <> '';
    CREATE INDEX IX_Referrers_Name ON dbo.Referrers(Name);
END
GO

IF OBJECT_ID('dbo.ReferralPayouts','U') IS NULL
BEGIN
    CREATE TABLE dbo.ReferralPayouts
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ReferrerId INT NOT NULL,
        PatientId INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL DEFAULT 0,
        Notes NVARCHAR(MAX) NULL,
        ApprovedByUserId INT NULL,
        UpdatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ReferralPayouts_Referrer FOREIGN KEY(ReferrerId) REFERENCES dbo.Referrers(Id),
        CONSTRAINT FK_ReferralPayouts_Patient FOREIGN KEY(PatientId) REFERENCES dbo.Patients(Id)
    );
    CREATE UNIQUE INDEX UX_ReferralPayouts_Referrer_Patient ON dbo.ReferralPayouts(ReferrerId,PatientId);
END
GO

IF COL_LENGTH('dbo.Patients','MarketingSource') IS NULL
BEGIN
    ALTER TABLE dbo.Patients ADD MarketingSource NVARCHAR(100) NOT NULL CONSTRAINT DF_Patients_MarketingSource DEFAULT 'Walk-in';
END
GO
IF COL_LENGTH('dbo.Patients','ReferrerId') IS NULL ALTER TABLE dbo.Patients ADD ReferrerId INT NULL;
GO

IF COL_LENGTH('dbo.Bills','ReferrerId') IS NULL ALTER TABLE dbo.Bills ADD ReferrerId INT NULL;
GO
IF COL_LENGTH('dbo.Bills','BulkDiscountTypeId') IS NULL ALTER TABLE dbo.Bills ADD BulkDiscountTypeId INT NULL;
GO
IF COL_LENGTH('dbo.Bills','BulkDiscountName') IS NULL ALTER TABLE dbo.Bills ADD BulkDiscountName NVARCHAR(256) NULL;
GO

IF COL_LENGTH('dbo.BillItems','DiscountTypeId') IS NULL ALTER TABLE dbo.BillItems ADD DiscountTypeId INT NULL;
GO
IF COL_LENGTH('dbo.BillItems','DiscountName') IS NULL ALTER TABLE dbo.BillItems ADD DiscountName NVARCHAR(256) NULL;
GO

IF COL_LENGTH('dbo.LabOrderTests','DiscountTypeId') IS NULL ALTER TABLE dbo.LabOrderTests ADD DiscountTypeId INT NULL;
GO
IF COL_LENGTH('dbo.LabOrderTests','DiscountName') IS NULL ALTER TABLE dbo.LabOrderTests ADD DiscountName NVARCHAR(256) NULL;
GO

IF COL_LENGTH('dbo.PharmacySaleItems','DiscountTypeId') IS NULL ALTER TABLE dbo.PharmacySaleItems ADD DiscountTypeId INT NULL;
GO
IF COL_LENGTH('dbo.PharmacySaleItems','DiscountName') IS NULL ALTER TABLE dbo.PharmacySaleItems ADD DiscountName NVARCHAR(256) NULL;
GO

DECLARE @DiscountSeed TABLE(Name NVARCHAR(256),Scope NVARCHAR(20));
INSERT INTO @DiscountSeed(Name,Scope) VALUES
('Company Default Discount','Bulk'),('Management Approved Discount','Bulk'),('Senior Citizen','Individual'),('BPL','Individual'),('Staff','Individual'),('Staff Family','Individual'),('Doctor Recommended','Individual'),('Package Discount','Bulk'),('Corporate/Company Agreement','Bulk'),('Insurance/TPA','Bulk'),('Promotional/Campaign','Bulk'),('Other','Individual');
INSERT INTO dbo.DiscountTypes(Name,DiscountMode,Value,Scope,IsActive)
SELECT s.Name,'Percent',0,s.Scope,0 FROM @DiscountSeed s
WHERE NOT EXISTS(SELECT 1 FROM dbo.DiscountTypes d WHERE d.Name=s.Name);
GO

DECLARE @AdminRoleId INT=(SELECT TOP 1 Id FROM dbo.AppRoles WHERE Name='Administrator' AND IsActive=1);
IF @AdminRoleId IS NOT NULL
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE RoleId=@AdminRoleId AND Module='DISCOUNT')
        INSERT dbo.RolePermissions(RoleId,Module,CanView,CanAdd,CanEdit,CanDelete) VALUES(@AdminRoleId,'DISCOUNT',1,1,1,1);
    IF NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE RoleId=@AdminRoleId AND Module='REFERRAL')
        INSERT dbo.RolePermissions(RoleId,Module,CanView,CanAdd,CanEdit,CanDelete) VALUES(@AdminRoleId,'REFERRAL',1,1,1,1);
END
GO
