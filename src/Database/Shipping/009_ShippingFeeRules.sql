-- ============================================================
-- ShippingDB Migration 009 - the shop's delivery price list (2026-10-28, ROADMAP P1-11c)
--
-- ShippingFeeRules: "parcels to these provinces in this weight band cost this much", tried in priority order; a parcel no row matches pays the
-- flat TenantShippingSettings.DefaultShippingFee. Name is unique per shop.
-- Idempotent.
-- ============================================================

USE ShippingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ShippingFeeRules')
BEGIN
    CREATE TABLE ShippingFeeRules
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        Name                NVARCHAR(200) NOT NULL,
        Priority            INT NOT NULL DEFAULT 100,
        IsActive            BIT NOT NULL DEFAULT 1,
        MatchProvinces      NVARCHAR(1000) NULL,
        MinWeightGrams      INT NULL,
        MaxWeightGrams      INT NULL,
        Fee                 DECIMAL(18,2) NOT NULL DEFAULT 0,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ShippingFeeRules_Tenant_Priority' AND object_id = OBJECT_ID('ShippingFeeRules'))
    CREATE INDEX IX_ShippingFeeRules_Tenant_Priority ON ShippingFeeRules (TenantId, Priority);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ShippingFeeRules_Tenant_Name' AND object_id = OBJECT_ID('ShippingFeeRules'))
    CREATE UNIQUE INDEX UX_ShippingFeeRules_Tenant_Name ON ShippingFeeRules (TenantId, Name) WHERE IsDeleted = 0;
GO
