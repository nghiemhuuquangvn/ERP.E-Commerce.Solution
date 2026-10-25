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
