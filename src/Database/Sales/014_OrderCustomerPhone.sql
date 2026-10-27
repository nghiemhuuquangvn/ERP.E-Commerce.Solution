-- ============================================================
-- SalesDB Migration 014 — the recipient's phone on the order (2026-10-27, ROADMAP P1-11)
--
-- SalesOrders.CustomerPhone: the carriers (GHTK, Viettel Post, GHN) refuse a parcel without a recipient phone. NULL on orders made before this.
-- Idempotent.
-- ============================================================

USE SalesDB;
GO

IF COL_LENGTH('SalesOrders', 'CustomerPhone') IS NULL
    ALTER TABLE SalesOrders ADD CustomerPhone NVARCHAR(30) NULL;
GO
