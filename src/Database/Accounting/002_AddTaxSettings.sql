-- ============================================================
-- AccountingDB Migration 002 — Tax Settings (System/Tenant two-tier)
-- Mirrors AuthDB's SystemAuthSettings/TenantAuthSettings pattern.
-- Distinct from the existing TaxRates table (master-data list of tax rates) —
-- this is company-level tax policy configuration.
-- ============================================================

USE AccountingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemTaxSettings')
BEGIN
    CREATE TABLE SystemTaxSettings
    (
        Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        RegionalTaxConfigAllowed    BIT NOT NULL DEFAULT 1,
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantTaxSettings')
BEGIN
    CREATE TABLE TenantTaxSettings
    (
        Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER NOT NULL,
        CompanyTaxCode              NVARCHAR(50) NULL,
        DefaultTaxRatePercent       DECIMAL(8,4) NOT NULL DEFAULT 0,
        PricesIncludeTax            BIT NOT NULL DEFAULT 1,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantTaxSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantTaxSettings_TenantId ON TenantTaxSettings(TenantId);
GO
