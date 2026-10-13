USE KrishavERPv52;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailyExpenses') AND name = 'CreatedByRole')
BEGIN
    ALTER TABLE dbo.DailyExpenses ADD CreatedByRole NVARCHAR(100) NOT NULL DEFAULT '';
END
GO

-- Historical bug fix: PharmacySalesController.Create never copied the
-- walk-in patient name onto the Bill itself (only onto PharmacySale), so
-- every walk-in pharmacy bill created before this fix shows a blank
-- patient name on the Pharmacy Bills screen. Backfilled from the linked
-- PharmacySale, which always had it right. Safe to re-run - only touches
-- rows that are still blank.
UPDATE b
SET b.WalkInPatientName = s.WalkInPatientName
FROM dbo.Bills b
JOIN dbo.PharmacySales s ON s.BillId = b.Id
WHERE b.BillType = 'Pharmacy'
  AND b.PatientId IS NULL
  AND (b.WalkInPatientName IS NULL OR b.WalkInPatientName = '')
  AND s.WalkInPatientName IS NOT NULL
  AND s.WalkInPatientName <> '';
GO
