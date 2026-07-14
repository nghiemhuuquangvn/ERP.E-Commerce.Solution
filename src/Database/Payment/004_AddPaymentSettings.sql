-- ============================================================
-- PaymentDB Migration 004 — Payment Settings (System/Tenant two-tier)
-- Mirrors AuthDB's SystemAuthSettings/TenantAuthSettings pattern.
-- VnPaySecretKeyEncrypted stores AES-256-CBC ciphertext (ERP.Shared.Security.
-- AesSecretProtector) — never plaintext.
-- ============================================================

USE PaymentDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemPaymentSettings')
BEGIN
    CREATE TABLE SystemPaymentSettings
    (
        Id                              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        VnPayAvailable                  BIT NOT NULL DEFAULT 1,
        ManualBankTransferAvailable     BIT NOT NULL DEFAULT 1,
        Description                     NVARCHAR(MAX) NULL,
        IsDeleted                       BIT NOT NULL DEFAULT 0,
        CreatedBy                       NVARCHAR(200) NULL,
        CreatedAt                       DATETIME2 NOT NULL,
        UpdatedBy                       NVARCHAR(200) NULL,
        UpdatedAt                       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion                      ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantPaymentSettings')
BEGIN
    CREATE TABLE TenantPaymentSettings
    (
        Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER NOT NULL,
        VnPayEnabled                BIT NOT NULL DEFAULT 0,
        VnPaySecretKeyEncrypted     NVARCHAR(500) NULL,
        SandboxMode                 BIT NOT NULL DEFAULT 1,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantPaymentSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantPaymentSettings_TenantId ON TenantPaymentSettings(TenantId);
GO
