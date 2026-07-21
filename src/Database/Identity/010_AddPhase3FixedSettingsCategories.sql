-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 010: Phase 3 fixed-shape Settings categories (System/Tenant two-tier) —
-- Workflow, File Storage, Scheduler, Integration, API, Audit, Advanced, Localization.
-- Storage+CRUD only — not yet wired into runtime behavior.
-- ============================================================

USE AuthDB;
GO

-- Required for INSERTs into tables with filtered indexes; some sqlcmd
-- sessions don't inherit ON even when the DB-level default is ON.
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Workflow Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemWorkflowSettings')
CREATE TABLE SystemWorkflowSettings (
    Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description                 NVARCHAR(MAX)    NULL,
    MultiLevelApprovalAvailable BIT              NOT NULL DEFAULT 1,
    CreatedBy                   NVARCHAR(256)    NULL,
    CreatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy                   NVARCHAR(256)    NULL,
    UpdatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted                   BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemWorkflowSettingsId UNIQUEIDENTIFIER = 'E1000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemWorkflowSettings WHERE Id = @SystemWorkflowSettingsId)
    INSERT INTO SystemWorkflowSettings (Id, MultiLevelApprovalAvailable)
    VALUES (@SystemWorkflowSettingsId, 1);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantWorkflowSettings')
CREATE TABLE TenantWorkflowSettings (
    Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId                    UNIQUEIDENTIFIER NOT NULL,
    Description                 NVARCHAR(MAX)    NULL,
    MultiLevelApprovalEnabled   BIT              NOT NULL DEFAULT 0,
    DefaultApprovalLevels       INT              NOT NULL DEFAULT 1,
    AutoApproveThresholdAmount  DECIMAL(18,2)    NULL,
    CreatedBy                   NVARCHAR(256)    NULL,
    CreatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy                   NVARCHAR(256)    NULL,
    UpdatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted                   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantWorkflowSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantWorkflowSettings_TenantId')
    CREATE UNIQUE INDEX IX_TenantWorkflowSettings_TenantId ON TenantWorkflowSettings (TenantId);
GO

INSERT INTO TenantWorkflowSettings (Id, TenantId, MultiLevelApprovalEnabled, DefaultApprovalLevels)
SELECT NEWID(), t.Id, 0, 1
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantWorkflowSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- File Storage Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemFileStorageSettings')
CREATE TABLE SystemFileStorageSettings (
    Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description                 NVARCHAR(MAX)    NULL,
    CustomStorageProviderAllowed BIT             NOT NULL DEFAULT 1,
    DefaultMaxUploadSizeMb      INT              NOT NULL DEFAULT 10,
    CreatedBy                   NVARCHAR(256)    NULL,
    CreatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy                   NVARCHAR(256)    NULL,
    UpdatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted                   BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemFileStorageSettingsId UNIQUEIDENTIFIER = 'E2000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemFileStorageSettings WHERE Id = @SystemFileStorageSettingsId)
    INSERT INTO SystemFileStorageSettings (Id, CustomStorageProviderAllowed, DefaultMaxUploadSizeMb)
    VALUES (@SystemFileStorageSettingsId, 1, 10);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantFileStorageSettings')
CREATE TABLE TenantFileStorageSettings (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    Provider        NVARCHAR(50)     NOT NULL DEFAULT 'Local',
    MaxUploadSizeMb INT              NOT NULL DEFAULT 10,
    CdnBaseUrl      NVARCHAR(500)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantFileStorageSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantFileStorageSettings_TenantId')
    CREATE UNIQUE INDEX IX_TenantFileStorageSettings_TenantId ON TenantFileStorageSettings (TenantId);
GO

INSERT INTO TenantFileStorageSettings (Id, TenantId, Provider, MaxUploadSizeMb)
SELECT NEWID(), t.Id, 'Local', 10
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantFileStorageSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- Scheduler Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemSchedulerSettings')
CREATE TABLE SystemSchedulerSettings (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description           NVARCHAR(MAX)    NULL,
    BackgroundJobsEnabled BIT              NOT NULL DEFAULT 1,
    CreatedBy             NVARCHAR(256)    NULL,
    CreatedAt             DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy             NVARCHAR(256)    NULL,
    UpdatedAt             DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted             BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemSchedulerSettingsId UNIQUEIDENTIFIER = 'E3000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemSchedulerSettings WHERE Id = @SystemSchedulerSettingsId)
    INSERT INTO SystemSchedulerSettings (Id, BackgroundJobsEnabled)
    VALUES (@SystemSchedulerSettingsId, 1);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantSchedulerSettings')
CREATE TABLE TenantSchedulerSettings (
    Id                              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId                        UNIQUEIDENTIFIER NOT NULL,
    Description                     NVARCHAR(MAX)    NULL,
    OutboxPollingIntervalSeconds    INT              NOT NULL DEFAULT 30,
    MaxRetryAttempts                INT              NOT NULL DEFAULT 5,
    CreatedBy                       NVARCHAR(256)    NULL,
    CreatedAt                       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy                       NVARCHAR(256)    NULL,
    UpdatedAt                       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted                       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantSchedulerSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantSchedulerSettings_TenantId')
    CREATE UNIQUE INDEX IX_TenantSchedulerSettings_TenantId ON TenantSchedulerSettings (TenantId);
GO

INSERT INTO TenantSchedulerSettings (Id, TenantId, OutboxPollingIntervalSeconds, MaxRetryAttempts)
SELECT NEWID(), t.Id, 30, 5
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantSchedulerSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- Integration Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemIntegrationSettings')
CREATE TABLE SystemIntegrationSettings (
    Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description                 NVARCHAR(MAX)    NULL,
    ExternalIntegrationsAllowed BIT              NOT NULL DEFAULT 1,
    CreatedBy                   NVARCHAR(256)    NULL,
    CreatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy                   NVARCHAR(256)    NULL,
    UpdatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted                   BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemIntegrationSettingsId UNIQUEIDENTIFIER = 'E4000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemIntegrationSettings WHERE Id = @SystemIntegrationSettingsId)
    INSERT INTO SystemIntegrationSettings (Id, ExternalIntegrationsAllowed)
    VALUES (@SystemIntegrationSettingsId, 1);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantIntegrationSettings')
CREATE TABLE TenantIntegrationSettings (
    Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    Description             NVARCHAR(MAX)    NULL,
    ExternalApiBaseUrl      NVARCHAR(500)    NULL,
    ExternalApiKeyEncrypted NVARCHAR(500)    NULL,
    SyncEnabled             BIT              NOT NULL DEFAULT 0,
    CreatedBy               NVARCHAR(256)    NULL,
    CreatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy               NVARCHAR(256)    NULL,
    UpdatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted               BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantIntegrationSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantIntegrationSettings_TenantId')
    CREATE UNIQUE INDEX IX_TenantIntegrationSettings_TenantId ON TenantIntegrationSettings (TenantId);
GO

INSERT INTO TenantIntegrationSettings (Id, TenantId, SyncEnabled)
SELECT NEWID(), t.Id, 0
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantIntegrationSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- API Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemApiSettings')
CREATE TABLE SystemApiSettings (
    Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description                 NVARCHAR(MAX)    NULL,
    ApiKeysAllowed              BIT              NOT NULL DEFAULT 1,
    DefaultRateLimitPerMinute   INT              NOT NULL DEFAULT 60,
    CreatedBy                   NVARCHAR(256)    NULL,
    CreatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy                   NVARCHAR(256)    NULL,
    UpdatedAt                   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted                   BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemApiSettingsId UNIQUEIDENTIFIER = 'E5000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemApiSettings WHERE Id = @SystemApiSettingsId)
    INSERT INTO SystemApiSettings (Id, ApiKeysAllowed, DefaultRateLimitPerMinute)
    VALUES (@SystemApiSettingsId, 1, 60);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantApiSettings')
CREATE TABLE TenantApiSettings (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Description         NVARCHAR(MAX)    NULL,
    RateLimitPerMinute  INT              NOT NULL DEFAULT 60,
    MaxActiveApiKeys    INT              NOT NULL DEFAULT 5,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantApiSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantApiSettings_TenantId')
    CREATE UNIQUE INDEX IX_TenantApiSettings_TenantId ON TenantApiSettings (TenantId);
GO

INSERT INTO TenantApiSettings (Id, TenantId, RateLimitPerMinute, MaxActiveApiKeys)
SELECT NEWID(), t.Id, 60, 5
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantApiSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- Audit Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemAuditSettings')
CREATE TABLE SystemAuditSettings (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description         NVARCHAR(MAX)    NULL,
    AuditLoggingEnabled BIT              NOT NULL DEFAULT 1,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemAuditSettingsId UNIQUEIDENTIFIER = 'E6000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemAuditSettings WHERE Id = @SystemAuditSettingsId)
    INSERT INTO SystemAuditSettings (Id, AuditLoggingEnabled)
    VALUES (@SystemAuditSettingsId, 1);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantAuditSettings')
CREATE TABLE TenantAuditSettings (
    Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    Description             NVARCHAR(MAX)    NULL,
    RetentionDays           INT              NOT NULL DEFAULT 90,
    LogSuccessfulLogins     BIT              NOT NULL DEFAULT 1,
    LogFailedLogins         BIT              NOT NULL DEFAULT 1,
    LogPermissionChanges    BIT              NOT NULL DEFAULT 1,
    CreatedBy               NVARCHAR(256)    NULL,
    CreatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy               NVARCHAR(256)    NULL,
    UpdatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted               BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantAuditSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantAuditSettings_TenantId')
    CREATE UNIQUE INDEX IX_TenantAuditSettings_TenantId ON TenantAuditSettings (TenantId);
GO

INSERT INTO TenantAuditSettings (Id, TenantId, RetentionDays, LogSuccessfulLogins, LogFailedLogins, LogPermissionChanges)
SELECT NEWID(), t.Id, 90, 1, 1, 1
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantAuditSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- Advanced Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemAdvancedSettings')
CREATE TABLE SystemAdvancedSettings (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description       NVARCHAR(MAX)    NULL,
    DebugModeAllowed  BIT              NOT NULL DEFAULT 0,
    CreatedBy         NVARCHAR(256)    NULL,
    CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy         NVARCHAR(256)    NULL,
    UpdatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemAdvancedSettingsId UNIQUEIDENTIFIER = 'E7000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemAdvancedSettings WHERE Id = @SystemAdvancedSettingsId)
    INSERT INTO SystemAdvancedSettings (Id, DebugModeAllowed)
    VALUES (@SystemAdvancedSettingsId, 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantAdvancedSettings')
CREATE TABLE TenantAdvancedSettings (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Description         NVARCHAR(MAX)    NULL,
    DebugModeEnabled    BIT              NOT NULL DEFAULT 0,
    CustomCssUrl        NVARCHAR(500)    NULL,
    DataExportEnabled   BIT              NOT NULL DEFAULT 1,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantAdvancedSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantAdvancedSettings_TenantId')
    CREATE UNIQUE INDEX IX_TenantAdvancedSettings_TenantId ON TenantAdvancedSettings (TenantId);
GO

INSERT INTO TenantAdvancedSettings (Id, TenantId, DebugModeEnabled, DataExportEnabled)
SELECT NEWID(), t.Id, 0, 1
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantAdvancedSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- Localization Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemLocalizationSettings')
CREATE TABLE SystemLocalizationSettings (
    Id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description           NVARCHAR(MAX)    NULL,
    MultiLanguageAllowed  BIT              NOT NULL DEFAULT 1,
    DefaultLocale         NVARCHAR(20)     NOT NULL DEFAULT 'en-US',
    CreatedBy             NVARCHAR(256)    NULL,
    CreatedAt             DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy             NVARCHAR(256)    NULL,
    UpdatedAt             DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted             BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemLocalizationSettingsId UNIQUEIDENTIFIER = 'E8000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemLocalizationSettings WHERE Id = @SystemLocalizationSettingsId)
    INSERT INTO SystemLocalizationSettings (Id, MultiLanguageAllowed, DefaultLocale)
    VALUES (@SystemLocalizationSettingsId, 1, 'en-US');
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantLocalizationSettings')
CREATE TABLE TenantLocalizationSettings (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Description         NVARCHAR(MAX)    NULL,
    DefaultLocale       NVARCHAR(20)     NOT NULL DEFAULT 'en-US',
    SupportedLocales    NVARCHAR(500)    NOT NULL DEFAULT 'en-US',
    NumberFormatLocale  NVARCHAR(20)     NOT NULL DEFAULT 'en-US',
    RtlEnabled          BIT              NOT NULL DEFAULT 0,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantLocalizationSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantLocalizationSettings_TenantId')
    CREATE UNIQUE INDEX IX_TenantLocalizationSettings_TenantId ON TenantLocalizationSettings (TenantId);
GO

INSERT INTO TenantLocalizationSettings (Id, TenantId, DefaultLocale, SupportedLocales, NumberFormatLocale, RtlEnabled)
SELECT NEWID(), t.Id, 'en-US', 'en-US', 'en-US', 0
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantLocalizationSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- New permissions: settings:{category}:{system|tenant}:{view|manage} for the 8 categories
-- above. SuperAdmin gets all 32; Admin gets the tenant-scoped 16.
-- ============================================================
DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';

DECLARE @Perms TABLE (PermId UNIQUEIDENTIFIER, Code NVARCHAR(200), Name NVARCHAR(200), IsTenantScoped BIT);
INSERT INTO @Perms (PermId, Code, Name, IsTenantScoped) VALUES
    ('30000000-0000-0000-0000-000000000026', 'settings:workflow:system:view',      'View System Workflow Settings',      0),
    ('30000000-0000-0000-0000-000000000027', 'settings:workflow:system:manage',    'Manage System Workflow Settings',    0),
    ('30000000-0000-0000-0000-000000000028', 'settings:workflow:tenant:view',      'View Tenant Workflow Settings',      1),
    ('30000000-0000-0000-0000-000000000029', 'settings:workflow:tenant:manage',    'Manage Tenant Workflow Settings',    1),
    ('30000000-0000-0000-0000-000000000030', 'settings:file-storage:system:view',   'View System File Storage Settings',   0),
    ('30000000-0000-0000-0000-000000000031', 'settings:file-storage:system:manage', 'Manage System File Storage Settings', 0),
    ('30000000-0000-0000-0000-000000000032', 'settings:file-storage:tenant:view',   'View Tenant File Storage Settings',   1),
    ('30000000-0000-0000-0000-000000000033', 'settings:file-storage:tenant:manage', 'Manage Tenant File Storage Settings', 1),
    ('30000000-0000-0000-0000-000000000034', 'settings:scheduler:system:view',      'View System Scheduler Settings',      0),
    ('30000000-0000-0000-0000-000000000035', 'settings:scheduler:system:manage',    'Manage System Scheduler Settings',    0),
    ('30000000-0000-0000-0000-000000000036', 'settings:scheduler:tenant:view',      'View Tenant Scheduler Settings',      1),
    ('30000000-0000-0000-0000-000000000037', 'settings:scheduler:tenant:manage',    'Manage Tenant Scheduler Settings',    1),
    ('30000000-0000-0000-0000-000000000038', 'settings:integration:system:view',    'View System Integration Settings',    0),
    ('30000000-0000-0000-0000-000000000039', 'settings:integration:system:manage',  'Manage System Integration Settings',  0),
    ('30000000-0000-0000-0000-000000000040', 'settings:integration:tenant:view',    'View Tenant Integration Settings',    1),
    ('30000000-0000-0000-0000-000000000041', 'settings:integration:tenant:manage',  'Manage Tenant Integration Settings',  1),
    ('30000000-0000-0000-0000-000000000042', 'settings:api:system:view',            'View System API Settings',            0),
    ('30000000-0000-0000-0000-000000000043', 'settings:api:system:manage',          'Manage System API Settings',          0),
    ('30000000-0000-0000-0000-000000000044', 'settings:api:tenant:view',            'View Tenant API Settings',            1),
    ('30000000-0000-0000-0000-000000000045', 'settings:api:tenant:manage',          'Manage Tenant API Settings',          1),
    ('30000000-0000-0000-0000-000000000046', 'settings:audit:system:view',          'View System Audit Settings',          0),
    ('30000000-0000-0000-0000-000000000047', 'settings:audit:system:manage',        'Manage System Audit Settings',        0),
    ('30000000-0000-0000-0000-000000000048', 'settings:audit:tenant:view',          'View Tenant Audit Settings',          1),
    ('30000000-0000-0000-0000-000000000049', 'settings:audit:tenant:manage',        'Manage Tenant Audit Settings',        1),
    ('30000000-0000-0000-0000-000000000050', 'settings:advanced:system:view',       'View System Advanced Settings',       0),
    ('30000000-0000-0000-0000-000000000051', 'settings:advanced:system:manage',     'Manage System Advanced Settings',     0),
    ('30000000-0000-0000-0000-000000000052', 'settings:advanced:tenant:view',       'View Tenant Advanced Settings',       1),
    ('30000000-0000-0000-0000-000000000053', 'settings:advanced:tenant:manage',     'Manage Tenant Advanced Settings',     1),
    ('30000000-0000-0000-0000-000000000054', 'settings:localization:system:view',   'View System Localization Settings',   0),
    ('30000000-0000-0000-0000-000000000055', 'settings:localization:system:manage', 'Manage System Localization Settings', 0),
    ('30000000-0000-0000-0000-000000000056', 'settings:localization:tenant:view',   'View Tenant Localization Settings',   1),
    ('30000000-0000-0000-0000-000000000057', 'settings:localization:tenant:manage', 'Manage Tenant Localization Settings', 1);

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT p.PermId, p.Code, p.Name, 'Identity'
FROM @Perms p
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = p.Code);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT @SuperAdminRoleId, p.PermId
FROM @Perms p
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = p.PermId);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT @AdminRoleId, p.PermId
FROM @Perms p
WHERE p.IsTenantScoped = 1
  AND NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = p.PermId);
GO
