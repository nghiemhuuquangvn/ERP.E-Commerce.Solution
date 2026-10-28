-- ============================================================
-- AccountingDB Migration 027 - delivery revenue on the invoice (2026-10-28, ROADMAP P1-11c)
--   Invoices.ShippingNetAmount   the part of the net amount that is delivery charged to the customer; booked on the ShippingRevenue posting key
-- The ShippingRevenue key needs no row: it resolves from the chart template (4000 on the simple chart, 5113 on Vas99), else it books with sales revenue. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('Invoices', 'ShippingNetAmount') IS NULL
    ALTER TABLE Invoices ADD ShippingNetAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Invoices_ShippingNetAmount DEFAULT 0;
GO
