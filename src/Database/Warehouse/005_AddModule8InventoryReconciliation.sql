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

-- Reservation.ReleaseReason (set when a reservation is released — see Reservation.cs's
-- Release(reason) method) was never added to the original 001_CreateWarehouseDB.sql table,
-- so ReservationExpiryProcessor's background poll — and any manual release — 500'd/silently
-- errored every run with "Invalid column name 'ReleaseReason'".
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'ReleaseReason')
    ALTER TABLE Reservations ADD ReleaseReason NVARCHAR(500) NULL;
GO

-- ============================================================
-- Reservation_ReservedItems: the single nullable LotId (added in migration 004)
-- only ever remembered the FIRST lot touched by a multi-lot FEFO reservation,
-- making it impossible to correctly reverse every lot on cancel. Replaced by a
-- JSON column holding every (LotId, Quantity) allocation for the line.
-- ============================================================
-- The FK on this column was created by EF's default naming convention (e.g.
-- FK__Reservati__LotId__xxxxxxxx), not the explicit name assumed below — look it
-- up dynamically by column instead of assuming a fixed constraint name, or this
-- DROP silently no-ops (IF EXISTS on a name that never matches) and the DROP
-- COLUMN two batches down fails because the real FK still references it.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reservation_ReservedItems') AND name = 'LotId')
BEGIN
    DECLARE @lotIdFkName NVARCHAR(200);
    SELECT @lotIdFkName = fk.name
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
    WHERE fk.parent_object_id = OBJECT_ID('Reservation_ReservedItems') AND c.name = 'LotId';

    IF @lotIdFkName IS NOT NULL
        EXEC('ALTER TABLE Reservation_ReservedItems DROP CONSTRAINT [' + @lotIdFkName + ']');
END
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
