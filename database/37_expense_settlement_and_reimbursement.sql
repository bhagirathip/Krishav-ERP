USE KrishavERPv52;
GO

IF OBJECT_ID('dbo.ExpenseSettlements', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpenseSettlements (
        Id INT IDENTITY PRIMARY KEY,
        SettlementNumber NVARCHAR(50) NOT NULL DEFAULT '',
        SettlementDateUtc DATETIME2 NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        ReimbursedAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        SettledByRole NVARCHAR(100) NOT NULL DEFAULT ''
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailyExpenses') AND name = 'IsSettled')
BEGIN
    ALTER TABLE dbo.DailyExpenses ADD IsSettled BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailyExpenses') AND name = 'SettlementId')
BEGIN
    ALTER TABLE dbo.DailyExpenses ADD SettlementId INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DailyExpenses_Settlement')
BEGIN
    ALTER TABLE dbo.DailyExpenses
        ADD CONSTRAINT FK_DailyExpenses_Settlement FOREIGN KEY (SettlementId) REFERENCES dbo.ExpenseSettlements(Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SalaryPayments') AND name = 'ReimbursementAmount')
BEGIN
    ALTER TABLE dbo.SalaryPayments ADD ReimbursementAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
END
GO
