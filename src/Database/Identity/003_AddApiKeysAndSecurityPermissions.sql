-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 003: API keys (service-to-service credentials) + permissions for the
-- new Security/API-key management endpoints. SecurityAuditLogs, LoginAttempts,
-- DeviceSessions, TokenBlacklist, PasswordHistories all already exist as of
-- 001_CreateAuthDB.sql — they only lacked a C# entity/DbSet until now, so no new
-- table is needed for those five.
-- ============================================================

USE AuthDB;
GO

-- ============================================================
-- API Keys (service-to-service credentials)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ApiKeys')
CREATE TABLE ApiKeys (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    KeyPrefix       NVARCHAR(16)     NOT NULL,
    KeyHash         VARCHAR(500)     NOT NULL,
    Scopes          NVARCHAR(500)    NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    ExpiresAt       DATETIME2        NULL,
    RevokedAt       DATETIME2        NULL,
    LastUsedAt      DATETIME2        NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ApiKeys_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
CREATE UNIQUE INDEX IX_ApiKeys_KeyPrefix ON ApiKeys (KeyPrefix);
CREATE INDEX IX_ApiKeys_TenantId ON ApiKeys (TenantId);
GO

-- ============================================================
-- New permissions: security:audit:view, security:apikeys:view/manage
-- SuperAdmin gets all 3; Admin gets the two "view" pairs matching how
-- settings:tenant:* was scoped for Admin in migration 002.
-- ============================================================
DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';

DECLARE @PermAuditViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000009';
DECLARE @PermApiKeysViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000000A';
DECLARE @PermApiKeysManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-00000000000B';

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'security:audit:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermAuditViewId, 'security:audit:view', 'View Security Audit Logs', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'security:apikeys:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermApiKeysViewId, 'security:apikeys:view', 'View API Keys', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'security:apikeys:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermApiKeysManageId, 'security:apikeys:manage', 'Manage API Keys', 'Identity');

-- Grant all 3 to SuperAdmin
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermAuditViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermAuditViewId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermApiKeysViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermApiKeysViewId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermApiKeysManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermApiKeysManageId);

-- Grant view + manage to Admin (tenant-scoped administrators still need to issue their own service keys)
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermApiKeysViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermApiKeysViewId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermApiKeysManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermApiKeysManageId);
GO
