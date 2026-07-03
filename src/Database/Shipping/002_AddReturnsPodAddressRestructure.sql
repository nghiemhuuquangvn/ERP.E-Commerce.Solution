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
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentTrackingEvents') AND name = 'Status' AND system_type_id = TYPE_ID('int'))
BEGIN
    ALTER TABLE ShipmentTrackingEvents ADD Status_New NVARCHAR(50) NULL;
    UPDATE ShipmentTrackingEvents SET Status_New = CAST(Status AS NVARCHAR(50));
    ALTER TABLE ShipmentTrackingEvents DROP COLUMN Status;
    EXEC sp_rename 'ShipmentTrackingEvents.Status_New', 'Status', 'COLUMN';
    ALTER TABLE ShipmentTrackingEvents ALTER COLUMN Status NVARCHAR(50) NOT NULL;
END
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
CREATE INDEX IX_ReturnShipments_OriginalShipmentId ON ReturnShipments (OriginalShipmentId);
CREATE INDEX IX_ReturnShipments_OrderId ON ReturnShipments (OrderId);
GO
