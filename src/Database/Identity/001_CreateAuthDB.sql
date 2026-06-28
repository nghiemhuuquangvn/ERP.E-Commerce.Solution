-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Security-enhanced: MFA, token rotation, audit logs, device tracking
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AuthDB')
    CREATE DATABASE AuthDB;
GO

USE AuthDB;
GO

-- ============================================================
-- Tenants (must exist before Users)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Tenants')
CREATE TABLE Tenants (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantCode      NVARCHAR(256)    NOT NULL,
    TenantName      NVARCHAR(256)    NOT NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_Tenants_TenantCode ON Tenants (TenantCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Users
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
CREATE TABLE Users (
    Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    Email                   NVARCHAR(256)    NOT NULL,
    UserName                NVARCHAR(256)    NULL,
    FullName                NVARCHAR(500)    NULL,
    PasswordHash            VARCHAR(500)     NOT NULL,
    PasswordSalt            VARCHAR(256)     NULL,
    PasswordChangedAt       DATETIME2        NULL,
    PhoneNumber             NVARCHAR(30)     NULL,
    IsPhoneConfirmed        BIT              NOT NULL DEFAULT 0,
    IsEmailConfirmed        BIT              NOT NULL DEFAULT 0,
    IsActive                BIT              NOT NULL DEFAULT 1,
    IsLocked                BIT              NOT NULL DEFAULT 0,
    LockoutEnd              DATETIME2        NULL,
    AccessFailedCount       INT              NOT NULL DEFAULT 0,
    TwoFactorEnabled        BIT              NOT NULL DEFAULT 0,
    LastLoginAt             DATETIME2        NULL,
    LastLoginIp             NVARCHAR(64)     NULL,
    UserTypeCode            NVARCHAR(100)    NOT NULL DEFAULT 'Customer',
    CreatedBy               NVARCHAR(256)    NULL,
    CreatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy               NVARCHAR(256)    NULL,
    UpdatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted               BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Users_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
CREATE UNIQUE INDEX IX_Users_Email         ON Users (Email)    WHERE IsDeleted = 0;
CREATE UNIQUE INDEX IX_Users_UserName      ON Users (UserName) WHERE IsDeleted = 0 AND UserName IS NOT NULL;
CREATE INDEX IX_Users_TenantId             ON Users (TenantId);
GO

-- ============================================================
-- Password History (prevent reuse of last 10 passwords)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PasswordHistories')
CREATE TABLE PasswordHistories (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    PasswordHash    VARCHAR(500)     NOT NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PasswordHistory_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_PasswordHistories_UserId ON PasswordHistories (UserId, CreatedAt DESC);
GO

-- ============================================================
-- Roles
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Roles')
CREATE TABLE Roles (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    RoleCode        NVARCHAR(256)    NOT NULL,
    RoleName        NVARCHAR(256)    NOT NULL,
    Description     NVARCHAR(1000)   NULL,
    IsSystem        BIT              NOT NULL DEFAULT 0,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_Roles_Code ON Roles (RoleCode, TenantId) WHERE IsDeleted = 0;
GO

-- ============================================================
-- Permissions
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Permissions')
CREATE TABLE Permissions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    PermissionCode  NVARCHAR(256)    NOT NULL,
    PermissionName  NVARCHAR(256)    NOT NULL,
    Module          NVARCHAR(100)    NOT NULL DEFAULT 'General',
    Description     NVARCHAR(500)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IX_Permissions_Code ON Permissions (PermissionCode) WHERE IsDeleted = 0;
GO

-- ============================================================
-- UserRoles, RolePermissions, UserPermissions
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserRoles')
CREATE TABLE UserRoles (
    UserId          UNIQUEIDENTIFIER NOT NULL,
    RoleId          UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    GrantedBy       NVARCHAR(256)    NULL,
    GrantedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY (UserId, RoleId, TenantId),
    CONSTRAINT FK_UserRoles_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Role FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RolePermissions')
CREATE TABLE RolePermissions (
    RoleId          UNIQUEIDENTIFIER NOT NULL,
    PermissionId    UNIQUEIDENTIFIER NOT NULL,
    GrantedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY (RoleId, PermissionId),
    CONSTRAINT FK_RolePerms_Role       FOREIGN KEY (RoleId)       REFERENCES Roles(Id)       ON DELETE CASCADE,
    CONSTRAINT FK_RolePerms_Permission FOREIGN KEY (PermissionId) REFERENCES Permissions(Id) ON DELETE CASCADE
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserPermissions')
CREATE TABLE UserPermissions (
    UserId          UNIQUEIDENTIFIER NOT NULL,
    PermissionId    UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    GrantedBy       NVARCHAR(256)    NULL,
    GrantedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY (UserId, PermissionId),
    CONSTRAINT FK_UserPerms_User       FOREIGN KEY (UserId)       REFERENCES Users(Id)       ON DELETE CASCADE,
    CONSTRAINT FK_UserPerms_Permission FOREIGN KEY (PermissionId) REFERENCES Permissions(Id) ON DELETE CASCADE
);
GO

-- ============================================================
-- UserTenants (multi-tenant membership)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserTenants')
CREATE TABLE UserTenants (
    UserId          UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    JoinedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDefault       BIT              NOT NULL DEFAULT 0,
    PRIMARY KEY (UserId, TenantId),
    CONSTRAINT FK_UserTenants_User   FOREIGN KEY (UserId)   REFERENCES Users(Id)   ON DELETE CASCADE,
    CONSTRAINT FK_UserTenants_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
);
GO

-- ============================================================
-- Refresh Tokens (with family rotation for theft detection)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RefreshTokens')
CREATE TABLE RefreshTokens (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Token           NVARCHAR(500)    NOT NULL,
    Family          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), -- all tokens in same rotation chain share a family
    ReplacedByToken NVARCHAR(500)    NULL,
    ExpiredAt       DATETIME2        NOT NULL,
    RevokedAt       DATETIME2        NULL,
    RevokedReason   NVARCHAR(200)    NULL,
    CreatedByIp     NVARCHAR(64)     NULL,
    UserAgent       NVARCHAR(500)    NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_RefreshTokens_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_RefreshTokens_Token  ON RefreshTokens (Token);
CREATE INDEX IX_RefreshTokens_UserId        ON RefreshTokens (UserId, IsActive);
CREATE INDEX IX_RefreshTokens_Family        ON RefreshTokens (Family);
CREATE INDEX IX_RefreshTokens_ExpiredAt     ON RefreshTokens (ExpiredAt) WHERE RevokedAt IS NULL;
GO

-- ============================================================
-- Token Blacklist (JWT revocation by JTI)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TokenBlacklist')
CREATE TABLE TokenBlacklist (
    Jti         NVARCHAR(200)   NOT NULL PRIMARY KEY,
    UserId      UNIQUEIDENTIFIER NOT NULL,
    ExpiresAt   DATETIME2        NOT NULL,
    RevokedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RevokedBy   NVARCHAR(256)    NULL
);
CREATE INDEX IX_TokenBlacklist_ExpiresAt ON TokenBlacklist (ExpiresAt);
CREATE INDEX IX_TokenBlacklist_UserId    ON TokenBlacklist (UserId);
GO

-- ============================================================
-- External Logins (OAuth2/Social login)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExternalLogins')
CREATE TABLE ExternalLogins (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    Provider        NVARCHAR(100)    NOT NULL, -- Google, Facebook, Microsoft
    ProviderKey     NVARCHAR(500)    NOT NULL,
    Email           NVARCHAR(256)    NULL,
    DisplayName     NVARCHAR(256)    NULL,
    AccessToken     NVARCHAR(MAX)    NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    LinkedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ExternalLogins_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_ExternalLogins_Provider ON ExternalLogins (Provider, ProviderKey) WHERE IsDeleted = 0;
CREATE INDEX IX_ExternalLogins_UserId          ON ExternalLogins (UserId);
GO

-- ============================================================
-- Email Confirmation Tokens
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmailConfirmTokens')
CREATE TABLE EmailConfirmTokens (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId      UNIQUEIDENTIFIER NOT NULL,
    Token       NVARCHAR(500)    NOT NULL,
    ExpiredAt   DATETIME2        NOT NULL,
    ConfirmedAt DATETIME2        NULL,
    IpAddress   NVARCHAR(64)     NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_EmailConfirm_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_EmailConfirmTokens_UserId ON EmailConfirmTokens (UserId, ExpiredAt);
GO

-- ============================================================
-- Password Reset Tokens
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PasswordResetTokens')
CREATE TABLE PasswordResetTokens (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId      UNIQUEIDENTIFIER NOT NULL,
    Token       NVARCHAR(500)    NOT NULL,
    ExpiredAt   DATETIME2        NOT NULL,
    UsedAt      DATETIME2        NULL,
    IpAddress   NVARCHAR(64)     NULL,
    UserAgent   NVARCHAR(500)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PasswordReset_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_PasswordResetTokens_Token  ON PasswordResetTokens (Token) WHERE UsedAt IS NULL;
CREATE INDEX IX_PasswordResetTokens_UserId         ON PasswordResetTokens (UserId, ExpiredAt);
GO

-- ============================================================
-- Two-Factor Authentication Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TwoFactorSettings')
CREATE TABLE TwoFactorSettings (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL UNIQUE,
    Method          INT              NOT NULL DEFAULT 0, -- 0=TOTP, 1=SMS, 2=Email
    SecretKey       NVARCHAR(500)    NULL,               -- TOTP secret (AES encrypted)
    BackupCodes     NVARCHAR(MAX)    NULL,               -- JSON array of hashed backup codes
    IsEnabled       BIT              NOT NULL DEFAULT 0,
    EnabledAt       DATETIME2        NULL,
    LastUsedAt      DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_2FA_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
GO

-- ============================================================
-- Security Audit Log (immutable — no UPDATE/DELETE)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SecurityAuditLogs')
CREATE TABLE SecurityAuditLogs (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId    UNIQUEIDENTIFIER NULL,
    UserId      UNIQUEIDENTIFIER NULL,
    Email       NVARCHAR(256)    NULL,
    EventType   NVARCHAR(100)    NOT NULL, -- Login, Logout, PasswordChange, PasswordReset, TokenRefresh, 2FAEnabled, RoleAssigned, AccountLocked
    IsSuccess   BIT              NOT NULL DEFAULT 1,
    IpAddress   NVARCHAR(64)     NULL,
    UserAgent   NVARCHAR(500)    NULL,
    Details     NVARCHAR(2000)   NULL,
    ErrorReason NVARCHAR(500)    NULL,
    OccurredAt  DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_AuditLogs_UserId     ON SecurityAuditLogs (UserId, OccurredAt DESC);
CREATE INDEX IX_AuditLogs_TenantId   ON SecurityAuditLogs (TenantId, OccurredAt DESC);
CREATE INDEX IX_AuditLogs_EventType  ON SecurityAuditLogs (EventType, OccurredAt DESC);
CREATE INDEX IX_AuditLogs_IpAddress  ON SecurityAuditLogs (IpAddress, OccurredAt DESC);
GO

-- ============================================================
-- Device Sessions (trusted devices per user)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeviceSessions')
CREATE TABLE DeviceSessions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    DeviceId        NVARCHAR(256)    NOT NULL,
    DeviceName      NVARCHAR(200)    NULL,
    DeviceType      NVARCHAR(50)     NULL, -- Desktop, Mobile, Tablet
    Platform        NVARCHAR(100)    NULL, -- Windows, iOS, Android, Web
    IpAddress       NVARCHAR(64)     NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    LastActiveAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_DeviceSessions_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_DeviceSessions_Device ON DeviceSessions (UserId, DeviceId);
CREATE INDEX IX_DeviceSessions_Active        ON DeviceSessions (UserId, IsActive, LastActiveAt DESC);
GO

-- ============================================================
-- Login Attempts (IP-based rate limiting)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LoginAttempts')
CREATE TABLE LoginAttempts (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    IpAddress       NVARCHAR(64)     NOT NULL,
    Email           NVARCHAR(256)    NULL,
    UserId          UNIQUEIDENTIFIER NULL,
    IsSuccessful    BIT              NOT NULL DEFAULT 0,
    FailureReason   NVARCHAR(200)    NULL,
    AttemptedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_LoginAttempts_IpAddress ON LoginAttempts (IpAddress, AttemptedAt DESC);
CREATE INDEX IX_LoginAttempts_Email     ON LoginAttempts (Email, AttemptedAt DESC) WHERE Email IS NOT NULL;
GO

-- ============================================================
-- Outbox Messages (for event publishing via Outbox pattern)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutboxMessages')
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
CREATE INDEX IX_OutboxMessages_Unprocessed ON OutboxMessages (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL;
GO

-- ============================================================
-- Seed: System roles & permissions
-- ============================================================
DECLARE @SystemTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Id = @SystemTenantId)
    INSERT INTO Tenants (Id, TenantCode, TenantName, IsActive)
    VALUES (@SystemTenantId, 'SYSTEM', 'System Tenant', 1);

-- System roles
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'SuperAdmin')
    INSERT INTO Roles (Id, TenantId, RoleCode, RoleName, IsSystem, IsActive)
    VALUES (NEWID(), @SystemTenantId, 'SuperAdmin', 'Super Administrator', 1, 1);

IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'Admin')
    INSERT INTO Roles (Id, TenantId, RoleCode, RoleName, IsSystem, IsActive)
    VALUES (NEWID(), @SystemTenantId, 'Admin', 'Administrator', 1, 1);

IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'Operator')
    INSERT INTO Roles (Id, TenantId, RoleCode, RoleName, IsSystem, IsActive)
    VALUES (NEWID(), @SystemTenantId, 'Operator', 'Operator', 1, 1);

IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'Viewer')
    INSERT INTO Roles (Id, TenantId, RoleCode, RoleName, IsSystem, IsActive)
    VALUES (NEWID(), @SystemTenantId, 'Viewer', 'Read-Only Viewer', 1, 1);

-- Core permissions
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'monitoring:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (NEWID(), 'monitoring:view', 'View System Monitoring', 'System');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'users:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (NEWID(), 'users:manage', 'Manage Users', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'roles:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (NEWID(), 'roles:manage', 'Manage Roles', 'Identity');
GO
