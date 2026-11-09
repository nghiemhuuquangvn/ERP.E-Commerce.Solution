-- ============================================================
-- ProductDB Migration 020 - Products.IsLotControlled / IsSerialControlled (Warehouse upgrade W2, docs/architecture/adr-warehouse-upgrade.md)
-- Whether an item is tracked by lot and / or serial number. The Warehouse and Manufacturing read them through internal/v1/product-lookup.
-- Idempotent.
-- ============================================================

USE ProductDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('dbo.Products', 'IsLotControlled') IS NULL
    ALTER TABLE dbo.Products ADD IsLotControlled BIT NOT NULL CONSTRAINT DF_Products_IsLotControlled DEFAULT 0;
IF COL_LENGTH('dbo.Products', 'IsSerialControlled') IS NULL
    ALTER TABLE dbo.Products ADD IsSerialControlled BIT NOT NULL CONSTRAINT DF_Products_IsSerialControlled DEFAULT 0;
GO
