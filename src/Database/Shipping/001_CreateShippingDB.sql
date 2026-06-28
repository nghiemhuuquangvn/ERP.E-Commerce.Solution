-- ============================================================
-- ShippingDB Schema
-- Service: ShippingService (Port 5010, Code: SHP)
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ShippingDB')
    CREATE DATABASE ShippingDB;
GO

USE ShippingDB;
GO

CREATE TABLE Carriers (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    Code            NVARCHAR(50)     NOT NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    BaseRatePerKg   DECIMAL(10,2)    NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_Carriers_Code     ON Carriers (Code);
CREATE INDEX IX_Carriers_TenantId ON Carriers (TenantId);
GO

CREATE TABLE Shipments (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    OrderId             UNIQUEIDENTIFIER NOT NULL,
    CarrierId           UNIQUEIDENTIFIER NOT NULL,
    CarrierName         NVARCHAR(200)    NOT NULL,
    TrackingNumber      NVARCHAR(200)    NULL,
    Status              INT              NOT NULL, -- 0=Pending,1=PickedUp,2=InTransit,3=OutForDelivery,4=Delivered,5=Failed,6=Returned,7=Cancelled
    RecipientName       NVARCHAR(200)    NOT NULL,
    RecipientPhone      NVARCHAR(50)     NULL,
    AddressStreet       NVARCHAR(300)    NOT NULL,
    AddressCity         NVARCHAR(100)    NOT NULL,
    AddressState        NVARCHAR(100)    NULL,
    AddressPostalCode   NVARCHAR(20)     NULL,
    AddressCountry      NVARCHAR(100)    NOT NULL,
    TotalWeightKg       DECIMAL(10,3)    NOT NULL DEFAULT 0,
    EstimatedDelivery   DATE             NULL,
    ActualDelivery      DATETIME2        NULL,
    CancelledAt         DATETIME2        NULL,
    CancelReason        NVARCHAR(500)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (CarrierId) REFERENCES Carriers(Id)
);

CREATE INDEX IX_Shipments_OrderId  ON Shipments (OrderId);
CREATE INDEX IX_Shipments_TenantId ON Shipments (TenantId);
CREATE INDEX IX_Shipments_Status   ON Shipments (Status);
GO

CREATE TABLE ShipmentItems (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    ShipmentId  UNIQUEIDENTIFIER NOT NULL,
    ProductId   UNIQUEIDENTIFIER NOT NULL,
    ProductName NVARCHAR(300)    NOT NULL,
    Quantity    INT              NOT NULL,
    WeightKg    DECIMAL(10,3)    NOT NULL DEFAULT 0,
    FOREIGN KEY (ShipmentId) REFERENCES Shipments(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ShipmentItems_ShipmentId ON ShipmentItems (ShipmentId);
GO

CREATE TABLE ShipmentTrackingEvents (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    ShipmentId  UNIQUEIDENTIFIER NOT NULL,
    Status      INT              NOT NULL,
    Location    NVARCHAR(300)    NULL,
    Description NVARCHAR(500)    NULL,
    OccurredAt  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (ShipmentId) REFERENCES Shipments(Id) ON DELETE CASCADE
);

CREATE INDEX IX_TrackingEvents_ShipmentId ON ShipmentTrackingEvents (ShipmentId);
GO

CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OccurredOnUtc   DATETIME2        NOT NULL,
    Type            NVARCHAR(500)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    ProcessedOnUtc  DATETIME2        NULL,
    Error           NVARCHAR(MAX)    NULL
);
GO

-- Seed default carrier
INSERT INTO Carriers (Id, TenantId, Name, Code, IsActive, BaseRatePerKg, CreatedAt)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000000',
    'Standard Shipping',
    'STD',
    1,
    2.50,
    GETUTCDATE()
);
GO
