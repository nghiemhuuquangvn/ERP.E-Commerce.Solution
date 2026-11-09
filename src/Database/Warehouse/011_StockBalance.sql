-- ============================================================
-- WarehouseDB Migration 011 - stock per warehouse and lot (Warehouse upgrade W2, docs/architecture/adr-warehouse-upgrade.md)
--   * StockBalances: stock per (tenant, product, warehouse, lot). Inventories stays the per-item total that Sales / POS / Replenishment read.
--   * InventoryLots: WarehouseId, Status (0 Available, 1 QcHold, 2 Blocked), ManufactureDate, Source (0 Receipt, 1 Production, 2 Return), RowVersion.
--   * Inventories: RowVersion (optimistic concurrency, HTTP 409 on a conflicting write).
--   * TenantInventorySettings.DefaultWarehouseId: where stock without a warehouse goes.
--   * Backfill: legacy stock has no warehouse, so it goes to the tenant's default warehouse = DefaultWarehouseId, else the active warehouse
--     with Code 'MAIN', else the first active warehouse by code. A tenant with no warehouse gets rows with WarehouseId NULL ("unassigned").
-- Idempotent: columns / tables / indexes are created only when missing; the backfill only touches products and lots without balances.
-- Prints a report (tenants, rows, cases the owner should review).
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Structure ------------------------------------------------------------------------------------------------------
IF OBJECT_ID('dbo.StockBalances') IS NULL
BEGIN
    CREATE TABLE dbo.StockBalances
    (
        BalanceId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockBalances PRIMARY KEY,
        TenantId         UNIQUEIDENTIFIER NOT NULL,
        ProductId        UNIQUEIDENTIFIER NOT NULL,
        WarehouseId      UNIQUEIDENTIFIER NULL,
        LotId            UNIQUEIDENTIFIER NULL,
        Quantity         DECIMAL(18,6)    NOT NULL CONSTRAINT DF_StockBalances_Quantity DEFAULT 0,
        ReservedQuantity DECIMAL(18,6)    NOT NULL CONSTRAINT DF_StockBalances_ReservedQuantity DEFAULT 0,
        LockedQuantity   DECIMAL(18,6)    NOT NULL CONSTRAINT DF_StockBalances_LockedQuantity DEFAULT 0,
        UpdatedAt        DATETIME2        NOT NULL CONSTRAINT DF_StockBalances_UpdatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion       ROWVERSION      NOT NULL
    );
END
GO

-- One row per key; SQL Server treats NULLs as equal in a unique index, so exactly one lot-less / unassigned row is allowed per key.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.StockBalances') AND name = 'UX_StockBalances_Key')
    CREATE UNIQUE INDEX UX_StockBalances_Key ON dbo.StockBalances (TenantId, ProductId, WarehouseId, LotId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.StockBalances') AND name = 'IX_StockBalances_Warehouse')
    CREATE INDEX IX_StockBalances_Warehouse ON dbo.StockBalances (TenantId, WarehouseId, ProductId);
GO

IF COL_LENGTH('dbo.InventoryLots', 'WarehouseId') IS NULL ALTER TABLE dbo.InventoryLots ADD WarehouseId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.InventoryLots', 'Status') IS NULL ALTER TABLE dbo.InventoryLots ADD [Status] TINYINT NOT NULL CONSTRAINT DF_InventoryLots_Status DEFAULT 0;
IF COL_LENGTH('dbo.InventoryLots', 'ManufactureDate') IS NULL ALTER TABLE dbo.InventoryLots ADD ManufactureDate DATE NULL;
IF COL_LENGTH('dbo.InventoryLots', 'Source') IS NULL ALTER TABLE dbo.InventoryLots ADD [Source] TINYINT NOT NULL CONSTRAINT DF_InventoryLots_Source DEFAULT 0;
IF COL_LENGTH('dbo.InventoryLots', 'RowVersion') IS NULL ALTER TABLE dbo.InventoryLots ADD RowVersion ROWVERSION NOT NULL;
IF COL_LENGTH('dbo.Inventories', 'RowVersion') IS NULL ALTER TABLE dbo.Inventories ADD RowVersion ROWVERSION NOT NULL;
IF COL_LENGTH('dbo.TenantInventorySettings', 'DefaultWarehouseId') IS NULL ALTER TABLE dbo.TenantInventorySettings ADD DefaultWarehouseId UNIQUEIDENTIFIER NULL;
GO

-- A lot number is unique per (tenant, product) when the data allows it; otherwise a plain index and a warning for the owner.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.InventoryLots') AND name IN ('UX_InventoryLots_Tenant_Product_Lot', 'IX_InventoryLots_Tenant_Product_Lot'))
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.InventoryLots GROUP BY TenantId, ProductId, LotNumber HAVING COUNT(*) > 1)
    BEGIN
        PRINT 'WARNING: duplicate lot numbers exist per (tenant, product) - created a NON-unique index. Review:';
        SELECT TenantId, ProductId, LotNumber, COUNT(*) AS Duplicates FROM dbo.InventoryLots GROUP BY TenantId, ProductId, LotNumber HAVING COUNT(*) > 1;
        CREATE INDEX IX_InventoryLots_Tenant_Product_Lot ON dbo.InventoryLots (TenantId, ProductId, LotNumber);
    END
    ELSE
        CREATE UNIQUE INDEX UX_InventoryLots_Tenant_Product_Lot ON dbo.InventoryLots (TenantId, ProductId, LotNumber);
END
GO

-- 2. Backfill ---------------------------------------------------------------------------------------------------------
-- Default warehouse per tenant that has inventory.
IF OBJECT_ID('tempdb..#Default') IS NOT NULL DROP TABLE #Default;
SELECT t.TenantId,
       COALESCE(s.DefaultWarehouseId,
                (SELECT TOP 1 w.Id FROM dbo.Warehouses w WHERE w.TenantId = t.TenantId AND w.IsActive = 1 ORDER BY CASE WHEN w.Code = 'MAIN' THEN 0 ELSE 1 END, w.Code)) AS WarehouseId
INTO #Default
FROM (SELECT DISTINCT TenantId FROM dbo.Inventories) t
LEFT JOIN dbo.TenantInventorySettings s ON s.TenantId = t.TenantId;

-- Lots without a warehouse go to the default one.
UPDATE l SET l.WarehouseId = d.WarehouseId
FROM dbo.InventoryLots l JOIN #Default d ON d.TenantId = l.TenantId
WHERE l.WarehouseId IS NULL AND d.WarehouseId IS NOT NULL;

-- One balance per lot that has stock or a reservation and no balance yet.
INSERT INTO dbo.StockBalances (BalanceId, TenantId, ProductId, WarehouseId, LotId, Quantity, ReservedQuantity, LockedQuantity)
SELECT NEWID(), l.TenantId, l.ProductId, l.WarehouseId, l.LotId, l.RemainingQuantity, l.ReservedQuantity, 0
FROM dbo.InventoryLots l
WHERE (l.RemainingQuantity <> 0 OR l.ReservedQuantity <> 0)
  AND EXISTS (SELECT 1 FROM dbo.Inventories i WHERE i.TenantId = l.TenantId AND i.ProductId = l.ProductId)
  AND NOT EXISTS (SELECT 1 FROM dbo.StockBalances b WHERE b.TenantId = l.TenantId AND b.ProductId = l.ProductId AND b.LotId = l.LotId);

-- The lot-less remainder of each product: total minus the lots (may be negative after POS sales), reserved minus the lots' reservations (never below 0).
IF OBJECT_ID('tempdb..#Remainder') IS NOT NULL DROP TABLE #Remainder;
SELECT i.TenantId, i.ProductId, d.WarehouseId,
       i.TotalQuantity - ISNULL(l.Remaining, 0) AS Quantity,
       CASE WHEN i.ReservedQuantity - ISNULL(l.Reserved, 0) < 0 THEN 0 ELSE i.ReservedQuantity - ISNULL(l.Reserved, 0) END AS Reserved,
       CASE WHEN i.ReservedQuantity - ISNULL(l.Reserved, 0) < 0 THEN 1 ELSE 0 END AS ReservedClamped,
       i.LockedQuantity AS Locked
INTO #Remainder
FROM dbo.Inventories i
JOIN #Default d ON d.TenantId = i.TenantId
OUTER APPLY (SELECT SUM(x.RemainingQuantity) AS Remaining, SUM(x.ReservedQuantity) AS Reserved FROM dbo.InventoryLots x WHERE x.TenantId = i.TenantId AND x.ProductId = i.ProductId) l
WHERE NOT EXISTS (SELECT 1 FROM dbo.StockBalances b WHERE b.TenantId = i.TenantId AND b.ProductId = i.ProductId AND b.LotId IS NULL);

INSERT INTO dbo.StockBalances (BalanceId, TenantId, ProductId, WarehouseId, LotId, Quantity, ReservedQuantity, LockedQuantity)
SELECT NEWID(), TenantId, ProductId, WarehouseId, NULL, Quantity, Reserved, Locked
FROM #Remainder
WHERE Quantity <> 0 OR Reserved <> 0 OR Locked <> 0;

-- 3. Report -----------------------------------------------------------------------------------------------------------
DECLARE @balances INT = (SELECT COUNT(*) FROM dbo.StockBalances);
DECLARE @noWarehouse INT = (SELECT COUNT(*) FROM #Default WHERE WarehouseId IS NULL);
DECLARE @clamped INT = (SELECT COUNT(*) FROM #Remainder WHERE ReservedClamped = 1);
PRINT 'StockBalance backfill: ' + CAST(@balances AS VARCHAR(20)) + ' balance rows in total.';
IF @noWarehouse > 0 PRINT 'REVIEW: ' + CAST(@noWarehouse AS VARCHAR(20)) + ' tenant(s) have no active warehouse - their stock is "unassigned" (WarehouseId NULL).';
IF @clamped > 0 PRINT 'REVIEW: ' + CAST(@clamped AS VARCHAR(20)) + ' product(s) reserve more through lots than the item total reserves - reserved clamped at 0.';
SELECT 'DefaultWarehouse' AS Report, d.TenantId, d.WarehouseId, w.Code FROM #Default d LEFT JOIN dbo.Warehouses w ON w.Id = d.WarehouseId;

-- Parity: Inventories vs the sum of its balances (0 rows = in step).
SELECT 'Drift' AS Report, i.TenantId, i.ProductId, i.TotalQuantity, ISNULL(b.Quantity, 0) AS BalanceQuantity, i.ReservedQuantity, ISNULL(b.Reserved, 0) AS BalanceReserved
FROM dbo.Inventories i
OUTER APPLY (SELECT SUM(x.Quantity) AS Quantity, SUM(x.ReservedQuantity) AS Reserved FROM dbo.StockBalances x WHERE x.TenantId = i.TenantId AND x.ProductId = i.ProductId) b
WHERE i.TotalQuantity <> ISNULL(b.Quantity, 0) OR i.ReservedQuantity <> ISNULL(b.Reserved, 0);
GO
