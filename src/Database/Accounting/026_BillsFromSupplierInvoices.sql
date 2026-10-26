-- ============================================================
-- AccountingDB Migration 026 - bills from supplier invoices, supplier credits (ROADMAP P1-8)
--   Bills.SupplierInvoiceId   the Purchasing supplier invoice the bill was booked from (unique per tenant when set)
--   Bills.ReceivedValue       the goods of the invoice at the order price: GRNI is cleared by it, the rest of the net is price variance
--   Bills.CreditedAmount      what the supplier credited back for returned goods; the open balance is amount - credited
-- The purchase-price-variance posting key needs no row: it resolves from the chart template. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('Bills', 'SupplierInvoiceId') IS NULL ALTER TABLE Bills ADD SupplierInvoiceId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('Bills', 'ReceivedValue')     IS NULL ALTER TABLE Bills ADD ReceivedValue DECIMAL(18, 2) NULL;
IF COL_LENGTH('Bills', 'CreditedAmount')    IS NULL ALTER TABLE Bills ADD CreditedAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Bills_CreditedAmount DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Bills_SupplierInvoice')
    CREATE UNIQUE INDEX UX_Bills_SupplierInvoice ON Bills (TenantId, SupplierInvoiceId) WHERE SupplierInvoiceId IS NOT NULL;
GO
