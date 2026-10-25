-- ============================================================
-- AccountingDB Migration 019 — the notes B09 are written once per language (ROADMAP P1-2j)
--   NoteTexts.Language ('vi' or 'en'); existing rows are Vietnamese. One text per tenant, section and language. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NoteTexts') AND name = 'Language')
    ALTER TABLE NoteTexts ADD Language NVARCHAR(5) NOT NULL CONSTRAINT DF_NoteTexts_Language DEFAULT 'vi';
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NoteTexts_Key' AND object_id = OBJECT_ID('NoteTexts'))
    DROP INDEX UX_NoteTexts_Key ON NoteTexts;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NoteTexts_Key_Language' AND object_id = OBJECT_ID('NoteTexts'))
    CREATE UNIQUE INDEX UX_NoteTexts_Key_Language ON NoteTexts (TenantId, SectionKey, Language) WHERE IsDeleted = 0;
GO
