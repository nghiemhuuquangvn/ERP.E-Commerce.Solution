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
