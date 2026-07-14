-- ============================================================
-- ShippingDB Migration 003 — Shipping Settings (System/Tenant two-tier)
-- Mirrors AuthDB's SystemAuthSettings/TenantAuthSettings pattern.
-- ============================================================

USE ShippingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemShippingSettings')
BEGIN
    CREATE TABLE SystemShippingSettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        CustomCarriersAllowed   BIT NOT NULL DEFAULT 1,
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantShippingSettings')
BEGIN
    CREATE TABLE TenantShippingSettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,
        DefaultShippingFee      DECIMAL(18,2) NOT NULL DEFAULT 0,
        FreeShippingThreshold   DECIMAL(18,2) NOT NULL DEFAULT 0,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantShippingSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantShippingSettings_TenantId ON TenantShippingSettings(TenantId);
GO
