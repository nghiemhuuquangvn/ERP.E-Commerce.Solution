-- ============================================================
-- NotificationDB Migration 002 — Notification/Email/SMS Settings (System/Tenant two-tier)
-- Mirrors AuthDB's SystemAuthSettings/TenantAuthSettings pattern (guarded CREATE TABLE,
-- unlike 001_CreateNotificationDB.sql's unguarded style — safer to re-run).
-- SmtpPasswordEncrypted/ApiKeyEncrypted store AES-256-CBC ciphertext
-- (ERP.Shared.Security.AesSecretProtector) — never plaintext.
-- ============================================================

USE NotificationDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemNotificationSettings')
BEGIN
    CREATE TABLE SystemNotificationSettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        EmailChannelAvailable   BIT NOT NULL DEFAULT 1,
        SmsChannelAvailable     BIT NOT NULL DEFAULT 1,
        PushChannelAvailable    BIT NOT NULL DEFAULT 1,
        Description             NVARCHAR(MAX) NULL,
        IsDeleted               BIT NOT NULL DEFAULT 0,
        CreatedBy               NVARCHAR(200) NULL,
        CreatedAt               DATETIME2 NOT NULL,
        UpdatedBy               NVARCHAR(200) NULL,
        UpdatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion              ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantNotificationSettings')
BEGIN
    CREATE TABLE TenantNotificationSettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,
        EmailEnabledByDefault   BIT NOT NULL DEFAULT 1,
        SmsEnabledByDefault     BIT NOT NULL DEFAULT 0,
        PushEnabledByDefault    BIT NOT NULL DEFAULT 0,
        Description             NVARCHAR(MAX) NULL,
        IsDeleted               BIT NOT NULL DEFAULT 0,
        CreatedBy               NVARCHAR(200) NULL,
        CreatedAt               DATETIME2 NOT NULL,
        UpdatedBy               NVARCHAR(200) NULL,
        UpdatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion              ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemEmailSettings')
BEGIN
    CREATE TABLE SystemEmailSettings
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        CustomSmtpAllowed   BIT NOT NULL DEFAULT 1,
        Description         NVARCHAR(MAX) NULL,
        IsDeleted           BIT NOT NULL DEFAULT 0,
        CreatedBy           NVARCHAR(200) NULL,
        CreatedAt           DATETIME2 NOT NULL,
        UpdatedBy           NVARCHAR(200) NULL,
        UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantEmailSettings')
BEGIN
    CREATE TABLE TenantEmailSettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,
        SmtpHost                NVARCHAR(255) NULL,
        SmtpPort                INT NOT NULL DEFAULT 587,
        SmtpUsername            NVARCHAR(255) NULL,
        SmtpPasswordEncrypted   NVARCHAR(500) NULL,
        FromAddress             NVARCHAR(320) NULL,
        FromName                NVARCHAR(200) NULL,
        Description             NVARCHAR(MAX) NULL,
        IsDeleted               BIT NOT NULL DEFAULT 0,
        CreatedBy               NVARCHAR(200) NULL,
        CreatedAt               DATETIME2 NOT NULL,
        UpdatedBy               NVARCHAR(200) NULL,
        UpdatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion              ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemSmsSettings')
BEGIN
    CREATE TABLE SystemSmsSettings
    (
        Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        CustomSmsGatewayAllowed     BIT NOT NULL DEFAULT 1,
        Description                 NVARCHAR(MAX) NULL,
        IsDeleted                   BIT NOT NULL DEFAULT 0,
        CreatedBy                   NVARCHAR(200) NULL,
        CreatedAt                   DATETIME2 NOT NULL,
        UpdatedBy                   NVARCHAR(200) NULL,
        UpdatedAt                   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion                  ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantSmsSettings')
BEGIN
    CREATE TABLE TenantSmsSettings
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        ProviderName        NVARCHAR(100) NULL,
        ApiKeyEncrypted     NVARCHAR(500) NULL,
        FromNumber          NVARCHAR(32) NULL,
        Description         NVARCHAR(MAX) NULL,
        IsDeleted           BIT NOT NULL DEFAULT 0,
        CreatedBy           NVARCHAR(200) NULL,
        CreatedAt           DATETIME2 NOT NULL,
        UpdatedBy           NVARCHAR(200) NULL,
        UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantNotificationSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantNotificationSettings_TenantId ON TenantNotificationSettings(TenantId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantEmailSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantEmailSettings_TenantId ON TenantEmailSettings(TenantId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantSmsSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantSmsSettings_TenantId ON TenantSmsSettings(TenantId);
GO
