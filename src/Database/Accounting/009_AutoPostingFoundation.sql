-- ============================================================
-- AccountingDB Migration 009 — foundation for automatic GL posting (ROADMAP P1-1)
--   * seeded accounts used 0-based AccountType / NormalBalance while the code, validators and admin UI use 1-based values
--     (Asset=1 .. Expense=5, Debit=1, Credit=2): the trial balance and P&L misread the seed;
--   * journal entries written by the application (not by the demo seed) were left Draft (Status 0) by the column default;
--   * an idempotency key so a redelivered event can never post twice (SourceKey);
--   * the account mapping ("posting profile") and the per-tenant journal number sequence.
-- JournalStatus: 0 Draft, 1 Posted, 2 Voided (excluded from reports), 3 Reversed (still counted: its reversal entry offsets it).
-- Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Seeded chart of accounts (SYSTEM tenant) -> the numbering the application uses.
UPDATE a SET a.AccountType = v.AccountType, a.NormalBalance = v.NormalBalance
FROM Accounts a
JOIN (VALUES
    ('1000', 1, 1), ('1100', 1, 1), ('1200', 1, 1), ('1500', 1, 1),
    ('2000', 2, 2), ('2100', 2, 2), ('2200', 2, 2),
    ('3000', 3, 2), ('3100', 3, 2),
    ('4000', 4, 2), ('4100', 4, 2),
    ('5000', 5, 1), ('5100', 5, 1), ('5200', 5, 1)
) v(AccountNumber, AccountType, NormalBalance) ON v.AccountNumber = a.AccountNumber
WHERE a.TenantId = '00000000-0000-0000-0000-000000000001'
  AND (a.AccountType <> v.AccountType OR a.NormalBalance <> v.NormalBalance);
GO

-- 2. Entries created through the application (no reference, never marked posted) were "posted" by the old handler.
UPDATE JournalEntries SET Status = 1, PostedAt = ISNULL(PostedAt, CreatedAt)
WHERE Status = 0 AND ReferenceType IS NULL AND PostedAt IS NULL;
GO

-- 3. Idempotency key of automatic postings: "<SourceType>:<SourceId>". Legacy / manual entries keep NULL.
IF COL_LENGTH('JournalEntries', 'SourceKey') IS NULL
    ALTER TABLE JournalEntries ADD SourceKey NVARCHAR(150) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_JE_SourceKey' AND object_id = OBJECT_ID('JournalEntries'))
    CREATE UNIQUE INDEX UX_JE_SourceKey ON JournalEntries (TenantId, SourceKey) WHERE SourceKey IS NOT NULL;
GO

-- 4. Posting profile: which account each posting key uses, per tenant.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PostingAccountMappings')
BEGIN
    CREATE TABLE PostingAccountMappings
    (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        PostingKey  NVARCHAR(50)     NOT NULL,
        AccountId   UNIQUEIDENTIFIER NOT NULL,
        Description NVARCHAR(MAX)    NULL,
        IsDeleted   BIT              NOT NULL DEFAULT 0,
        CreatedBy   NVARCHAR(256)    NULL,
        CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy   NVARCHAR(256)    NULL,
        UpdatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion  ROWVERSION,
        CONSTRAINT FK_PostingMapping_Account FOREIGN KEY (AccountId) REFERENCES Accounts(Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PostingAccountMappings_Key' AND object_id = OBJECT_ID('PostingAccountMappings'))
    CREATE UNIQUE INDEX UX_PostingAccountMappings_Key ON PostingAccountMappings (TenantId, PostingKey) WHERE IsDeleted = 0;
GO

-- 5. Journal entry numbers: JE-<year>-<000001> per tenant (atomic MERGE ... OUTPUT in the application).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JournalNumberSequences')
BEGIN
    CREATE TABLE JournalNumberSequences
    (
        TenantId  UNIQUEIDENTIFIER NOT NULL,
        [Year]    INT              NOT NULL,
        LastValue INT              NOT NULL,
        CONSTRAINT PK_JournalNumberSequences PRIMARY KEY (TenantId, [Year])
    );
END
GO
