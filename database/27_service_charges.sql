-- A price list for miscellaneous hospital charges (Dressing, Injection,
-- Saline, Emergency, etc.) not tied to the Lab/Pharmacy catalogs, plus the
-- SERVICE_CHARGE permission module to manage it.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ServiceCharges')
BEGIN
    CREATE TABLE dbo.ServiceCharges (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Price DECIMAL(18,2) NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO

-- Grant the new module to every role that already has full access to
-- another master-data module (DISCOUNT), so an existing Administrator role
-- isn't locked out of a page it didn't know to ask for yet.
INSERT INTO dbo.RolePermissions (RoleId, Module, CanView, CanAdd, CanEdit, CanDelete)
SELECT rp.RoleId, 'SERVICE_CHARGE', rp.CanView, rp.CanAdd, rp.CanEdit, rp.CanDelete
FROM dbo.RolePermissions rp
WHERE rp.Module = 'DISCOUNT'
AND NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions existing
    WHERE existing.RoleId = rp.RoleId AND existing.Module = 'SERVICE_CHARGE'
);
GO
