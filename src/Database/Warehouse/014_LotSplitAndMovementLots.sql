-- ============================================================
-- WarehouseDB Migration 014 - lot split and the lots a movement acted on (Warehouse upgrade W7, Manufacturing Phase 4 P4-1,
-- docs/architecture/adr-manufacturing-phase4.md)
--   * InventoryLots.ParentLotId: a partial move of a lot to another warehouse splits it - the part that leaves is a NEW lot (sub-lot, number
--     "<root>-S<n>") that points back to the lot it came from. NULL for every lot that came from a receipt (all existing rows).
--   * StockMovementLots: the lot(s) a production movement really acted on (a reservation issue that drew from several lots, the new sub-lot of
--     a split transfer ...). StockMovements.LotId keeps the lot named in the request. Movements booked before W7 have no rows.
-- Idempotent: columns, constraints, the table and the indexes are created only when missing. No data is changed.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Sub-lots --------------------------------------------------------------------------------------------------------
IF COL_LENGTH('dbo.InventoryLots', 'ParentLotId') IS NULL
    ALTER TABLE dbo.InventoryLots ADD ParentLotId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InventoryLots_ParentLot')
    ALTER TABLE dbo.InventoryLots ADD CONSTRAINT FK_InventoryLots_ParentLot FOREIGN KEY (ParentLotId) REFERENCES dbo.InventoryLots (LotId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.InventoryLots') AND name = 'IX_InventoryLots_Parent')
    CREATE INDEX IX_InventoryLots_Parent ON dbo.InventoryLots (ParentLotId) WHERE ParentLotId IS NOT NULL;
GO

-- 2. The lots of a movement ------------------------------------------------------------------------------------------
IF OBJECT_ID('dbo.StockMovementLots') IS NULL
BEGIN
    CREATE TABLE dbo.StockMovementLots
    (
        LotRowId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockMovementLots PRIMARY KEY,
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        MovementId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockMovementLots_Movement FOREIGN KEY REFERENCES dbo.StockMovements (MovementId) ON DELETE CASCADE,
        Position    INT              NOT NULL CONSTRAINT DF_StockMovementLots_Position DEFAULT 0,
        LotId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockMovementLots_Lot FOREIGN KEY REFERENCES dbo.InventoryLots (LotId),
        LotNumber   NVARCHAR(100)    NOT NULL,
        Quantity    DECIMAL(18,6)    NOT NULL,
        UnitCost    DECIMAL(18,6)    NULL,
        WarehouseId UNIQUEIDENTIFIER NULL,
        ParentLotId UNIQUEIDENTIFIER NULL
    );
END
GO

-- the order of the rows in the answer (a replay lists them as the first answer did)
IF COL_LENGTH('dbo.StockMovementLots', 'Position') IS NULL
    ALTER TABLE dbo.StockMovementLots ADD Position INT NOT NULL CONSTRAINT DF_StockMovementLots_Position DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.StockMovementLots') AND name = 'IX_StockMovementLots_MovementId')
    CREATE INDEX IX_StockMovementLots_MovementId ON dbo.StockMovementLots (MovementId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.StockMovementLots') AND name = 'IX_StockMovementLots_TenantId_LotId')
    CREATE INDEX IX_StockMovementLots_TenantId_LotId ON dbo.StockMovementLots (TenantId, LotId);
GO

PRINT 'Lot split and movement lots: structure ready.';
GO
