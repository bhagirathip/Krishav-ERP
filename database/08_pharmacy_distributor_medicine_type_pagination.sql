USE KrishavERPv52;
GO

IF COL_LENGTH('dbo.PharmacyDistributors', 'Pan') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacyDistributors ADD Pan NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH('dbo.PharmacyDistributors', 'DrugsBazaarId') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacyDistributors ADD DrugsBazaarId NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.PharmacyDistributors', 'Fssai') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacyDistributors ADD Fssai NVARCHAR(100) NULL;
END
GO

IF OBJECT_ID('dbo.PharmacyMedicineTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyMedicineTypes
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_PharmacyMedicineTypes_IsActive DEFAULT 1
    );

    CREATE UNIQUE INDEX UX_PharmacyMedicineTypes_Name
        ON dbo.PharmacyMedicineTypes(Name);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PharmacyMedicineTypes WHERE Name = 'Tablet')
    INSERT INTO dbo.PharmacyMedicineTypes(Name, IsActive) VALUES ('Tablet', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.PharmacyMedicineTypes WHERE Name = 'Cream')
    INSERT INTO dbo.PharmacyMedicineTypes(Name, IsActive) VALUES ('Cream', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.PharmacyMedicineTypes WHERE Name = 'Injection')
    INSERT INTO dbo.PharmacyMedicineTypes(Name, IsActive) VALUES ('Injection', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.PharmacyMedicineTypes WHERE Name = 'Spray')
    INSERT INTO dbo.PharmacyMedicineTypes(Name, IsActive) VALUES ('Spray', 1);
GO

IF COL_LENGTH('dbo.PharmacyPurchaseItems', 'MedicineTypeId') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacyPurchaseItems ADD MedicineTypeId INT NULL;
END
GO

DECLARE @TabletTypeId INT = (
    SELECT TOP 1 Id FROM dbo.PharmacyMedicineTypes WHERE Name = 'Tablet'
);

UPDATE dbo.PharmacyPurchaseItems
SET MedicineTypeId = @TabletTypeId
WHERE MedicineTypeId IS NULL;
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.PharmacyPurchaseItems')
      AND name = 'MedicineTypeId'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.PharmacyPurchaseItems ALTER COLUMN MedicineTypeId INT NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_PharmacyPurchaseItems_MedicineType'
)
BEGIN
    ALTER TABLE dbo.PharmacyPurchaseItems
    ADD CONSTRAINT FK_PharmacyPurchaseItems_MedicineType
        FOREIGN KEY (MedicineTypeId)
        REFERENCES dbo.PharmacyMedicineTypes(Id);
END
GO

IF COL_LENGTH('dbo.PharmacySales', 'DoctorId') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacySales ADD DoctorId INT NULL;
END
GO

IF COL_LENGTH('dbo.PharmacySales', 'WalkInPatientName') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacySales ADD WalkInPatientName NVARCHAR(256) NULL;
END
GO

IF COL_LENGTH('dbo.PharmacySales', 'WalkInPhone') IS NULL
BEGIN
    ALTER TABLE dbo.PharmacySales ADD WalkInPhone NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PharmacySales_Doctor'
)
BEGIN
    ALTER TABLE dbo.PharmacySales
    ADD CONSTRAINT FK_PharmacySales_Doctor
        FOREIGN KEY (DoctorId)
        REFERENCES dbo.Doctors(Id);
END
GO
