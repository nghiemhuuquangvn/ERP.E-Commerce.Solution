-- ============================================================
-- ShippingDB Schema Update — ShippingService (Port 5010, Code: SHP)
-- Migration 002 (2026-07-03): structured recipient address (Ward/District/Province,
-- matching what GHN's API actually needs and what CreateShipmentCommandHandler now
-- sends it), proof-of-delivery fields, delivery-retry counter, return shipments
-- (RMA), and carrier province-aware rate/ETA fields.
-- ============================================================

USE ShippingDB;
GO

-- 001_CreateShippingDB.sql named these columns AddressCity/AddressState; the C#
-- entity (and GHN's own API, which needs Ward/District/Province specifically for a
-- Vietnamese address) now uses AddressDistrict/AddressProvince — same concept,
-- renamed for accuracy rather than left as a misleading synonym.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'AddressCity')
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'AddressDistrict')
    EXEC sp_rename 'Shipments.AddressCity', 'AddressDistrict', 'COLUMN';
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'AddressState')
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'AddressProvince')
    EXEC sp_rename 'Shipments.AddressState', 'AddressProvince', 'COLUMN';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'AddressWard')
    ALTER TABLE Shipments ADD AddressWard NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'DeliveryAttempts')
    ALTER TABLE Shipments ADD DeliveryAttempts INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'ReceivedByName')
    ALTER TABLE Shipments ADD ReceivedByName NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'SignatureUrl')
    ALTER TABLE Shipments ADD SignatureUrl NVARCHAR(1000) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shipments') AND name = 'PhotoUrl')
    ALTER TABLE Shipments ADD PhotoUrl NVARCHAR(1000) NULL;
GO

-- 001_CreateShippingDB.sql typed this column INT, but the EF model and the C#
-- TrackingEvent entity have always treated Status as a string — provisioning the DB
-- from the original script alone would break the first tracking-event write/read.
--
-- Originally a single BEGIN...END batch: ALTER TABLE ADD Status_New followed by an
-- UPDATE referencing Status_New in the SAME batch. SQL Server compiles an entire
-- ad-hoc batch before executing any of it, so the UPDATE's reference to a column
-- the ALTER two lines above it hadn't committed yet failed at compile time with
-- "Invalid column name 'Status_New'" — meaning this conversion has never once
-- succeeded anywhere this migration ran, and Status stayed `int` on every DB,
-- silently breaking every tracking-event read/write against the string-typed
-- entity. Split into separate GO batches so each step sees the prior step's DDL.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentTrackingEvents') AND name = 'Status' AND system_type_id = TYPE_ID('int'))
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentTrackingEvents') AND name = 'Status_New')
    ALTER TABLE ShipmentTrackingEvents ADD Status_New NVARCHAR(50) NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentTrackingEvents') AND name = 'Status_New')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentTrackingEvents') AND name = 'Status' AND system_type_id = TYPE_ID('int'))
BEGIN
    -- Dynamic SQL: when Status_New does not exist the batch is still compiled, and a plain
    -- UPDATE would raise "Invalid column name 'Status_New'" on every re-run.
    EXEC (N'UPDATE ShipmentTrackingEvents SET Status_New = CAST(Status AS NVARCHAR(50))');
    ALTER TABLE ShipmentTrackingEvents DROP COLUMN Status;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentTrackingEvents') AND name = 'Status_New')
    EXEC sp_rename 'ShipmentTrackingEvents.Status_New', 'Status', 'COLUMN';
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentTrackingEvents') AND name = 'Status' AND system_type_id = TYPE_ID('nvarchar') AND is_nullable = 1)
    ALTER TABLE ShipmentTrackingEvents ALTER COLUMN Status NVARCHAR(50) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carriers') AND name = 'InterProvinceSurcharge')
    ALTER TABLE Carriers ADD InterProvinceSurcharge DECIMAL(10, 2) NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carriers') AND name = 'OriginProvince')
    ALTER TABLE Carriers ADD OriginProvince NVARCHAR(100) NULL;
GO

-- ============================================================
-- Return Shipments (RMA — reverse logistics, customer → warehouse)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReturnShipments')
CREATE TABLE ReturnShipments (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OriginalShipmentId  UNIQUEIDENTIFIER NOT NULL REFERENCES Shipments(Id),
    OrderId             UNIQUEIDENTIFIER NOT NULL,
    Reason              NVARCHAR(500)    NOT NULL,
    TrackingNumber      NVARCHAR(100)    NULL,
    Status              INT              NOT NULL DEFAULT 0, -- 0=Requested,1=PickedUp,2=Received,3=Cancelled
    PickedUpAt          DATETIME2        NULL,
    ReceivedAt          DATETIME2        NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt           DATETIME2        NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReturnShipments_OriginalShipmentId')
    CREATE INDEX IX_ReturnShipments_OriginalShipmentId ON ReturnShipments (OriginalShipmentId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReturnShipments_OrderId')
    CREATE INDEX IX_ReturnShipments_OrderId ON ReturnShipments (OrderId);
GO
