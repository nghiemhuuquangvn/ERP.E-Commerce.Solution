-- ============================================================
-- SalesDB Migration 003 — Add BaseEntity columns missing from initial schema
-- Description (nullable string) and RowVersion (optimistic concurrency token)
-- are inherited from BaseEntity but were omitted from 001_CreateSalesDB.sql.
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'Description')
    ALTER TABLE SalesOrders ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrders') AND name = 'RowVersion')
    ALTER TABLE SalesOrders ADD RowVersion ROWVERSION;
GO
