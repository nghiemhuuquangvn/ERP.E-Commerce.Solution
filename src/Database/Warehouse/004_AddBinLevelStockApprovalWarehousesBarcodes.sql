-- ============================================================
-- WarehouseDB Migration 004 — Module 7 (Warehouse) reconciliation:
-- bin-level stock tracking, Stock Adjustment approval workflow, Stocktake
-- approval gate, a real Warehouse (site) entity, FEFO lot linkage on
-- reservations, and Barcode resolve.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Bin-level stock breakdown (additive alongside Inventories' rollup)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InventoryLocationStocks')
CREATE TABLE InventoryLocationStocks (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    LocationId      UNIQUEIDENTIFIER NOT NULL REFERENCES WarehouseLocations(LocationId),
    Quantity        INT              NOT NULL DEFAULT 0,
    ReservedQuantity INT             NOT NULL DEFAULT 0,
    LockedQuantity  INT              NOT NULL DEFAULT 0,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE UNIQUE INDEX IX_InventoryLocationStocks_Product_Location ON InventoryLocationStocks (ProductId, LocationId);
GO

-- ============================================================
-- Stock Adjustment approval workflow — previously AdjustStockCommand wrote
-- straight to Inventories with no persisted request/approval step at all.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StockAdjustments')
CREATE TABLE StockAdjustments (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    LocationId      UNIQUEIDENTIFIER NULL REFERENCES WarehouseLocations(LocationId),
    QuantityDelta   INT              NOT NULL,
    Reason          INT              NOT NULL, -- 0=Damaged, 1=Lost, 2=Correction, 3=Other
    Notes           NVARCHAR(1000)   NULL,
    Status          INT              NOT NULL DEFAULT 0, -- 0=Pending, 1=Approved, 2=Rejected
    RequestedBy     NVARCHAR(256)    NOT NULL,
    RequestedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ApprovedBy      NVARCHAR(256)    NULL,
    ApprovedAt      DATETIME2        NULL,
    RejectionReason NVARCHAR(1000)   NULL
);
CREATE INDEX IX_StockAdjustments_ProductId ON StockAdjustments (ProductId);
CREATE INDEX IX_StockAdjustments_Status ON StockAdjustments (Status);
GO

-- ============================================================
-- Stocktake approval gate — new PendingApproval status (value 3, added after
-- the existing InProgress=0/Completed=1/Cancelled=2 to not disturb them) and
-- a VarianceReason column on counted lines.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StocktakeLines') AND name = 'VarianceReason')
    ALTER TABLE StocktakeLines ADD VarianceReason NVARCHAR(500) NULL;
GO

-- ============================================================
-- Warehouse (physical site/branch) — did not exist anywhere in this system
-- before. Backfills a default "Main Warehouse" and points every existing
-- WarehouseLocation at it.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Warehouses')
CREATE TABLE Warehouses (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Code        NVARCHAR(50)     NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Address     NVARCHAR(500)    NULL,
    IsActive    BIT              NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IX_Warehouses_Code ON Warehouses (Code);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WarehouseLocations') AND name = 'WarehouseId')
    ALTER TABLE WarehouseLocations ADD WarehouseId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM Warehouses WHERE Code = 'MAIN')
    INSERT INTO Warehouses (Id, Code, Name, IsActive) VALUES (NEWID(), 'MAIN', 'Main Warehouse', 1);
GO

UPDATE WarehouseLocations
SET WarehouseId = (SELECT TOP 1 Id FROM Warehouses WHERE Code = 'MAIN')
WHERE WarehouseId IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_WarehouseLocations_Warehouse')
    ALTER TABLE WarehouseLocations ADD CONSTRAINT FK_WarehouseLocations_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id);
GO

-- ============================================================
-- FEFO lot linkage on reservations
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reservation_ReservedItems') AND name = 'LotId')
    ALTER TABLE Reservation_ReservedItems ADD LotId UNIQUEIDENTIFIER NULL REFERENCES InventoryLots(LotId);
GO

-- ============================================================
-- Barcode resolve — did not exist at all before.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BarcodeMappings')
CREATE TABLE BarcodeMappings (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Barcode         NVARCHAR(100)    NOT NULL,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    LotId           UNIQUEIDENTIFIER NULL REFERENCES InventoryLots(LotId),
    SerialNumber    NVARCHAR(100)    NULL
);
CREATE UNIQUE INDEX IX_BarcodeMappings_Barcode ON BarcodeMappings (Barcode);
CREATE INDEX IX_BarcodeMappings_ProductId ON BarcodeMappings (ProductId);
GO
