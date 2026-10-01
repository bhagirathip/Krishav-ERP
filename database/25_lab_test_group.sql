-- Tests sharing the same Group (e.g. "Fever Panel", "Sugar Panel") print
-- merged together onto one page when several tests are printed at once; a
-- test with no group always prints on its own separate page.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.LabTests') AND name = 'Group'
)
BEGIN
    ALTER TABLE dbo.LabTests ADD [Group] NVARCHAR(200) NULL;
END
GO
