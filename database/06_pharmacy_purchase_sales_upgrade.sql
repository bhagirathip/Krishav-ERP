USE KrishavERPv52;
GO

IF OBJECT_ID('dbo.PharmacyDistributors', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyDistributors
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(256) NOT NULL,
        Phone NVARCHAR(50) NULL,
        GstNumber NVARCHAR(100) NULL,
        Address NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO

IF OBJECT_ID('dbo.PharmacyPurchaseInvoices', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyPurchaseInvoices
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DistributorId INT NOT NULL,
        InvoiceNumber NVARCHAR(256) NOT NULL,
        InvoiceDate DATETIME2 NOT NULL,
        PaymentType NVARCHAR(30) NOT NULL,
        DistributorDocumentName NVARCHAR(512) NULL,
        DistributorDocumentPath NVARCHAR(1024) NULL,
        TotalQuantity DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalTaxableAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalCgst DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalSgst DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_PharmacyPurchaseInvoices_Distributor
            FOREIGN KEY (DistributorId)
            REFERENCES dbo.PharmacyDistributors(Id)
    );

    CREATE UNIQUE INDEX UX_PharmacyPurchaseInvoices_Distributor_Invoice
        ON dbo.PharmacyPurchaseInvoices(DistributorId, InvoiceNumber);
END
GO

IF OBJECT_ID('dbo.PharmacyPurchaseItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyPurchaseItems
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PurchaseInvoiceId INT NOT NULL,
        SlNo INT NOT NULL,
        Manufacturer NVARCHAR(256) NOT NULL DEFAULT '',
        Hsn NVARCHAR(100) NOT NULL DEFAULT '',
        ProductName NVARCHAR(512) NOT NULL,
        Packing NVARCHAR(256) NOT NULL DEFAULT '',
        BatchNo NVARCHAR(100) NOT NULL,
        ExpiryDate DATETIME2 NOT NULL,
        Mrp DECIMAL(18,2) NOT NULL,
        Rate DECIMAL(18,2) NOT NULL,
        Quantity INT NOT NULL,
        Bonus INT NOT NULL DEFAULT 0,
                DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TaxableAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        CgstPercent DECIMAL(8,2) NOT NULL DEFAULT 0,
        CgstAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        SgstPercent DECIMAL(8,2) NOT NULL DEFAULT 0,
        SgstAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TabletsPerStrip INT NOT NULL DEFAULT 1,
        RemainingTablets INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_PharmacyPurchaseItems_Invoice
            FOREIGN KEY (PurchaseInvoiceId)
            REFERENCES dbo.PharmacyPurchaseInvoices(Id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_PharmacyPurchaseItems_Search
        ON dbo.PharmacyPurchaseItems(ProductName, BatchNo, Hsn);
END
GO

IF OBJECT_ID('dbo.PharmacySales', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacySales
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SaleNumber NVARCHAR(100) NOT NULL DEFAULT '',
        PatientId INT NULL,
        BillId INT NOT NULL,
        PaymentMode NVARCHAR(50) NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        SaleDateUtc DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        CONSTRAINT FK_PharmacySales_Patient
            FOREIGN KEY (PatientId)
            REFERENCES dbo.Patients(Id),
        CONSTRAINT FK_PharmacySales_Bill
            FOREIGN KEY (BillId)
            REFERENCES dbo.Bills(Id)
    );
END
GO

IF OBJECT_ID('dbo.PharmacySaleItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacySaleItems
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PharmacySaleId INT NOT NULL,
        PurchaseItemId INT NOT NULL,
        Manufacturer NVARCHAR(256) NOT NULL DEFAULT '',
        Hsn NVARCHAR(100) NOT NULL DEFAULT '',
        ProductName NVARCHAR(512) NOT NULL,
        BatchNo NVARCHAR(100) NOT NULL,
        Packing NVARCHAR(256) NOT NULL DEFAULT '',
        Mrp DECIMAL(18,2) NOT NULL,
        UnitType NVARCHAR(20) NOT NULL DEFAULT 'Strip',
        Quantity INT NOT NULL,
        QuantityInTablets INT NOT NULL DEFAULT 0,
        UnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
        DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalAmount DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_PharmacySaleItems_Sale
            FOREIGN KEY (PharmacySaleId)
            REFERENCES dbo.PharmacySales(Id)
            ON DELETE CASCADE,
        CONSTRAINT FK_PharmacySaleItems_PurchaseItem
            FOREIGN KEY (PurchaseItemId)
            REFERENCES dbo.PharmacyPurchaseItems(Id)
    );
END
GO
