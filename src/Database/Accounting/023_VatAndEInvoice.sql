-- ============================================================
-- AccountingDB Migration 023 — VAT on invoices and bills, e-invoices (ROADMAP P1-3)
--   Invoices / Bills: TaxRatePercent (SubTotal = net and TaxAmount exist since 001; TotalAmount stays the gross). Existing rows: net = gross, tax 0. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- SubTotal (the amount before VAT) and TaxAmount already exist on both tables since migration 001; only the rate is new.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'TaxRatePercent')
    ALTER TABLE Invoices ADD TaxRatePercent DECIMAL(9,4) NOT NULL CONSTRAINT DF_Invoices_TaxRate DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'TaxRatePercent')
    ALTER TABLE Bills ADD TaxRatePercent DECIMAL(9,4) NOT NULL CONSTRAINT DF_Bills_TaxRate DEFAULT 0;
GO

UPDATE Invoices SET SubTotal = TotalAmount WHERE SubTotal = 0 AND TaxAmount = 0 AND TotalAmount <> 0;
GO

UPDATE Bills SET SubTotal = TotalAmount WHERE SubTotal = 0 AND TaxAmount = 0 AND TotalAmount <> 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantTaxSettings') AND name = 'EInvoiceProvider')
BEGIN
    ALTER TABLE TenantTaxSettings ADD EInvoiceProvider NVARCHAR(30) NULL;
    ALTER TABLE TenantTaxSettings ADD EInvoiceSeries NVARCHAR(20) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EInvoices')
BEGIN
    CREATE TABLE EInvoices
    (
        Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId         UNIQUEIDENTIFIER NOT NULL,
        InvoiceId        UNIQUEIDENTIFIER NOT NULL,
        Provider         NVARCHAR(30)     NOT NULL,
        Status           INT              NOT NULL,
        Series           NVARCHAR(20)     NOT NULL,
        Number           NVARCHAR(20)     NULL,
        LookupCode       NVARCHAR(40)     NULL,
        ProviderReference NVARCHAR(100)   NULL,
        IssuedAt         DATETIME2        NULL,
        BuyerName        NVARCHAR(200)    NOT NULL,
        BuyerTaxCode     NVARCHAR(20)     NULL,
        BuyerAddress     NVARCHAR(300)    NULL,
        Payload          NVARCHAR(MAX)    NOT NULL,
        Error            NVARCHAR(1000)   NULL,
        CancelReason     NVARCHAR(500)    NULL,
        CancelledAt      DATETIME2        NULL,
        Description      NVARCHAR(MAX)    NULL,
        IsDeleted        BIT              NOT NULL DEFAULT 0,
        CreatedBy        NVARCHAR(256)    NULL,
        CreatedAt        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy        NVARCHAR(256)    NULL,
        UpdatedAt        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion       ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_EInvoices_Invoice_Active' AND object_id = OBJECT_ID('EInvoices'))
    CREATE UNIQUE INDEX UX_EInvoices_Invoice_Active ON EInvoices (TenantId, InvoiceId) WHERE IsDeleted = 0 AND Status <> 2;
GO
