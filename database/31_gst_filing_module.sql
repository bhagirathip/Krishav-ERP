INSERT INTO dbo.RolePermissions (RoleId, Module, CanView, CanAdd, CanEdit, CanDelete)
SELECT rp.RoleId, 'GST_FILING', rp.CanView, rp.CanAdd, rp.CanEdit, rp.CanDelete
FROM dbo.RolePermissions rp
WHERE rp.Module = 'REPORT_EXECUTIVE'
AND NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions existing
    WHERE existing.RoleId = rp.RoleId AND existing.Module = 'GST_FILING'
);
GO
