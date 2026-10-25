-- ============================================================
-- AccountingDB Migration 020 — loan receiving account and accrued interest (ROADMAP P1-2k)
--   Loans.ReceivingAccountId: the asset account (bank / cash) the loan is received on and repaid from; NULL = the Bank key of the posting profile.
--   LoanInstallments.AccruedInterest: interest of the installment already accrued to the books and not yet paid. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Loans') AND name = 'ReceivingAccountId')
    ALTER TABLE Loans ADD ReceivingAccountId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LoanInstallments') AND name = 'AccruedInterest')
    ALTER TABLE LoanInstallments ADD AccruedInterest DECIMAL(18,2) NOT NULL CONSTRAINT DF_LoanInstallments_Accrued DEFAULT 0;
GO
