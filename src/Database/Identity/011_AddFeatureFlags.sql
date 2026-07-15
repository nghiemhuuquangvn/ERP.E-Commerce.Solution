-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 011: Feature Flags — dynamic key-value settings (deliberately different
-- shape from every other Settings category: admin-defined keys, not fixed fields).
-- Storage+CRUD only — no flag is read anywhere yet to gate behavior.
-- ============================================================

USE AuthDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemFeatureFlags')
CREATE TABLE SystemFeatureFlags (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Key]       NVARCHAR(200)    NOT NULL,
    IsEnabled   BIT              NOT NULL DEFAULT 0,
    Description NVARCHAR(500)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SystemFeatureFlags_Key')
    CREATE UNIQUE INDEX IX_SystemFeatureFlags_Key ON SystemFeatureFlags ([Key]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantFeatureFlagOverrides')
CREATE TABLE TenantFeatureFlagOverrides (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    [Key]       NVARCHAR(200)    NOT NULL,
    IsEnabled   BIT              NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_TenantFeatureFlagOverrides_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantFeatureFlagOverrides_TenantId_Key')
    CREATE UNIQUE INDEX IX_TenantFeatureFlagOverrides_TenantId_Key ON TenantFeatureFlagOverrides (TenantId, [Key]);
GO

-- No tenant backfill needed — empty by default, no flags exist until a SuperAdmin creates one.

-- ============================================================
-- New permissions: settings:feature-flags:{system|tenant}:{view|manage}.
-- SuperAdmin gets all 4; Admin gets the tenant-scoped 2.
-- ============================================================
DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';

DECLARE @PermFeatureFlagsSystemViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000058';
DECLARE @PermFeatureFlagsSystemManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000059';
DECLARE @PermFeatureFlagsTenantViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000060';
DECLARE @PermFeatureFlagsTenantManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000061';

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:feature-flags:system:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermFeatureFlagsSystemViewId, 'settings:feature-flags:system:view', 'View System Feature Flags', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:feature-flags:system:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermFeatureFlagsSystemManageId, 'settings:feature-flags:system:manage', 'Manage System Feature Flags', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:feature-flags:tenant:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermFeatureFlagsTenantViewId, 'settings:feature-flags:tenant:view', 'View Tenant Feature Flags', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:feature-flags:tenant:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermFeatureFlagsTenantManageId, 'settings:feature-flags:tenant:manage', 'Manage Tenant Feature Flags', 'Identity');

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermFeatureFlagsSystemViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermFeatureFlagsSystemViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermFeatureFlagsSystemManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermFeatureFlagsSystemManageId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermFeatureFlagsTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermFeatureFlagsTenantViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermFeatureFlagsTenantManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermFeatureFlagsTenantManageId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermFeatureFlagsTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermFeatureFlagsTenantViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermFeatureFlagsTenantManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermFeatureFlagsTenantManageId);
GO
