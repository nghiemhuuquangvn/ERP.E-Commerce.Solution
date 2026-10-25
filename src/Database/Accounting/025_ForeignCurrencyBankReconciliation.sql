-- ============================================================
-- AccountingDB Migration 025 — foreign currency, line-level bank reconciliation (ROADMAP P1-4)
--   Invoices / Bills / PaymentApplications: the document currency, rate and foreign amount (NULL = a document in the base currency).
--   The old CurrencyCode columns of Invoices / Bills (default 'USD', never mapped) are left alone.
--   TenantTaxSettings.BaseCurrencyCode: the currency of the books (default VND).
--   Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'ForeignCurrencyCode')
BEGIN
    ALTER TABLE Invoices ADD ForeignCurrencyCode NVARCHAR(3) NULL;
    ALTER TABLE Invoices ADD ExchangeRate DECIMAL(18,6) NULL;
    ALTER TABLE Invoices ADD ForeignAmount DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'ForeignCurrencyCode')
BEGIN
    ALTER TABLE Bills ADD ForeignCurrencyCode NVARCHAR(3) NULL;
    ALTER TABLE Bills ADD ExchangeRate DECIMAL(18,6) NULL;
    ALTER TABLE Bills ADD ForeignAmount DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaymentApplications') AND name = 'ForeignCurrencyCode')
BEGIN
    ALTER TABLE PaymentApplications ADD ForeignCurrencyCode NVARCHAR(3) NULL;
    ALTER TABLE PaymentApplications ADD ExchangeRate DECIMAL(18,6) NULL;
    ALTER TABLE PaymentApplications ADD ForeignAmount DECIMAL(18,2) NULL;
    ALTER TABLE PaymentApplications ADD FxDifference DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantTaxSettings') AND name = 'BaseCurrencyCode')
    ALTER TABLE TenantTaxSettings ADD BaseCurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_TenantTaxSettings_BaseCurrency DEFAULT 'VND';
GO
