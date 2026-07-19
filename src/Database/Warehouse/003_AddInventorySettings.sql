-- ============================================================
-- WarehouseDB Migration 003 — Inventory Settings (System/Tenant two-tier)
-- Mirrors AuthDB's SystemAuthSettings/TenantAuthSettings pattern.
-- ============================================================

USE WarehouseDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemInventorySettings')
BEGIN
    CREATE TABLE SystemInventorySettings
    (
        Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
        NegativeStockAllowed    BIT NOT NULL DEFAULT 0,
        Description             NVARCHAR(MAX) NULL,
        IsDeleted               BIT NOT NULL DEFAULT 0,
        CreatedBy               NVARCHAR(200) NULL,
        CreatedAt               DATETIME2 NOT NULL,
        UpdatedBy               NVARCHAR(200) NULL,
        UpdatedAt               DATETIME2 NULL,
        RowVersion              ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantInventorySettings')
BEGIN
    CREATE TABLE TenantInventorySettings
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        LowStockThreshold   INT NOT NULL DEFAULT 10,
        BackorderAllowed    BIT NOT NULL DEFAULT 0,
        DefaultHoldDays     INT NOT NULL DEFAULT 7,
        Description         NVARCHAR(MAX) NULL,
        IsDeleted           BIT NOT NULL DEFAULT 0,
        CreatedBy           NVARCHAR(200) NULL,
        CreatedAt           DATETIME2 NOT NULL,
        UpdatedBy           NVARCHAR(200) NULL,
        UpdatedAt           DATETIME2 NULL,
        RowVersion          ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantInventorySettings_TenantId')
    CREATE UNIQUE INDEX UX_TenantInventorySettings_TenantId ON TenantInventorySettings(TenantId);
GO
