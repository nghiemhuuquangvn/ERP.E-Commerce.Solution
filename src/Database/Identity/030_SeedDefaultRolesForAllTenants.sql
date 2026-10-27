-- Risk register R16: a tenant created through POST /tenants or tenant-signup had no CUSTOMER / STAFF / USER role,
-- so storefront / mobile customer registration failed with "Default role ... not found".
-- New tenants now get them in code (DefaultTenantRoles); this repairs the tenants that already exist.
-- Idempotent: only inserts what is missing.
USE AuthDB;
GO

INSERT INTO Roles (Id, TenantId, RoleCode, RoleName, IsSystem, IsActive)
SELECT NEWID(), t.Id, r.RoleCode, r.RoleName, 1, 1
FROM Tenants t
CROSS JOIN (VALUES ('CUSTOMER', 'Customer'), ('STAFF', 'Staff'), ('USER', 'User')) AS r(RoleCode, RoleName)
WHERE NOT EXISTS (SELECT 1 FROM Roles x WHERE x.TenantId = t.Id AND x.RoleCode = r.RoleCode);
GO
