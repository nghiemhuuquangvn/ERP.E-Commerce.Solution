-- ============================================================
-- ProductDB Migration 018 — PriceLists.RowVersion (ROADMAP P0-9)
-- PriceList derives from BaseEntity (optimistic-locking RowVersion) but the table never had the column.
-- Idempotent.
-- ============================================================

USE ProductDB;
GO

IF COL_LENGTH('PriceLists', 'RowVersion') IS NULL
    ALTER TABLE PriceLists ADD RowVersion ROWVERSION NOT NULL;
GO
