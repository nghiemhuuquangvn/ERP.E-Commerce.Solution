-- ============================================================
-- SalesDB Migration 013 — what the customer chose to pay with (2026-10-27, ROADMAP P1-10)
--
-- SalesOrders.PaymentMethod: COD | VNPay | MoMo | ZaloPay | Unspecified. NULL = an order that did not say (all orders before P1-10):
-- it keeps the old behaviour (the saga waits for a payment). COD orders do not wait; the cash is collected on delivery.
-- Idempotent.
-- ============================================================

USE SalesDB;
GO

IF COL_LENGTH('SalesOrders', 'PaymentMethod') IS NULL
    ALTER TABLE SalesOrders ADD PaymentMethod NVARCHAR(20) NULL;
GO
