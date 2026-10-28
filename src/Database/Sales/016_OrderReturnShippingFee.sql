-- ============================================================
-- SalesDB Migration 016 - whether a return gave the delivery fee back (2026-10-28, ROADMAP P1-11c)
--
-- SalesOrders.ReturnShippingFeeRefunded: the approver ticked "also refund the delivery fee" when completing the return; 0 on every earlier order.
-- Idempotent.
-- ============================================================

USE SalesDB;
GO

IF COL_LENGTH('SalesOrders', 'ReturnShippingFeeRefunded') IS NULL
    ALTER TABLE SalesOrders ADD ReturnShippingFeeRefunded BIT NOT NULL CONSTRAINT DF_SalesOrders_ReturnShippingFeeRefunded DEFAULT 0;
GO
