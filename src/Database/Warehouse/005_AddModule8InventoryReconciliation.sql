-- ============================================================
-- WarehouseDB Migration 005 — Module 8 (Inventory) reconciliation:
-- soft-hold lot reservation (fixes double-depletion + never-actually-ships-stock
-- bugs), per-sale COGS audit trail, and period-end Inventory Valuation Snapshot.
-- ============================================================

USE WarehouseDB;
GO

-- ============================================================
-- InventoryLots gains a soft-hold ReservedQuantity — reservation no longer
-- destructively depletes RemainingQuantity; real depletion happens only at
-- ship time (see ReservationFulfillmentService).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryLots') AND name = 'ReservedQuantity')
    ALTER TABLE InventoryLots ADD ReservedQuantity INT NOT NULL DEFAULT 0;
GO

-- ============================================================
-- Reservation_ReservedItems: the single nullable LotId (added in migration 004)
-- only ever remembered the FIRST lot touched by a multi-lot FEFO reservation,
-- making it impossible to correctly reverse every lot on cancel. Replaced by a
-- JSON column holding every (LotId, Quantity) allocation for the line.
-- ============================================================
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reservation_ReservedItems') AND name = 'LotId')
    ALTER TABLE Reservation_ReservedItems DROP CONSTRAINT IF EXISTS FK_Reservation_ReservedItems_InventoryLots_LotId;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reservation_ReservedItems') AND name = 'LotId')
    ALTER TABLE Reservation_ReservedItems DROP COLUMN LotId;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reservation_ReservedItems') AND name = 'LotAllocationsJson')
    ALTER TABLE Reservation_ReservedItems ADD LotAllocationsJson NVARCHAR(MAX) NULL;
GO

-- ============================================================
-- Per-sale COGS audit trail — StockMovement never recorded a unit cost, so
-- there was no record of "this sale used X units at cost Y from lot Z".
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CostConsumptionLogs')
CREATE TABLE CostConsumptionLogs (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    LotId           UNIQUEIDENTIFIER NULL REFERENCES InventoryLots(LotId),
    Quantity        INT              NOT NULL,
    UnitCost        DECIMAL(18,2)    NOT NULL,
    TotalCost       DECIMAL(18,2)    NOT NULL,
    SourceOrderId   UNIQUEIDENTIFIER NOT NULL,
    ConsumedAt      DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_CostConsumptionLogs_ProductId ON CostConsumptionLogs (ProductId);
CREATE INDEX IX_CostConsumptionLogs_SourceOrderId ON CostConsumptionLogs (SourceOrderId);
GO

-- ============================================================
-- Period-end Inventory Valuation Snapshot — did not exist at all before;
-- only a live, on-demand valuation query existed. Immutable once written
-- (IsClosed is always true at insert time — a period is superseded by a
-- correcting entry in a later period, never edited).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InventoryValuationSnapshots')
CREATE TABLE InventoryValuationSnapshots (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    PeriodCode      NVARCHAR(50)     NOT NULL,
    WarehouseId     UNIQUEIDENTIFIER NULL REFERENCES Warehouses(Id),
    SnapshotDate    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsClosed        BIT              NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IX_InventoryValuationSnapshots_Period_Warehouse ON InventoryValuationSnapshots (PeriodCode, WarehouseId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InventoryValuationSnapshotLines')
CREATE TABLE InventoryValuationSnapshotLines (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    SnapshotId      UNIQUEIDENTIFIER NOT NULL REFERENCES InventoryValuationSnapshots(Id) ON DELETE CASCADE,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    Quantity        INT              NOT NULL,
    UnitCost        DECIMAL(18,2)    NOT NULL,
    TotalValue      DECIMAL(18,2)    NOT NULL
);
CREATE INDEX IX_InventoryValuationSnapshotLines_SnapshotId ON InventoryValuationSnapshotLines (SnapshotId);
GO
