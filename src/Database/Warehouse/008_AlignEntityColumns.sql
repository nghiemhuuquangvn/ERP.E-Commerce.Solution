-- ============================================================
-- WarehouseDB Migration 008 — columns the EF model needs (ROADMAP P0-9, LiveSchemaDriftTests)
--  * Reservations.ProductId / Quantity: legacy single-product columns; a reservation now holds its products in
--    Reservation_ReservedItems, so the old NOT NULL columns made every reservation insert fail. Kept, allowed NULL.
--  * InventoryValuationSnapshotLines.TenantId: tenant-owned line without the column; taken from its snapshot.
-- Idempotent.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'ProductId' AND is_nullable = 0)
    ALTER TABLE Reservations ALTER COLUMN ProductId UNIQUEIDENTIFIER NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'Quantity' AND is_nullable = 0)
    ALTER TABLE Reservations ALTER COLUMN Quantity INT NULL;
GO

IF COL_LENGTH('InventoryValuationSnapshotLines', 'TenantId') IS NULL
    ALTER TABLE InventoryValuationSnapshotLines ADD TenantId UNIQUEIDENTIFIER NULL;
GO

UPDATE l SET l.TenantId = s.TenantId
FROM InventoryValuationSnapshotLines l JOIN InventoryValuationSnapshots s ON s.Id = l.SnapshotId
WHERE l.TenantId IS NULL;
UPDATE InventoryValuationSnapshotLines SET TenantId = '00000000-0000-0000-0000-000000000001' WHERE TenantId IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryValuationSnapshotLines') AND name = 'TenantId' AND is_nullable = 1)
    ALTER TABLE InventoryValuationSnapshotLines ALTER COLUMN TenantId UNIQUEIDENTIFIER NOT NULL;
GO
