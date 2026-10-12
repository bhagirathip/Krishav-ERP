USE KrishavERPv52;
GO

IF OBJECT_ID('dbo.PharmacyReturns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyReturns (
        Id INT IDENTITY PRIMARY KEY,
        ReturnNumber NVARCHAR(50) NOT NULL DEFAULT '',
        PharmacySaleId INT NOT NULL,
        BillId INT NOT NULL,
        ReturnDateUtc DATETIME2 NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        RefundedAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        RefundMode NVARCHAR(50) NULL,
        Reference NVARCHAR(200) NULL,
        CONSTRAINT FK_PharmacyReturns_Sale FOREIGN KEY (PharmacySaleId) REFERENCES dbo.PharmacySales(Id),
        CONSTRAINT FK_PharmacyReturns_Bill FOREIGN KEY (BillId) REFERENCES dbo.Bills(Id)
    );
END
GO

IF OBJECT_ID('dbo.PharmacyReturnItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyReturnItems (
        Id INT IDENTITY PRIMARY KEY,
        PharmacyReturnId INT NOT NULL,
        PharmacySaleItemId INT NOT NULL,
        PurchaseItemId INT NOT NULL,
        ProductName NVARCHAR(200) NOT NULL DEFAULT '',
        BatchNo NVARCHAR(100) NOT NULL DEFAULT '',
        UnitType NVARCHAR(50) NOT NULL DEFAULT 'Strip',
        Quantity INT NOT NULL DEFAULT 0,
        QuantityInTablets INT NOT NULL DEFAULT 0,
        UnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
        CgstPercent DECIMAL(8,2) NOT NULL DEFAULT 0,
        SgstPercent DECIMAL(8,2) NOT NULL DEFAULT 0,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        CONSTRAINT FK_PharmacyReturnItems_Return FOREIGN KEY (PharmacyReturnId) REFERENCES dbo.PharmacyReturns(Id)
    );
END
GO

-- The new Pharmacy Bills screen gets its own permission module, starting
-- from the same access Pharmacy Sales already has (same pattern as the
-- earlier PHARMACY_HOSPITAL migration).
INSERT INTO dbo.RolePermissions (RoleId, Module, CanView, CanAdd, CanEdit, CanDelete)
SELECT rp.RoleId, 'PHARMACY_BILLS', rp.CanView, rp.CanAdd, rp.CanEdit, rp.CanDelete
FROM dbo.RolePermissions rp
WHERE rp.Module = 'PHARMACY_SALES'
AND NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions existing
    WHERE existing.RoleId = rp.RoleId AND existing.Module = 'PHARMACY_BILLS'
);
GO
