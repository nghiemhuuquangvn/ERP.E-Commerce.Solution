-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 007: Seed the Users/Roles/Permissions/Tenants management permission
-- codes that back ERP.Identity.Application.Authorization.Permissions — until now
-- these constants had zero rows in any seed script, so no role in any tenant could
-- actually be granted them except by manual DB insert. Names use the final
-- hierarchical "identity:{resource}:{action}" convention (matches
-- src/Shared/Authorization/PermissionConstants.cs's module:action pattern used by
-- the other 13 services), not the old flat snake_case the C# constants used to hold.
--
-- Note: 001_CreateAuthDB.sql already seeded unrelated 'users:manage'/'roles:manage'
-- permission codes that no C# constant ever referenced (orphaned rows, left as-is —
-- not touched by this migration to avoid disturbing existing data).
-- ============================================================

USE AuthDB;
GO

DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';

-- Users
DECLARE @PermUsersManageId  UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000000C';
DECLARE @PermUsersViewId    UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000000D';
DECLARE @PermUsersCreateId  UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000000E';
DECLARE @PermUsersEditId    UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000000F';
DECLARE @PermUsersDeleteId  UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000010';

-- Roles
DECLARE @PermRolesManageId  UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000011';
DECLARE @PermRolesViewId    UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000012';
DECLARE @PermRolesCreateId  UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000013';
DECLARE @PermRolesEditId    UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000014';
DECLARE @PermRolesDeleteId  UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000015';

-- Permissions
DECLARE @PermPermsManageId  UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000016';
DECLARE @PermPermsViewId    UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000017';

-- Tenants
DECLARE @PermTenantsManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000018';
DECLARE @PermTenantsViewId   UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000019';
DECLARE @PermTenantsCreateId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000001A';
DECLARE @PermTenantsEditId   UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000001B';
DECLARE @PermTenantsDeleteId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000001C';

-- ============================================================
-- Insert permissions (idempotent)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:users:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermUsersManageId, 'identity:users:manage', 'Manage Users', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:users:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermUsersViewId, 'identity:users:view', 'View Users', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:users:create')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermUsersCreateId, 'identity:users:create', 'Create Users', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:users:edit')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermUsersEditId, 'identity:users:edit', 'Edit Users', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:users:delete')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermUsersDeleteId, 'identity:users:delete', 'Delete Users', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:roles:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermRolesManageId, 'identity:roles:manage', 'Manage Roles', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:roles:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermRolesViewId, 'identity:roles:view', 'View Roles', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:roles:create')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermRolesCreateId, 'identity:roles:create', 'Create Roles', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:roles:edit')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermRolesEditId, 'identity:roles:edit', 'Edit Roles', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:roles:delete')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermRolesDeleteId, 'identity:roles:delete', 'Delete Roles', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:permissions:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermPermsManageId, 'identity:permissions:manage', 'Manage Permissions', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:permissions:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermPermsViewId, 'identity:permissions:view', 'View Permissions', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:tenants:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermTenantsManageId, 'identity:tenants:manage', 'Manage Tenants', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:tenants:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermTenantsViewId, 'identity:tenants:view', 'View Tenants', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:tenants:create')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermTenantsCreateId, 'identity:tenants:create', 'Create Tenants', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:tenants:edit')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermTenantsEditId, 'identity:tenants:edit', 'Edit Tenants', 'Identity');
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'identity:tenants:delete')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module) VALUES (@PermTenantsDeleteId, 'identity:tenants:delete', 'Delete Tenants', 'Identity');
GO

-- ============================================================
-- Grant all 17 to SuperAdmin (cross-tenant, full control)
-- ============================================================
DECLARE @SuperAdminRoleId2 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT @SuperAdminRoleId2, p.Id
FROM Permissions p
WHERE p.PermissionCode IN (
    'identity:users:manage', 'identity:users:view', 'identity:users:create', 'identity:users:edit', 'identity:users:delete',
    'identity:roles:manage', 'identity:roles:view', 'identity:roles:create', 'identity:roles:edit', 'identity:roles:delete',
    'identity:permissions:manage', 'identity:permissions:view',
    'identity:tenants:manage', 'identity:tenants:view', 'identity:tenants:create', 'identity:tenants:edit', 'identity:tenants:delete'
)
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = @SuperAdminRoleId2 AND rp.PermissionId = p.Id);

-- ============================================================
-- Grant Admin the tenant-scoped subset: full user/role management within their
-- own tenant, view-only for the permission catalog (avoid arbitrary grants) and
-- for tenants (so the frontend's tenant selector can appear without allowing
-- Admin to create/edit/delete other tenants — that stays SuperAdmin-only).
-- ============================================================
DECLARE @AdminRoleId2 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT @AdminRoleId2, p.Id
FROM Permissions p
WHERE p.PermissionCode IN (
    'identity:users:manage', 'identity:users:view', 'identity:users:create', 'identity:users:edit', 'identity:users:delete',
    'identity:roles:manage', 'identity:roles:view', 'identity:roles:create', 'identity:roles:edit', 'identity:roles:delete',
    'identity:permissions:view',
    'identity:tenants:view'
)
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = @AdminRoleId2 AND rp.PermissionId = p.Id);
GO
