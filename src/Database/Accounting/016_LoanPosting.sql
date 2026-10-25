-- ============================================================
-- AccountingDB Migration 016 — loan ledger posts to the journal (ROADMAP P1-2j)
--   Loans.PostedToLedger: 1 once the drawdown is in the journal, so repayments are posted too. Loans entered before this stay 0 (booked by hand).
--   Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Loans') AND name = 'PostedToLedger')
    ALTER TABLE Loans ADD PostedToLedger BIT NOT NULL CONSTRAINT DF_Loans_PostedToLedger DEFAULT 0;
GO
