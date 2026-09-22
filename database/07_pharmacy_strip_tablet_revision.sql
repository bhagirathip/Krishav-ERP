USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.PharmacyPurchaseInvoices', 'DistributorDocumentName') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacyPurchaseInvoices
    ADD DistributorDocumentName NVARCHAR(512) NULL;
END
GO

IF COL_LENGTH('dbo.PharmacyPurchaseInvoices', 'DistributorDocumentPath') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacyPurchaseInvoices
    ADD DistributorDocumentPath NVARCHAR(1024) NULL;
END
GO

IF COL_LENGTH('dbo.PharmacyPurchaseInvoices', 'InvoiceSlNo') IS NOT NULL
BEGIN
    ALTER TABLE dbo.PharmacyPurchaseInvoices
    ALTER COLUMN InvoiceSlNo NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.PharmacyPurchaseItems', 'TabletsPerStrip') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacyPurchaseItems
    ADD TabletsPerStrip INT NOT NULL
        CONSTRAINT DF_PharmacyPurchaseItems_TabletsPerStrip DEFAULT 1;
END
GO

IF COL_LENGTH('dbo.PharmacyPurchaseItems', 'RemainingTablets') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacyPurchaseItems
    ADD RemainingTablets INT NOT NULL
        CONSTRAINT DF_PharmacyPurchaseItems_RemainingTablets DEFAULT 0;

    IF COL_LENGTH('dbo.PharmacyPurchaseItems', 'RemainingQuantity') IS NOT NULL
    BEGIN
        UPDATE dbo.PharmacyPurchaseItems
        SET RemainingTablets = RemainingQuantity;
    END
    ELSE
    BEGIN
        UPDATE dbo.PharmacyPurchaseItems
        SET RemainingTablets =
            (Quantity + Bonus) * ISNULL(NULLIF(TabletsPerStrip, 0), 1);
    END
END
GO

IF COL_LENGTH('dbo.PharmacySaleItems', 'UnitType') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacySaleItems
    ADD UnitType NVARCHAR(20) NOT NULL
        CONSTRAINT DF_PharmacySaleItems_UnitType DEFAULT 'Strip';
END
GO

IF COL_LENGTH('dbo.PharmacySaleItems', 'QuantityInTablets') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacySaleItems
    ADD QuantityInTablets INT NOT NULL
        CONSTRAINT DF_PharmacySaleItems_QuantityInTablets DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.PharmacySaleItems', 'UnitPrice') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacySaleItems
    ADD UnitPrice DECIMAL(18,4) NOT NULL
        CONSTRAINT DF_PharmacySaleItems_UnitPrice DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.PharmacySaleItems', 'DiscountAmount') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacySaleItems
    ADD DiscountAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_PharmacySaleItems_DiscountAmount DEFAULT 0;
END
GO
