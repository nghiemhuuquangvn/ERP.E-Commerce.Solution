-- ============================================================
-- AccountingDB Migration 003 — TaxRates / BankAccounts / BankStatements
-- All three (TaxRate/BankAccount/BankStatement : AggregateRoot) are fully wired
-- (TaxRatesController, BankAccountsController, AccountingTaxHandlers/
-- AccountingBankHandlers, their repositories), but no migration ever created
-- any of their tables — every read/write 500'd with "Invalid object name".
-- ============================================================

USE AccountingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TaxRates')
CREATE TABLE TaxRates (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(100)    NOT NULL,
    Code        NVARCHAR(20)     NOT NULL,
    Rate        DECIMAL(8, 4)    NOT NULL,
    IsActive    BIT              NOT NULL DEFAULT 1,
    Description NVARCHAR(500)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion  ROWVERSION
);
CREATE UNIQUE INDEX UX_TaxRates_Code ON TaxRates (Code);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BankAccounts')
CREATE TABLE BankAccounts (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    AccountName     NVARCHAR(200)    NOT NULL,
    BankName        NVARCHAR(200)    NOT NULL,
    AccountNumber   NVARCHAR(50)     NOT NULL,
    CurrencyCode    NVARCHAR(10)     NOT NULL DEFAULT 'VND',
    CurrentBalance  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    IsActive        BIT              NOT NULL DEFAULT 1,
    GlAccountId     UNIQUEIDENTIFIER NULL,
    Description     NVARCHAR(MAX)    NULL,
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion      ROWVERSION,
    CONSTRAINT FK_BankAccounts_GlAccount FOREIGN KEY (GlAccountId) REFERENCES Accounts(Id)
);
CREATE INDEX IX_BankAccounts_AccountNumber ON BankAccounts (AccountNumber);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BankStatements')
CREATE TABLE BankStatements (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    BankAccountId   UNIQUEIDENTIFIER NOT NULL,
    StatementDate   DATETIME2        NOT NULL,
    OpeningBalance  DECIMAL(18, 2)   NOT NULL,
    ClosingBalance  DECIMAL(18, 2)   NOT NULL,
    Status          INT              NOT NULL DEFAULT 0,
    ReconciledAt    DATETIME2        NULL,
    Notes           NVARCHAR(1000)   NULL,
    Description     NVARCHAR(MAX)    NULL,
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion      ROWVERSION,
    CONSTRAINT FK_BankStatements_BankAccount FOREIGN KEY (BankAccountId) REFERENCES BankAccounts(Id)
);
CREATE INDEX IX_BankStatements_BankAccountId ON BankStatements (BankAccountId);
CREATE INDEX IX_BankStatements_BankAccountId_StatementDate ON BankStatements (BankAccountId, StatementDate);
GO
