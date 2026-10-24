-- ============================================================
-- AccountingDB Migration 010 — cash-flow statement attributes on accounts (ROADMAP P1-2b)
--   * Accounts.IsCash            : the account holds cash or bank money (the cash-flow statement tracks its movements)
--   * Accounts.CashFlowCategory  : how a movement against this account (as the counter-account of a cash movement) is classified:
--                                  1 operating, 2 investing, 3 financing; NULL = default (equity accounts financing, the rest operating)
--   Existing rows are flagged by their standard numbers (the simple 14-account chart and the Vietnamese chart template); a tenant's own
--   numbers are set through PUT accounts/{id}. The ledger and the cash-flow report scan journal lines by account: IX_JELines_AccountId
--   (migration 001) already covers that.
-- Idempotent.
-- ============================================================

USE AccountingDB;
GO

IF COL_LENGTH('Accounts', 'IsCash') IS NULL
    ALTER TABLE Accounts ADD IsCash BIT NOT NULL CONSTRAINT DF_Accounts_IsCash DEFAULT 0;
GO
IF COL_LENGTH('Accounts', 'CashFlowCategory') IS NULL
    ALTER TABLE Accounts ADD CashFlowCategory INT NULL;
GO

-- cash and bank accounts
UPDATE Accounts SET IsCash = 1
WHERE IsCash = 0 AND AccountNumber IN ('1000', '111', '1111', '1112', '112', '1121', '1122', '113');

-- investing: fixed assets and investments (1500 is the simple chart's fixed assets)
UPDATE Accounts SET CashFlowCategory = 2
WHERE CashFlowCategory IS NULL
  AND AccountNumber IN ('1500', '121', '128', '211', '212', '213', '217', '221', '222', '228', '241');

-- financing: borrowings and bonds (equity accounts are financing by default in the application)
UPDATE Accounts SET CashFlowCategory = 3
WHERE CashFlowCategory IS NULL
  AND AccountNumber IN ('341', '343', '344');
GO
