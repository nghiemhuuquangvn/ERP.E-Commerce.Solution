-- ============================================================
-- WarehouseDB Schema Update — WareHouseService (Port 5004, Code: WH)
-- Migration 002 (2026-07-03): Lot/batch + expiry tracking, serial number tracking,
-- transfer orders, physical stocktakes, and the ValuationMethod column.
-- Note: Inventories.LocationId/ProductSku and WarehouseLocations.Name/Zone/
-- CurrentOccupancy/UpdatedAt already existed in 001_CreateWarehouseDB.sql — they had
-- no corresponding C# entity/usage until this same pass (see
-- docs/business/ERP-Business-Analysis.md section 3.5), so no new column is needed
-- for those.
-- ============================================================

USE WarehouseDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Inventories') AND name = 'ValuationMethod')
    ALTER TABLE Inventories ADD ValuationMethod INT NOT NULL DEFAULT 0; -- 0=FIFO, 1=LIFO, 2=Average
GO

-- ============================================================
-- Inventory Lots (batch tracking + expiry date management)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InventoryLots')
CREATE TABLE InventoryLots (
    LotId               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId           UNIQUEIDENTIFIER NOT NULL,
    LotNumber           NVARCHAR(100)    NOT NULL,
    ReceivedQuantity    INT              NOT NULL,
    RemainingQuantity   INT              NOT NULL,
    UnitCost            DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    ReceivedDate        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ExpiryDate          DATETIME2        NULL,
    PurchaseOrderId     UNIQUEIDENTIFIER NULL
);
CREATE INDEX IX_InventoryLots_Product ON InventoryLots (ProductId, RemainingQuantity);
CREATE INDEX IX_InventoryLots_Expiry ON InventoryLots (ExpiryDate) WHERE ExpiryDate IS NOT NULL;
GO

-- ============================================================
-- Inventory Serials (serial number tracking)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InventorySerials')
CREATE TABLE InventorySerials (
    SerialId        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    SerialNumber    NVARCHAR(100)    NOT NULL,
    LotId           UNIQUEIDENTIFIER NULL REFERENCES InventoryLots(LotId),
    Status          INT              NOT NULL DEFAULT 0, -- 0=InStock, 1=Reserved, 2=Sold, 3=Damaged
    ReceivedDate    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    SoldDate        DATETIME2        NULL
);
CREATE UNIQUE INDEX IX_InventorySerials_SerialNumber ON InventorySerials (SerialNumber);
CREATE INDEX IX_InventorySerials_Product ON InventorySerials (ProductId, Status);
GO

-- ============================================================
-- Transfer Orders (inter-warehouse stock transfer)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TransferOrders')
CREATE TABLE TransferOrders (
    TransferOrderId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TransferNumber  NVARCHAR(50)     NOT NULL,
    FromLocationId  UNIQUEIDENTIFIER NOT NULL REFERENCES WarehouseLocations(LocationId),
    ToLocationId    UNIQUEIDENTIFIER NOT NULL REFERENCES WarehouseLocations(LocationId),
    Status          INT              NOT NULL DEFAULT 0, -- 0=Draft, 1=InTransit, 2=Completed, 3=Cancelled
    Notes           NVARCHAR(1000)   NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ShippedAt       DATETIME2        NULL,
    CompletedAt     DATETIME2        NULL
);
CREATE UNIQUE INDEX IX_TransferOrders_Number ON TransferOrders (TransferNumber);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TransferOrderLines')
CREATE TABLE TransferOrderLines (
    LineId          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TransferOrderId UNIQUEIDENTIFIER NOT NULL REFERENCES TransferOrders(TransferOrderId) ON DELETE CASCADE,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    Quantity        INT              NOT NULL
);
CREATE INDEX IX_TransferOrderLines_Order ON TransferOrderLines (TransferOrderId);
GO

-- ============================================================
-- Stocktakes (physical inventory counts)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Stocktakes')
CREATE TABLE Stocktakes (
    StocktakeId     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    StocktakeNumber NVARCHAR(50)     NOT NULL,
    LocationId      UNIQUEIDENTIFIER NULL REFERENCES WarehouseLocations(LocationId),
    Status          INT              NOT NULL DEFAULT 0, -- 0=InProgress, 1=Completed, 2=Cancelled
    Notes           NVARCHAR(1000)   NULL,
    StartedBy       NVARCHAR(256)    NULL,
    StartedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt     DATETIME2        NULL
);
CREATE UNIQUE INDEX IX_Stocktakes_Number ON Stocktakes (StocktakeNumber);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StocktakeLines')
CREATE TABLE StocktakeLines (
    LineId          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    StocktakeId     UNIQUEIDENTIFIER NOT NULL REFERENCES Stocktakes(StocktakeId) ON DELETE CASCADE,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    SystemQuantity  INT              NOT NULL,
    CountedQuantity INT              NULL
);
CREATE INDEX IX_StocktakeLines_Stocktake ON StocktakeLines (StocktakeId);
GO

-- ============================================================
-- Reservations.ReservedItems owned collection (Reservation aggregate previously
-- modeled one row per reservation in this table; the C# aggregate holds a list of
-- items instead — see WarehouseDbContext's new OwnsMany config). Add the table the
-- owned collection now needs; existing single-row-per-reservation data (if any) is
-- not migrated automatically since the columns don't correspond 1:1.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reservation_ReservedItems')
CREATE TABLE Reservation_ReservedItems (
    Id               INT              NOT NULL IDENTITY PRIMARY KEY,
    ReservationId    UNIQUEIDENTIFIER NOT NULL REFERENCES Reservations(ReservationId) ON DELETE CASCADE,
    ProductId        UNIQUEIDENTIFIER NOT NULL,
    Quantity         INT              NOT NULL,
    WarehouseLocation NVARCHAR(100)   NULL
);
CREATE INDEX IX_Reservation_ReservedItems_ReservationId ON Reservation_ReservedItems (ReservationId);
GO
