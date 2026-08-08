-- ============================================================
-- ReportingDB Migration 005 — DimProducts.StockQuantity
--
-- GetInventoryReportAsync previously derived "current stock" by summing
-- FactInventoryMovements per product, but that fact table only carries a
-- small random SAMPLE of movement events (~2/product) for trend purposes —
-- it was never meant to reconcile to actual current stock, and doing so
-- produced nonsensical negative "available quantity" values once the demo
-- data seeding gave it real (signed) numbers to sum. The authoritative
-- current-stock number lives in WarehouseDB.Inventories.AvailableQuantity
-- (a PERSISTED COMPUTED column there); this migration adds a column here to
-- mirror it, ETL'd by 003_SeedFactsFromSeedData.sql's companion UPDATE below.
-- ============================================================

USE ReportingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DimProducts') AND name = 'StockQuantity')
    ALTER TABLE DimProducts ADD StockQuantity INT NOT NULL DEFAULT 0;
GO

-- Backfill from WarehouseDB (same SQL Server instance) — same one-time ETL
-- exception documented in 003_SeedFactsFromSeedData.sql's header.
UPDATE dp
SET dp.StockQuantity = inv.AvailableQuantity
FROM DimProducts dp
JOIN WarehouseDB.dbo.Inventories inv ON inv.ProductId = dp.ProductId;
GO
