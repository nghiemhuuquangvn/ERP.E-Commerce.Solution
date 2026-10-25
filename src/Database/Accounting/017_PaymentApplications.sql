-- ============================================================
-- AccountingDB Migration 017 — payment history (ROADMAP P1-2j)
--   PaymentApplications: every payment / refund applied to an invoice or a bill with its day (DocumentType 1 invoice, 2 bill; refunds negative), so the
--   open amount on a past date is exact for the maturity split of B01 / B09. Bills.ApprovedOn and Invoices.CancelledOn keep the other two dates.
--   Backfill: one estimated row per invoice with a paid amount (dated by its last update) and per paid bill; ApprovedOn / CancelledOn from the last update.
--   Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'ApprovedOn')
    ALTER TABLE Bills ADD ApprovedOn DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'CancelledOn')
    ALTER TABLE Invoices ADD CancelledOn DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaymentApplications')
BEGIN
    CREATE TABLE PaymentApplications
    (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId     UNIQUEIDENTIFIER NOT NULL,
        DocumentType INT              NOT NULL,
        DocumentId   UNIQUEIDENTIFIER NOT NULL,
        PaidOn       DATE             NOT NULL,
        Amount       DECIMAL(18,2)    NOT NULL,
        SourceKey    NVARCHAR(150)    NULL,
        IsEstimated  BIT              NOT NULL CONSTRAINT DF_PaymentApplications_Estimated DEFAULT 0,
        Description  NVARCHAR(MAX)    NULL,
        IsDeleted    BIT              NOT NULL DEFAULT 0,
        CreatedBy    NVARCHAR(256)    NULL,
        CreatedAt    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy    NVARCHAR(256)    NULL,
        UpdatedAt    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion   ROWVERSION,
        CONSTRAINT CK_PaymentApplications_Type CHECK (DocumentType IN (1, 2)),
        CONSTRAINT CK_PaymentApplications_Amount CHECK (Amount <> 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentApplications_Document' AND object_id = OBJECT_ID('PaymentApplications'))
    CREATE INDEX IX_PaymentApplications_Document ON PaymentApplications (TenantId, DocumentType, DocumentId, PaidOn);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PaymentApplications_SourceKey' AND object_id = OBJECT_ID('PaymentApplications'))
    CREATE UNIQUE INDEX UX_PaymentApplications_SourceKey ON PaymentApplications (TenantId, SourceKey) WHERE SourceKey IS NOT NULL AND IsDeleted = 0;
GO

-- backfill (estimated): invoices with a paid amount, paid bills; only for documents that have no row yet
INSERT INTO PaymentApplications (Id, TenantId, DocumentType, DocumentId, PaidOn, Amount, SourceKey, IsEstimated)
SELECT NEWID(), i.TenantId, 1, i.Id, CAST(COALESCE(i.UpdatedAt, i.CreatedAt) AS DATE), i.AmountPaid, 'backfill:' + LOWER(REPLACE(CONVERT(VARCHAR(36), i.Id), '-', '')), 1
FROM Invoices i
WHERE i.AmountPaid > 0 AND i.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM PaymentApplications p WHERE p.TenantId = i.TenantId AND p.DocumentType = 1 AND p.DocumentId = i.Id);
GO

INSERT INTO PaymentApplications (Id, TenantId, DocumentType, DocumentId, PaidOn, Amount, SourceKey, IsEstimated)
SELECT NEWID(), b.TenantId, 2, b.Id, CAST(COALESCE(b.UpdatedAt, b.CreatedAt) AS DATE), b.TotalAmount, 'backfill:' + LOWER(REPLACE(CONVERT(VARCHAR(36), b.Id), '-', '')), 1
FROM Bills b
WHERE b.Status = 2 AND b.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM PaymentApplications p WHERE p.TenantId = b.TenantId AND p.DocumentType = 2 AND p.DocumentId = b.Id);
GO

UPDATE Bills SET ApprovedOn = COALESCE(CASE WHEN Status = 1 THEN UpdatedAt END, CreatedAt) WHERE ApprovedOn IS NULL AND Status IN (1, 2);
GO

UPDATE Invoices SET CancelledOn = COALESCE(UpdatedAt, CreatedAt) WHERE CancelledOn IS NULL AND Status = 5;
GO
