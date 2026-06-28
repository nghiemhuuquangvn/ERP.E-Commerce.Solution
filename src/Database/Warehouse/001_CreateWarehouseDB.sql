-- ============================================================
-- WarehouseDB Schema — WareHouseService (Port 5004, Code: WH)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'WarehouseDB')
    CREATE DATABASE WarehouseDB;
GO

USE WarehouseDB;
GO

-- ============================================================
-- Warehouse Locations (zones, aisles, bins)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WarehouseLocations')
CREATE TABLE WarehouseLocations (
    LocationId      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    LocationCode    NVARCHAR(50)     NOT NULL,
    Name            NVARCHAR(200)    NULL,
    Zone            NVARCHAR(50)     NULL,
    Aisle           NVARCHAR(50)     NULL,
    Shelf           NVARCHAR(50)     NULL,
    Bin             NVARCHAR(50)     NULL,
    Capacity        INT              NOT NULL DEFAULT 1000,
    CurrentOccupancy INT             NOT NULL DEFAULT 0,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE UNIQUE INDEX IX_Location_Code ON WarehouseLocations (LocationCode, TenantId) WHERE IsActive = 1;
CREATE INDEX IX_Location_TenantId    ON WarehouseLocations (TenantId, IsActive);
GO

-- ============================================================
-- Inventories (current stock levels per product)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Inventories')
CREATE TABLE Inventories (
    ProductId           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    ProductName         NVARCHAR(255)    NOT NULL,
    ProductSku          NVARCHAR(100)    NULL,
    TotalQuantity       INT              NOT NULL DEFAULT 0,
    ReservedQuantity    INT              NOT NULL DEFAULT 0,
    LockedQuantity      INT              NOT NULL DEFAULT 0,
    AvailableQuantity   AS (TotalQuantity - ReservedQuantity - LockedQuantity) PERSISTED,
    ReorderPoint        INT              NOT NULL DEFAULT 10,
    ReorderQuantity     INT              NOT NULL DEFAULT 50,
    Cost                DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    LocationId          UNIQUEIDENTIFIER NULL,
    LastMovementDate    DATETIME2        NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Inventory_Location FOREIGN KEY (LocationId) REFERENCES WarehouseLocations(LocationId)
);
CREATE INDEX IX_Inventory_TenantId ON Inventories (TenantId);
CREATE INDEX IX_Inventory_LowStock  ON Inventories (TenantId, AvailableQuantity, ReorderPoint);
GO

-- ============================================================
-- Stock Movements (every inventory change is tracked)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StockMovements')
CREATE TABLE StockMovements (
    MovementId      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    Quantity        INT              NOT NULL, -- positive=in, negative=out
    MovementType    INT              NOT NULL, -- 0=Receipt, 1=Issue, 2=Transfer, 3=Adjustment, 4=Return
    FromLocationId  UNIQUEIDENTIFIER NULL,
    ToLocationId    UNIQUEIDENTIFIER NULL,
    FromLocation    NVARCHAR(100)    NULL,
    ToLocation      NVARCHAR(100)    NULL,
    Reference       NVARCHAR(100)    NULL, -- PO number, SO number, etc.
    ReferenceId     UNIQUEIDENTIFIER NULL,
    Reason          NVARCHAR(500)    NULL,
    RecordedBy      NVARCHAR(256)    NULL,
    RecordedAt      DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_StockMovements_ProductId  ON StockMovements (ProductId, RecordedAt DESC);
CREATE INDEX IX_StockMovements_TenantId   ON StockMovements (TenantId, RecordedAt DESC);
CREATE INDEX IX_StockMovements_Type       ON StockMovements (MovementType, RecordedAt DESC);
CREATE INDEX IX_StockMovements_Reference  ON StockMovements (ReferenceId) WHERE ReferenceId IS NOT NULL;
GO

-- ============================================================
-- Reservations (hold stock for orders)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reservations')
CREATE TABLE Reservations (
    ReservationId   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    OrderId         UNIQUEIDENTIFIER NOT NULL,
    ProductId       UNIQUEIDENTIFIER NOT NULL,
    Quantity        INT              NOT NULL,
    Status          INT              NOT NULL DEFAULT 0, -- 0=Active, 1=Released, 2=Consumed, 3=Expired
    HoldUntil       DATETIME2        NOT NULL,
    ReservedAt      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ReleasedAt      DATETIME2        NULL,
    ConsumedAt      DATETIME2        NULL
);
CREATE INDEX IX_Reservations_OrderId    ON Reservations (OrderId, Status);
CREATE INDEX IX_Reservations_ProductId  ON Reservations (ProductId, Status);
CREATE INDEX IX_Reservations_TenantId   ON Reservations (TenantId, Status, HoldUntil);
CREATE INDEX IX_Reservations_Expiring   ON Reservations (HoldUntil) WHERE Status = 0;
GO

-- ============================================================
-- Outbox Messages
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OutboxMessages')
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
CREATE INDEX IX_OutboxMessages_Unprocessed ON OutboxMessages (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL;
GO
