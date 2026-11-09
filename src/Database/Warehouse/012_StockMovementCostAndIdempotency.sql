-- ============================================================
-- WarehouseDB Migration 012 - production movement types, cost on the movement, real idempotency (Warehouse upgrade W3,
-- docs/architecture/adr-warehouse-upgrade.md)
--   * StockMovements: WarehouseId, LotId, UnitCost, ValueDelta, StockDelta (all nullable: rows older than W3 stay NULL = "not recorded").
--   * Movement types 7-12 (ProductionIssue, ProductionReturn, ProductionReceipt, ProductionScrap, QcRelease, SubcontractTransfer) are new
--     enum values only - the column is an int, nothing to alter.
--   * UX_StockMovements_Idempotency: one movement per (tenant, product, reference, type) for the PRODUCTION types only; the reference is
--     "<document number>#<line>", so one line = one movement.
--     Older types cannot get it: the live data holds many repeated (product, reference, type) groups; the report below counts them.
-- Idempotent: columns and the index are created only when missing.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('dbo.StockMovements', 'WarehouseId') IS NULL ALTER TABLE dbo.StockMovements ADD WarehouseId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.StockMovements', 'LotId')       IS NULL ALTER TABLE dbo.StockMovements ADD LotId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.StockMovements', 'UnitCost')    IS NULL ALTER TABLE dbo.StockMovements ADD UnitCost DECIMAL(18,6) NULL;
IF COL_LENGTH('dbo.StockMovements', 'ValueDelta')  IS NULL ALTER TABLE dbo.StockMovements ADD ValueDelta DECIMAL(18,2) NULL;
IF COL_LENGTH('dbo.StockMovements', 'StockDelta')  IS NULL ALTER TABLE dbo.StockMovements ADD StockDelta DECIMAL(18,6) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.StockMovements') AND name = 'UX_StockMovements_Idempotency')
    CREATE UNIQUE INDEX UX_StockMovements_Idempotency
        ON dbo.StockMovements (TenantId, ProductId, Reference, MovementType)
        WHERE Reference IS NOT NULL AND MovementType >= 7;
GO

-- Report (information only, nothing is changed): movements of the older types that repeat a (tenant, product, reference, type).
DECLARE @groups INT = (SELECT COUNT(*) FROM (SELECT 1 AS x FROM dbo.StockMovements WHERE Reference IS NOT NULL AND MovementType < 7
                                              GROUP BY TenantId, ProductId, Reference, MovementType HAVING COUNT(*) > 1) d);
PRINT CONCAT('Report: ', @groups, ' repeated (tenant, product, reference, type) groups among the older movement types - they keep the pre-check only, no unique index.');
GO
