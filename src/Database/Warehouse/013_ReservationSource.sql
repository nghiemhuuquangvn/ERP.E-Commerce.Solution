-- ============================================================
-- WarehouseDB Migration 013 - reservations by source, partial consume (Warehouse upgrade W4, docs/architecture/adr-warehouse-upgrade.md)
--   * Reservations: SourceType (0 SalesOrder - every existing row, 1 ProductionOrder) and SourceReference (e.g. the production order number).
--     OrderId keeps holding the id of the source document.
--   * Reservation_ReservedItems: SourceLineId (the production order component line), WarehouseId (the warehouse the line is held in) and
--     ConsumedQuantity (what has already been taken; the part of the line still held = Quantity - ConsumedQuantity).
--     The lot split (LotAllocationsJson) also records how much of each lot was consumed; old rows read as nothing consumed.
-- Idempotent: columns and the index are created only when missing. No data is changed.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('dbo.Reservations', 'SourceType') IS NULL
    ALTER TABLE dbo.Reservations ADD SourceType TINYINT NOT NULL CONSTRAINT DF_Reservations_SourceType DEFAULT 0;
IF COL_LENGTH('dbo.Reservations', 'SourceReference') IS NULL
    ALTER TABLE dbo.Reservations ADD SourceReference NVARCHAR(100) NULL;
GO

IF COL_LENGTH('dbo.Reservation_ReservedItems', 'SourceLineId') IS NULL
    ALTER TABLE dbo.Reservation_ReservedItems ADD SourceLineId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.Reservation_ReservedItems', 'WarehouseId') IS NULL
    ALTER TABLE dbo.Reservation_ReservedItems ADD WarehouseId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.Reservation_ReservedItems', 'ConsumedQuantity') IS NULL
    ALTER TABLE dbo.Reservation_ReservedItems ADD ConsumedQuantity DECIMAL(18,6) NOT NULL CONSTRAINT DF_Reservation_ReservedItems_Consumed DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Reservations') AND name = 'IX_Reservations_Source')
    CREATE INDEX IX_Reservations_Source ON dbo.Reservations (TenantId, SourceType, OrderId);
GO

PRINT 'Reservations by source: columns ready.';
GO
