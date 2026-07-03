-- ============================================================
-- PosDB Schema Update — POSService (Port 5011, Code: POS)
-- Migration 002 (2026-07-03): split payment (TransactionTenders), cash denomination
-- tracking (CashDenominationLines), and two real type mismatches found in the
-- ground-truth audit (see docs/business/ERP-Business-Analysis.md section 3.8).
-- Note: Transactions.PromotionId/VoidedAt and TransactionItems.DiscountAmount/
-- TaxAmount/LineTotal already existed in 001_CreatePosDB.sql with no corresponding
-- C# property until now — no migration needed for those, just documenting.
-- ============================================================

USE PosDB;
GO

-- 001_CreatePosDB.sql typed CashierId as NVARCHAR(100) on both CashShifts and
-- Transactions, but the C# entities have always used Guid — a DB provisioned from
-- the original script would reject every EF write to this column.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CashShifts') AND name = 'CashierId' AND system_type_id = TYPE_ID('nvarchar'))
BEGIN
    ALTER TABLE CashShifts ADD CashierId_New UNIQUEIDENTIFIER NULL;
    ALTER TABLE CashShifts DROP COLUMN CashierId;
    EXEC sp_rename 'CashShifts.CashierId_New', 'CashierId', 'COLUMN';
    ALTER TABLE CashShifts ALTER COLUMN CashierId UNIQUEIDENTIFIER NOT NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'CashierId' AND system_type_id = TYPE_ID('nvarchar'))
BEGIN
    ALTER TABLE Transactions ADD CashierId_New UNIQUEIDENTIFIER NULL;
    ALTER TABLE Transactions DROP COLUMN CashierId;
    EXEC sp_rename 'Transactions.CashierId_New', 'CashierId', 'COLUMN';
    ALTER TABLE Transactions ALTER COLUMN CashierId UNIQUEIDENTIFIER NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'ExternalPaymentId')
    ALTER TABLE Transactions ADD ExternalPaymentId UNIQUEIDENTIFIER NULL;
GO

-- ============================================================
-- Transaction Tenders (split payment — cash + card on the same sale)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TransactionTenders')
CREATE TABLE TransactionTenders (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TransactionId   UNIQUEIDENTIFIER NOT NULL REFERENCES Transactions(Id) ON DELETE CASCADE,
    PaymentMethod   NVARCHAR(50)     NOT NULL,
    Amount          DECIMAL(18, 2)   NOT NULL
);
CREATE INDEX IX_TransactionTenders_TransactionId ON TransactionTenders (TransactionId);
GO

-- ============================================================
-- Cash Denomination Lines (cash count breakdown at shift open/close)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CashDenominationLines')
CREATE TABLE CashDenominationLines (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CashShiftId     UNIQUEIDENTIFIER NOT NULL REFERENCES CashShifts(Id) ON DELETE CASCADE,
    Stage           INT              NOT NULL, -- 0=Opening, 1=Closing
    Denomination    DECIMAL(18, 2)   NOT NULL,
    Count           INT              NOT NULL
);
CREATE INDEX IX_CashDenominationLines_ShiftId ON CashDenominationLines (CashShiftId, Stage);
GO
