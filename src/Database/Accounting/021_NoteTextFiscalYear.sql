-- ============================================================
-- AccountingDB Migration 021 — versioned note texts (ROADMAP P1-2k)
--   NoteTexts.FiscalYear: the fiscal year (the year in which it ends) a text is written for; 0 = every year (the texts written before this migration).
--   A report reads the text of its year, else the latest earlier year, else year 0. One text per tenant, section, language and year. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NoteTexts') AND name = 'FiscalYear')
    ALTER TABLE NoteTexts ADD FiscalYear INT NOT NULL CONSTRAINT DF_NoteTexts_FiscalYear DEFAULT 0;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NoteTexts_Key_Language' AND object_id = OBJECT_ID('NoteTexts'))
    DROP INDEX UX_NoteTexts_Key_Language ON NoteTexts;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NoteTexts_Key_Language_Year' AND object_id = OBJECT_ID('NoteTexts'))
    CREATE UNIQUE INDEX UX_NoteTexts_Key_Language_Year ON NoteTexts (TenantId, SectionKey, Language, FiscalYear) WHERE IsDeleted = 0;
GO
