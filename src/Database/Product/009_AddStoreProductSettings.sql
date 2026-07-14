-- ============================================================
-- ProductDB Migration 009 — Store/Product Settings (System/Tenant two-tier)
-- Mirrors AuthDB's SystemAuthSettings/TenantAuthSettings pattern.
-- ============================================================

USE ProductDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemStoreProductSettings')
BEGIN
    CREATE TABLE SystemStoreProductSettings
    (
        Id                              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        PriceDisplayIncludesTaxByDefault BIT NOT NULL DEFAULT 1,
        ProductReviewsEnabled           BIT NOT NULL DEFAULT 1,
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantStoreProductSettings')
BEGIN
    CREATE TABLE TenantStoreProductSettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,
        StoreName               NVARCHAR(200) NOT NULL DEFAULT '',
        StoreDescription        NVARCHAR(2000) NULL,
        LogoUrl                 NVARCHAR(500) NULL,
        ShowOutOfStockProducts  BIT NOT NULL DEFAULT 1,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantStoreProductSettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantStoreProductSettings_TenantId ON TenantStoreProductSettings(TenantId);
GO
