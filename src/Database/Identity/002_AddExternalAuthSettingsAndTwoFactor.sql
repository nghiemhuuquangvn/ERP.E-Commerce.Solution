-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 002: External-login system/tenant settings + 2FA challenges + OAuth PKCE state
-- ============================================================

USE AuthDB;
GO

-- ============================================================
-- Users: add TwoFactorMethod (PhoneNumber, TwoFactorEnabled already exist from 001)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'TwoFactorMethod')
    ALTER TABLE Users ADD TwoFactorMethod NVARCHAR(16) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PhoneNumber')
    ALTER TABLE Users ADD PhoneNumber NVARCHAR(32) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'TwoFactorEnabled')
    ALTER TABLE Users ADD TwoFactorEnabled BIT NOT NULL DEFAULT 0;
GO

-- ============================================================
-- SystemAuthSettings (single well-known-GUID row)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemAuthSettings')
CREATE TABLE SystemAuthSettings (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description         NVARCHAR(MAX)    NULL,
    GoogleEnabled       BIT              NOT NULL DEFAULT 1,
    MicrosoftEnabled    BIT              NOT NULL DEFAULT 1,
    FacebookEnabled     BIT              NOT NULL DEFAULT 1,
    TwitterEnabled      BIT              NOT NULL DEFAULT 1,
    TwoFactorEnabled    BIT              NOT NULL DEFAULT 1,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemAuthSettingsId UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemAuthSettings WHERE Id = @SystemAuthSettingsId)
    INSERT INTO SystemAuthSettings (Id, GoogleEnabled, MicrosoftEnabled, FacebookEnabled, TwitterEnabled, TwoFactorEnabled)
    VALUES (@SystemAuthSettingsId, 1, 1, 1, 1, 1);
GO

-- ============================================================
-- TenantAuthSettings (one row per tenant; backfilled below)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantAuthSettings')
CREATE TABLE TenantAuthSettings (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Description         NVARCHAR(MAX)    NULL,
    GoogleEnabled       BIT              NOT NULL DEFAULT 1,
    MicrosoftEnabled    BIT              NOT NULL DEFAULT 1,
    FacebookEnabled     BIT              NOT NULL DEFAULT 1,
    TwitterEnabled      BIT              NOT NULL DEFAULT 1,
    TwoFactorEnabled    BIT              NOT NULL DEFAULT 1,
    TwoFactorRequired   BIT              NOT NULL DEFAULT 0,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantAuthSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
CREATE UNIQUE INDEX IX_TenantAuthSettings_TenantId ON TenantAuthSettings (TenantId);
GO

-- Backfill one default row per existing tenant
INSERT INTO TenantAuthSettings (Id, TenantId, GoogleEnabled, MicrosoftEnabled, FacebookEnabled, TwitterEnabled, TwoFactorEnabled, TwoFactorRequired)
SELECT NEWID(), t.Id, 1, 1, 1, 1, 1, 0
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantAuthSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- TwoFactorChallenges
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TwoFactorChallenges')
CREATE TABLE TwoFactorChallenges (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    CodeHash        NVARCHAR(500)    NOT NULL,
    Method          NVARCHAR(16)     NOT NULL,
    Destination     NVARCHAR(256)    NULL,
    ExpiredAt       DATETIME2        NOT NULL,
    AttemptCount    INT              NOT NULL DEFAULT 0,
    MaxAttempts     INT              NOT NULL DEFAULT 3,
    VerifiedAt      DATETIME2        NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TwoFactorChallenges_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_TwoFactorChallenges_UserId_ExpiredAt ON TwoFactorChallenges (UserId, ExpiredAt);
GO

-- ============================================================
-- OAuthStates (Twitter Authorization Code + PKCE flow)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OAuthStates')
CREATE TABLE OAuthStates (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    State           NVARCHAR(256)    NOT NULL,
    CodeVerifier    NVARCHAR(256)    NOT NULL,
    TenantCode      NVARCHAR(256)    NOT NULL,
    ExpiredAt       DATETIME2        NOT NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_OAuthStates_State ON OAuthStates (State);
GO

-- ============================================================
-- New permissions: settings:system:view/manage, settings:tenant:view/manage
-- SuperAdmin gets all 4; Admin gets only the tenant pair.
-- ============================================================
DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';

DECLARE @PermSettingsSystemViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000005';
DECLARE @PermSettingsSystemManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000006';
DECLARE @PermSettingsTenantViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000007';
DECLARE @PermSettingsTenantManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000008';

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:system:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermSettingsSystemViewId, 'settings:system:view', 'View System Auth Settings', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:system:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermSettingsSystemManageId, 'settings:system:manage', 'Manage System Auth Settings', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:tenant:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermSettingsTenantViewId, 'settings:tenant:view', 'View Tenant Auth Settings', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:tenant:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermSettingsTenantManageId, 'settings:tenant:manage', 'Manage Tenant Auth Settings', 'Identity');

-- Grant all 4 to SuperAdmin
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermSettingsSystemViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermSettingsSystemViewId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermSettingsSystemManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermSettingsSystemManageId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermSettingsTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermSettingsTenantViewId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermSettingsTenantManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermSettingsTenantManageId);

-- Grant only the tenant pair to Admin
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermSettingsTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermSettingsTenantViewId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermSettingsTenantManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermSettingsTenantManageId);
GO
