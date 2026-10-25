-- ============================================================
-- AccountingDB Migration 023 — VAT on invoices and bills, e-invoices (ROADMAP P1-3)
--   Invoices / Bills: NetAmount, TaxRatePercent, TaxAmount (TotalAmount stays the gross). Existing rows: net = gross, tax 0. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'TaxAmount')
BEGIN
    ALTER TABLE Invoices ADD NetAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Invoices_NetAmount DEFAULT 0;
    ALTER TABLE Invoices ADD TaxRatePercent DECIMAL(9,4) NOT NULL CONSTRAINT DF_Invoices_TaxRate DEFAULT 0;
    ALTER TABLE Invoices ADD TaxAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Invoices_TaxAmount DEFAULT 0;
END
GO

IF EXISTS (SELECT 1 FROM Invoices WHERE NetAmount = 0 AND TaxAmount = 0 AND TotalAmount <> 0)
    UPDATE Invoices SET NetAmount = TotalAmount WHERE NetAmount = 0 AND TaxAmount = 0;
GO
