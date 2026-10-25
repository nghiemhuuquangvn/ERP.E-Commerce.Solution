-- ============================================================
-- AccountingDB Migration 022 — loan day count, automatic accrual switch, history of note texts (ROADMAP P1-2l)
--   Loans.DayCountBasis: 0 = the scheduled interest split by days (default), 1 = actual/365, 2 = actual/360 on the outstanding principal.
--   ClosingPolicies.AutoAccrueLoanInterest: the tenant opts in to the month-end accrual job (default off).
--   NoteTextRevisions: append-only history of every change of a note text. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Loans') AND name = 'DayCountBasis')
    ALTER TABLE Loans ADD DayCountBasis INT NOT NULL CONSTRAINT DF_Loans_DayCountBasis DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClosingPolicies') AND name = 'AutoAccrueLoanInterest')
    ALTER TABLE ClosingPolicies ADD AutoAccrueLoanInterest BIT NOT NULL CONSTRAINT DF_ClosingPolicies_AutoAccrue DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NoteTextRevisions')
BEGIN
    CREATE TABLE NoteTextRevisions
    (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        SectionKey  NVARCHAR(50)     NOT NULL,
        Language    NVARCHAR(5)      NOT NULL,
        FiscalYear  INT              NOT NULL,
        [Text]      NVARCHAR(4000)   NOT NULL CONSTRAINT DF_NoteTextRevisions_Text DEFAULT N'',
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NoteTextRevisions_Key' AND object_id = OBJECT_ID('NoteTextRevisions'))
    CREATE INDEX IX_NoteTextRevisions_Key ON NoteTextRevisions (TenantId, SectionKey, Language, FiscalYear, CreatedAt);
GO
