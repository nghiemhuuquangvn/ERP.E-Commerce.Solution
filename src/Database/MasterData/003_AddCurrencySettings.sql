-- ============================================================
-- MasterDataDB Migration 003 — Currency Settings (System/Tenant two-tier)
-- Mirrors AuthDB's SystemAuthSettings/TenantAuthSettings pattern.
-- ============================================================

USE MasterDataDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemCurrencySettings')
BEGIN
    CREATE TABLE SystemCurrencySettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        MultiCurrencyAllowed    BIT NOT NULL DEFAULT 1,
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantCurrencySettings')
BEGIN
    CREATE TABLE TenantCurrencySettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,
        DefaultCurrencyCode     NVARCHAR(3) NOT NULL DEFAULT 'USD',
        ExchangeRateSource      NVARCHAR(50) NOT NULL DEFAULT 'Manual',
        DecimalDigits           INT NOT NULL DEFAULT 2,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantCurrencySettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantCurrencySettings_TenantId ON TenantCurrencySettings(TenantId);
GO
