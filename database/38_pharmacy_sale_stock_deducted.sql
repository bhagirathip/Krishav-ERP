USE KrishavERPv52;
GO

-- Defaults to 1 (already deducted) so every historical PharmacySale row -
-- all created under the old instant-checkout flow, which deducted stock at
-- creation - keeps reading as "stock already committed, no longer editable"
-- exactly like before. Only new walk-in/OPD sales created after this change
-- start out with this false (see PharmacySalesController.Create).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PharmacySales') AND name = 'StockDeducted')
BEGIN
    ALTER TABLE dbo.PharmacySales ADD StockDeducted BIT NOT NULL DEFAULT 1;
END
GO
