USE KrishavERPv52;
GO

IF OBJECT_ID('dbo.HospitalSettlements', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HospitalSettlements (
        Id INT IDENTITY PRIMARY KEY,
        SettlementNumber NVARCHAR(50) NOT NULL DEFAULT '',
        SettlementDateUtc DATETIME2 NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        IsPaid BIT NOT NULL DEFAULT 0,
        PaidAtUtc DATETIME2 NULL,
        PaymentMode NVARCHAR(50) NULL,
        ReferenceNumber NVARCHAR(100) NULL
    );
END
GO

IF OBJECT_ID('dbo.HospitalIssues', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HospitalIssues (
        Id INT IDENTITY PRIMARY KEY,
        IssueNumber NVARCHAR(50) NOT NULL DEFAULT '',
        Department NVARCHAR(50) NOT NULL,
        IssueDateUtc DATETIME2 NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        SettlementId INT NULL,
        CONSTRAINT FK_HospitalIssues_Settlement FOREIGN KEY (SettlementId) REFERENCES dbo.HospitalSettlements(Id)
    );
END
GO

IF OBJECT_ID('dbo.HospitalIssueItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HospitalIssueItems (
        Id INT IDENTITY PRIMARY KEY,
        HospitalIssueId INT NOT NULL,
        PurchaseItemId INT NOT NULL,
        ProductName NVARCHAR(200) NOT NULL DEFAULT '',
        BatchNo NVARCHAR(100) NOT NULL DEFAULT '',
        UnitType NVARCHAR(50) NOT NULL DEFAULT 'Strip',
        Quantity INT NOT NULL DEFAULT 0,
        QuantityInTablets INT NOT NULL DEFAULT 0,
        Mrp DECIMAL(18,2) NOT NULL DEFAULT 0,
        UnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        CONSTRAINT FK_HospitalIssueItems_Issue FOREIGN KEY (HospitalIssueId) REFERENCES dbo.HospitalIssues(Id)
    );
END
GO

-- Same permission as Pharmacy Sales to start with - an admin can adjust it
-- separately afterward via the Permissions screen, same as any other module.
INSERT INTO dbo.RolePermissions (RoleId, Module, CanView, CanAdd, CanEdit, CanDelete)
SELECT rp.RoleId, 'PHARMACY_HOSPITAL', rp.CanView, rp.CanAdd, rp.CanEdit, rp.CanDelete
FROM dbo.RolePermissions rp
WHERE rp.Module = 'PHARMACY_SALES'
AND NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions existing
    WHERE existing.RoleId = rp.RoleId AND existing.Module = 'PHARMACY_HOSPITAL'
);
GO
