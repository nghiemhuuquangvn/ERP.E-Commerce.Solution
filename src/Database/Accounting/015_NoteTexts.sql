-- ============================================================
-- AccountingDB Migration 015 — texts of the notes to the financial statements, B09-DN (ROADMAP P1-2i)
--   The parts of the notes only the accountant can write (accounting policies, standards applied, non-cash transactions, events after the
--   period), one row per tenant and section key. The figures of the notes are computed from the books. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NoteTexts')
BEGIN
    CREATE TABLE NoteTexts
    (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        SectionKey  NVARCHAR(50)     NOT NULL,
        [Text]      NVARCHAR(4000)   NOT NULL CONSTRAINT DF_NoteTexts_Text DEFAULT N'',
        Description NVARCHAR(MAX)    NULL,
        IsDeleted   BIT              NOT NULL DEFAULT 0,
        CreatedBy   NVARCHAR(256)    NULL,
        CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy   NVARCHAR(256)    NULL,
        UpdatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion  ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NoteTexts_Key' AND object_id = OBJECT_ID('NoteTexts'))
    CREATE UNIQUE INDEX UX_NoteTexts_Key ON NoteTexts (TenantId, SectionKey) WHERE IsDeleted = 0;
GO
