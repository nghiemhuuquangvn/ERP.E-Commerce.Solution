-- ============================================================
-- AuthDB Migration 021 — Platform vs tenant permission scope (2026-10-20 security review)
--
-- Platform permissions (cross-tenant capabilities) may only be held by the platform
-- super-admin role in the platform (SYSTEM) tenant. Before this migration:
--   * seed 007 gave tenant ADMIN roles identity:tenants:view (cross-tenant listing);
--   * anonymous /auth/tenant-signup gave every new tenant's ADMIN identity:tenants:*,
--     identity:permissions:manage and settings:system:* (selected by Module = 'Identity');
--   * tenant admins could add any permission to their own roles and create a role named
--     "SuperAdmin" (which bypassed every [AuthorizePermission] check).
-- The code now prevents all of these (ERP.Shared.Authorization.PermissionScopes,
-- EffectivePermissions, RoleService); this migration cleans up existing data and adds a
-- DB-level guarantee that a user can only hold a role of the same tenant.
--
-- KEEP IN SYNC with PermissionScopes.PlatformPermissions (src/Shared/Authorization).
-- Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @PlatformTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

DECLARE @PlatformPermissions TABLE (Code NVARCHAR(256) PRIMARY KEY);
INSERT INTO @PlatformPermissions (Code) VALUES
    ('identity:tenants:view'), ('identity:tenants:create'), ('identity:tenants:edit'),
    ('identity:tenants:manage'), ('identity:tenants:delete'),
    ('identity:permissions:manage'),
    ('settings:system:view'), ('settings:system:manage'),
    ('license:modules:manage'), ('license:plans:view'), ('license:plans:manage'),
    ('license:tenant:issue');

-- 1) The platform super-admin keeps (and gets) the whole catalog.
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE r.TenantId = @PlatformTenantId AND r.RoleCode = 'SuperAdmin'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

-- 2) Every other role loses platform permissions (incl. ADMIN of the SYSTEM tenant and
--    the self-administration ADMIN role of every self-signed-up tenant).
DELETE rp
FROM RolePermissions rp
JOIN Roles r ON r.Id = rp.RoleId
JOIN Permissions p ON p.Id = rp.PermissionId
WHERE p.PermissionCode IN (SELECT Code FROM @PlatformPermissions)
  AND NOT (r.TenantId = @PlatformTenantId AND r.RoleCode = 'SuperAdmin');

-- 3) Platform role codes may only exist in the platform tenant: neutralise look-alikes
--    elsewhere (deactivate, rename, detach users) instead of deleting history.
UPDATE r
SET r.IsActive = 0,
    r.RoleCode = LEFT(r.RoleCode, 230) + '_BLOCKED_' + LEFT(CONVERT(NVARCHAR(36), r.Id), 8)
FROM Roles r
WHERE r.TenantId <> @PlatformTenantId
  AND REPLACE(REPLACE(UPPER(r.RoleCode), '_', ''), '-', '') IN ('SUPERADMIN', 'SYSTEM', 'PLATFORMADMIN', 'ROOT');

DELETE ur
FROM UserRoles ur
JOIN Roles r ON r.Id = ur.RoleId
WHERE r.RoleCode LIKE '%[_]BLOCKED[_]%' AND r.TenantId <> @PlatformTenantId;

-- 4) A user's role link must point at a role of the same tenant (bad data from the old
--    unscoped AssignRole path).
DELETE ur
FROM UserRoles ur
JOIN Roles r ON r.Id = ur.RoleId
WHERE r.TenantId <> ur.TenantId;
GO

-- 5) DB-level guarantee for (4): UserRoles(RoleId, TenantId) -> Roles(Id, TenantId).
--    NO ACTION: deletes still cascade through the existing FK_UserRoles_Role.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Roles_Id_TenantId' AND object_id = OBJECT_ID('Roles'))
    CREATE UNIQUE INDEX UX_Roles_Id_TenantId ON Roles (Id, TenantId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UserRoles_Role_SameTenant')
    ALTER TABLE UserRoles WITH CHECK
        ADD CONSTRAINT FK_UserRoles_Role_SameTenant
        FOREIGN KEY (RoleId, TenantId) REFERENCES Roles (Id, TenantId);
GO

-- 6) Report (not auto-fixed): API keys whose scopes include a platform permission outside
--    the platform tenant. New keys are validated by ApiKeyService; revoke these manually.
SELECT k.Id, k.TenantId, k.Name, k.KeyPrefix, k.Scopes
FROM ApiKeys k
WHERE k.IsActive = 1
  AND k.TenantId <> '00000000-0000-0000-0000-000000000001'
  AND (k.Scopes LIKE '%identity:tenants:%' OR k.Scopes LIKE '%identity:permissions:manage%'
       OR k.Scopes LIKE '%settings:system:%' OR k.Scopes LIKE '%license:plans:%'
       OR k.Scopes LIKE '%license:modules:manage%' OR k.Scopes LIKE '%license:tenant:issue%');
GO
