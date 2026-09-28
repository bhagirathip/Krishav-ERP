-- Splits the old parent-level menu permissions (PHARMACY, LAB, STAFF, EXPENSE,
-- REPORTING) into one permission per menu/submenu item, so each page can be
-- granted independently. For every role that already had a row on the old
-- parent module, copy that same View/Add/Edit/Delete grant onto each new
-- child module (only if the role doesn't already have that child row).
SET QUOTED_IDENTIFIER ON;
GO

INSERT INTO dbo.RolePermissions (RoleId, Module, CanView, CanAdd, CanEdit, CanDelete)
SELECT rp.RoleId, child.Module, rp.CanView, rp.CanAdd, rp.CanEdit, rp.CanDelete
FROM dbo.RolePermissions rp
CROSS APPLY (
    SELECT 'PHARMACY_DISTRIBUTOR' AS Module WHERE rp.Module = 'PHARMACY'
    UNION ALL SELECT 'PHARMACY_PURCHASE' WHERE rp.Module = 'PHARMACY'
    UNION ALL SELECT 'PHARMACY_SALES' WHERE rp.Module = 'PHARMACY'
    UNION ALL SELECT 'PHARMACY_EXPIRY' WHERE rp.Module = 'PHARMACY'
    UNION ALL SELECT 'PHARMACY_STOCK_REPORT' WHERE rp.Module = 'PHARMACY'
    UNION ALL SELECT 'PHARMACY_REPORT' WHERE rp.Module = 'PHARMACY'
    UNION ALL SELECT 'LAB_MASTER' WHERE rp.Module = 'LAB'
    UNION ALL SELECT 'LAB_PATIENT_TESTS' WHERE rp.Module = 'LAB'
    UNION ALL SELECT 'LAB_CBC_ANALYZER' WHERE rp.Module = 'LAB'
    UNION ALL SELECT 'LAB_REPORT' WHERE rp.Module = 'LAB'
    UNION ALL SELECT 'STAFF_MASTER' WHERE rp.Module = 'STAFF'
    UNION ALL SELECT 'STAFF_DESIGNATION' WHERE rp.Module = 'STAFF'
    UNION ALL SELECT 'STAFF_ATTENDANCE' WHERE rp.Module = 'STAFF'
    UNION ALL SELECT 'STAFF_SALARY' WHERE rp.Module = 'STAFF'
    UNION ALL SELECT 'STAFF_REPORT' WHERE rp.Module = 'STAFF'
    UNION ALL SELECT 'EXPENSE_DAILY' WHERE rp.Module = 'EXPENSE'
    UNION ALL SELECT 'EXPENSE_LAB' WHERE rp.Module = 'EXPENSE'
    UNION ALL SELECT 'EXPENSE_PHARMACY' WHERE rp.Module = 'EXPENSE'
    UNION ALL SELECT 'EXPENSE_DOCTOR_SETTLEMENT' WHERE rp.Module = 'EXPENSE'
    UNION ALL SELECT 'EXPENSE_REPORT' WHERE rp.Module = 'EXPENSE'
    UNION ALL SELECT 'REPORT_EXECUTIVE' WHERE rp.Module = 'REPORTING'
    UNION ALL SELECT 'PAYOUT' WHERE rp.Module = 'REFERRAL'
) child
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions existing
    WHERE existing.RoleId = rp.RoleId AND existing.Module = child.Module
);
GO

DELETE FROM dbo.RolePermissions WHERE Module IN ('PHARMACY','LAB','STAFF','EXPENSE','REPORTING');
GO
