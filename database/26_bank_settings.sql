-- Bank details used on the Salary/Payroll print's NEFT/RTGS request letter.
IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Bank Name')
BEGIN
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Bank Name', '', 'Generic', 1);
END
GO
IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE Name = 'Bank Account Number')
BEGIN
    INSERT INTO dbo.AppSettings (Name, Value, Type, IsActive) VALUES ('Bank Account Number', '', 'Generic', 1);
END
GO
