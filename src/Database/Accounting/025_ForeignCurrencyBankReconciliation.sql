-- ============================================================
-- AccountingDB Migration 025 — foreign currency, line-level bank reconciliation (ROADMAP P1-4)
--   Invoices / Bills / PaymentApplications: the document currency, rate and foreign amount (NULL = a document in the base currency).
--   The old CurrencyCode columns of Invoices / Bills (default 'USD', never mapped) are left alone.
--   TenantTaxSettings.BaseCurrencyCode: the currency of the books (default VND).
--   Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'ForeignCurrencyCode')
BEGIN
    ALTER TABLE Invoices ADD ForeignCurrencyCode NVARCHAR(3) NULL;
    ALTER TABLE Invoices ADD ExchangeRate DECIMAL(18,6) NULL;
    ALTER TABLE Invoices ADD ForeignAmount DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'ForeignCurrencyCode')
BEGIN
    ALTER TABLE Bills ADD ForeignCurrencyCode NVARCHAR(3) NULL;
    ALTER TABLE Bills ADD ExchangeRate DECIMAL(18,6) NULL;
    ALTER TABLE Bills ADD ForeignAmount DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaymentApplications') AND name = 'ForeignCurrencyCode')
BEGIN
    ALTER TABLE PaymentApplications ADD ForeignCurrencyCode NVARCHAR(3) NULL;
    ALTER TABLE PaymentApplications ADD ExchangeRate DECIMAL(18,6) NULL;
    ALTER TABLE PaymentApplications ADD ForeignAmount DECIMAL(18,2) NULL;
    ALTER TABLE PaymentApplications ADD FxDifference DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantTaxSettings') AND name = 'BaseCurrencyCode')
    ALTER TABLE TenantTaxSettings ADD BaseCurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_TenantTaxSettings_BaseCurrency DEFAULT 'VND';
GO

-- ---------------------------------------------------------------------------------------------------------
-- Part 2: the lines of a bank statement and their match to the ledger (line-level reconciliation)
-- ---------------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BankStatementLines')
BEGIN
    CREATE TABLE BankStatementLines
    (
        Id                   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER NOT NULL,
        BankStatementId      UNIQUEIDENTIFIER NOT NULL,
        BankAccountId        UNIQUEIDENTIFIER NOT NULL,
        LineDate             DATE             NOT NULL,
        Reference            NVARCHAR(100)    NULL,
        Amount               DECIMAL(18,2)    NOT NULL,
        Balance              DECIMAL(18,2)    NULL,
        MatchStatus          INT              NOT NULL DEFAULT 0,
        MatchedJournalLineId UNIQUEIDENTIFIER NULL,
        JournalEntryId       UNIQUEIDENTIFIER NULL,
        Description          NVARCHAR(500)    NULL,
        IsDeleted            BIT              NOT NULL DEFAULT 0,
        CreatedBy            NVARCHAR(256)    NULL,
        CreatedAt            DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy            NVARCHAR(256)    NULL,
        UpdatedAt            DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion           ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BankStatementLines_Statement' AND object_id = OBJECT_ID('BankStatementLines'))
    CREATE INDEX IX_BankStatementLines_Statement ON BankStatementLines (TenantId, BankStatementId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BankStatementLines_Account' AND object_id = OBJECT_ID('BankStatementLines'))
    CREATE INDEX IX_BankStatementLines_Account ON BankStatementLines (TenantId, BankAccountId, LineDate);
GO

-- a ledger line is matched to at most one statement line
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BankStatementLines_JournalLine' AND object_id = OBJECT_ID('BankStatementLines'))
    CREATE UNIQUE INDEX UX_BankStatementLines_JournalLine ON BankStatementLines (TenantId, MatchedJournalLineId)
        WHERE MatchedJournalLineId IS NOT NULL AND IsDeleted = 0;
GO
