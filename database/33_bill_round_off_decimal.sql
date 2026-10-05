USE KrishavERPv52;
GO

-- RoundOff was INT (whole rupees only, -9..9). The feature now accepts
-- decimals too (e.g. 5.05), so widen the column to match the money
-- precision used elsewhere (NetAmount, GrossAmount, etc.).
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Bills')
      AND name = 'RoundOff'
      AND system_type_id = TYPE_ID('int')
)
BEGIN
    DECLARE @constraintName sysname = (
        SELECT dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Bills') AND c.name = 'RoundOff'
    );

    IF @constraintName IS NOT NULL
        EXEC('ALTER TABLE dbo.Bills DROP CONSTRAINT [' + @constraintName + ']');

    ALTER TABLE dbo.Bills ALTER COLUMN RoundOff DECIMAL(18,2) NOT NULL;
    ALTER TABLE dbo.Bills ADD CONSTRAINT DF_Bills_RoundOff DEFAULT 0 FOR RoundOff;
END
GO
