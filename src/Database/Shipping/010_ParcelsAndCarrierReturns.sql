-- ============================================================
-- ShippingDB Migration 010 - several parcels per order, returns through the carrier (2026-10-28, ROADMAP P1-11d)
--
-- * Shipments.ParcelNo / ParcelCount: an order may travel in several parcels (a person splits it). One live (not cancelled) parcel per
--   (TenantId, OrderId, ParcelNo).
-- * ReturnShipments: the reverse booking at the carrier (carrier row, carrier's own code, fee, booking error, last sync).
-- Idempotent.
-- ============================================================

USE ShippingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('Shipments', 'ParcelNo') IS NULL ALTER TABLE Shipments ADD ParcelNo INT NOT NULL CONSTRAINT DF_Shipments_ParcelNo DEFAULT 1;
IF COL_LENGTH('Shipments', 'ParcelCount') IS NULL ALTER TABLE Shipments ADD ParcelCount INT NOT NULL CONSTRAINT DF_Shipments_ParcelCount DEFAULT 1;
GO

-- Before this migration an order could (rarely) hold two live shipments (rebooked by hand): number them so the unique index can be built.
;WITH d AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, OrderId ORDER BY CreatedAt, Id) AS rn
    FROM Shipments WHERE Status <> 7
)
UPDATE s SET ParcelNo = d.rn
FROM Shipments s JOIN d ON d.Id = s.Id
WHERE d.rn > 1 AND s.ParcelNo = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Shipments_Order_Parcel' AND object_id = OBJECT_ID('Shipments'))
    CREATE UNIQUE INDEX UX_Shipments_Order_Parcel ON Shipments (TenantId, OrderId, ParcelNo) WHERE Status <> 7;
GO

IF COL_LENGTH('ReturnShipments', 'CarrierId') IS NULL ALTER TABLE ReturnShipments ADD CarrierId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('ReturnShipments', 'CarrierName') IS NULL ALTER TABLE ReturnShipments ADD CarrierName NVARCHAR(200) NULL;
IF COL_LENGTH('ReturnShipments', 'CarrierOrderCode') IS NULL ALTER TABLE ReturnShipments ADD CarrierOrderCode NVARCHAR(100) NULL;
IF COL_LENGTH('ReturnShipments', 'CarrierFee') IS NULL ALTER TABLE ReturnShipments ADD CarrierFee DECIMAL(18,2) NULL;
IF COL_LENGTH('ReturnShipments', 'BookingError') IS NULL ALTER TABLE ReturnShipments ADD BookingError NVARCHAR(1000) NULL;
IF COL_LENGTH('ReturnShipments', 'LastCarrierSyncAt') IS NULL ALTER TABLE ReturnShipments ADD LastCarrierSyncAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReturnShipments_TrackingNumber' AND object_id = OBJECT_ID('ReturnShipments'))
    CREATE INDEX IX_ReturnShipments_TrackingNumber ON ReturnShipments (TrackingNumber);
GO
