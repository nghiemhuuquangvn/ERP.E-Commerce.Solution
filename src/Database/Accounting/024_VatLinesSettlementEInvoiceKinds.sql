-- ============================================================
-- AccountingDB Migration 024 — VAT lines, e-invoice kinds, auto-issue (ROADMAP P1-3b)
--   VatLines: the lines of an invoice / bill (rate, goods not subject to VAT). A document without lines keeps its single net / rate / tax. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'VatLines')
BEGIN
    CREATE TABLE VatLines
    (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId     UNIQUEIDENTIFIER NOT NULL,
        DocumentType INT              NOT NULL,
        DocumentId   UNIQUEIDENTIFIER NOT NULL,
        Category     INT              NOT NULL,
        RatePercent  DECIMAL(9,4)     NOT NULL,
        Net          DECIMAL(18,2)    NOT NULL,
        Tax          DECIMAL(18,2)    NOT NULL,
        [Text]       NVARCHAR(200)    NULL,
        Description  NVARCHAR(MAX)    NULL,
        IsDeleted    BIT              NOT NULL DEFAULT 0,
        CreatedBy    NVARCHAR(256)    NULL,
        CreatedAt    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy    NVARCHAR(256)    NULL,
        UpdatedAt    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion   ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VatLines_Document' AND object_id = OBJECT_ID('VatLines'))
    CREATE INDEX IX_VatLines_Document ON VatLines (TenantId, DocumentType, DocumentId);
GO

-- e-invoice kinds: replacement and adjustment e-invoices refer to an issued one; auto-issue is a tenant switch
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EInvoices') AND name = 'Kind')
BEGIN
    ALTER TABLE EInvoices ADD Kind INT NOT NULL CONSTRAINT DF_EInvoices_Kind DEFAULT 0;
    ALTER TABLE EInvoices ADD RelatedEInvoiceId UNIQUEIDENTIFIER NULL;
    ALTER TABLE EInvoices ADD Reason NVARCHAR(500) NULL;
    ALTER TABLE EInvoices ADD AdjustNet DECIMAL(18,2) NULL;
    ALTER TABLE EInvoices ADD AdjustTax DECIMAL(18,2) NULL;
END
GO

-- one active original / replacement per invoice; replaced and cancelled ones and any number of adjustments do not count
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_EInvoices_Invoice_Active' AND object_id = OBJECT_ID('EInvoices'))
    DROP INDEX UX_EInvoices_Invoice_Active ON EInvoices;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_EInvoices_Invoice_Active2' AND object_id = OBJECT_ID('EInvoices'))
    CREATE UNIQUE INDEX UX_EInvoices_Invoice_Active2 ON EInvoices (TenantId, InvoiceId) WHERE IsDeleted = 0 AND Status <> 2 AND Status <> 4 AND Kind <> 2;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantTaxSettings') AND name = 'AutoIssueEInvoice')
    ALTER TABLE TenantTaxSettings ADD AutoIssueEInvoice BIT NOT NULL CONSTRAINT DF_TenantTaxSettings_AutoIssue DEFAULT 0;
GO
