IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.LabTests') AND name = 'StaticTableJson')
BEGIN
    ALTER TABLE dbo.LabTests ADD StaticTableJson NVARCHAR(MAX) NULL;
END
GO
