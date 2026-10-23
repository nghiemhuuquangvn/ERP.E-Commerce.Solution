-- ============================================================
-- ShippingDB Migration 006 — columns the EF model needs (ROADMAP P0-9, LiveSchemaDriftTests)
--  * Carriers.TrackingUrlTemplate, Shipments.PickedUpAt: new columns.
--  * ReturnShipments (aggregate), ShipmentItems and ShipmentTrackingEvents (owned BaseEntity rows) have no
--    TenantId; it is taken from the parent shipment. ReturnShipments also lacks the other BaseEntity columns.
--  * Shipments.TotalWeight / DeliveredAt and ShipmentItems.Weight are mapped to the existing
--    TotalWeightKg / ActualDelivery / WeightKg columns in ShippingDbContext.
-- Idempotent.
-- ============================================================

USE ShippingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('Carriers', 'TrackingUrlTemplate') IS NULL ALTER TABLE Carriers ADD TrackingUrlTemplate NVARCHAR(500) NULL;
IF COL_LENGTH('Shipments', 'PickedUpAt') IS NULL ALTER TABLE Shipments ADD PickedUpAt DATETIME2 NULL;
GO

-- ReturnShipments: BaseEntity columns.
IF COL_LENGTH('ReturnShipments', 'TenantId') IS NULL ALTER TABLE ReturnShipments ADD TenantId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('ReturnShipments', 'Description') IS NULL ALTER TABLE ReturnShipments ADD Description NVARCHAR(MAX) NULL;
IF COL_LENGTH('ReturnShipments', 'CreatedBy') IS NULL ALTER TABLE ReturnShipments ADD CreatedBy NVARCHAR(256) NULL;
IF COL_LENGTH('ReturnShipments', 'UpdatedBy') IS NULL ALTER TABLE ReturnShipments ADD UpdatedBy NVARCHAR(256) NULL;
IF COL_LENGTH('ReturnShipments', 'IsDeleted') IS NULL ALTER TABLE ReturnShipments ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ReturnShipments_IsDeleted DEFAULT 0;
IF COL_LENGTH('ReturnShipments', 'RowVersion') IS NULL ALTER TABLE ReturnShipments ADD RowVersion ROWVERSION NOT NULL;
GO

IF COL_LENGTH('ShipmentItems', 'TenantId') IS NULL ALTER TABLE ShipmentItems ADD TenantId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('ShipmentTrackingEvents', 'TenantId') IS NULL ALTER TABLE ShipmentTrackingEvents ADD TenantId UNIQUEIDENTIFIER NULL;
GO

-- The tenant of a child row is the tenant of its shipment; orphans belong to the SYSTEM tenant.
UPDATE r SET r.TenantId = s.TenantId FROM ReturnShipments r JOIN Shipments s ON s.Id = r.OriginalShipmentId WHERE r.TenantId IS NULL;
UPDATE i SET i.TenantId = s.TenantId FROM ShipmentItems i JOIN Shipments s ON s.Id = i.ShipmentId WHERE i.TenantId IS NULL;
UPDATE e SET e.TenantId = s.TenantId FROM ShipmentTrackingEvents e JOIN Shipments s ON s.Id = e.ShipmentId WHERE e.TenantId IS NULL;
UPDATE ReturnShipments SET TenantId = '00000000-0000-0000-0000-000000000001' WHERE TenantId IS NULL;
UPDATE ShipmentItems SET TenantId = '00000000-0000-0000-0000-000000000001' WHERE TenantId IS NULL;
UPDATE ShipmentTrackingEvents SET TenantId = '00000000-0000-0000-0000-000000000001' WHERE TenantId IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ReturnShipments') AND name = 'TenantId' AND is_nullable = 1)
    ALTER TABLE ReturnShipments ALTER COLUMN TenantId UNIQUEIDENTIFIER NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentItems') AND name = 'TenantId' AND is_nullable = 1)
    ALTER TABLE ShipmentItems ALTER COLUMN TenantId UNIQUEIDENTIFIER NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ShipmentTrackingEvents') AND name = 'TenantId' AND is_nullable = 1)
    ALTER TABLE ShipmentTrackingEvents ALTER COLUMN TenantId UNIQUEIDENTIFIER NOT NULL;
GO
