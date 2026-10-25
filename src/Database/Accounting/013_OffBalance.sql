-- ============================================================
-- AccountingDB Migration 013 — off-balance register (ROADMAP P1-2i)
--   Movements of the off-balance accounts (class 0, TK 001 – 008). They have one side only, so they are NOT journal entries: this register keeps
--   them outside the double entry. Direction: 1 In (raises the balance), 2 Out (lowers it). Status: 1 Posted, 2 Voided. Idempotent.
--   (The accounts 001 – 008 themselves are added by applying the Vietnamese chart template; AccountType 6 = OffBalance.)
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OffBalanceEntries')
BEGIN
    CREATE TABLE OffBalanceEntries
    (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        AccountId   UNIQUEIDENTIFIER NOT NULL,
        EntryDate   DATE             NOT NULL,
        Direction   INT              NOT NULL,
        Amount      DECIMAL(18,2)    NOT NULL,
        Reference   NVARCHAR(100)    NULL,
        Status      INT              NOT NULL CONSTRAINT DF_OffBalanceEntries_Status DEFAULT 1,
        VoidReason  NVARCHAR(500)    NULL,
        VoidedAt    DATETIME2        NULL,
        Description NVARCHAR(MAX)    NULL,
        IsDeleted   BIT              NOT NULL DEFAULT 0,
        CreatedBy   NVARCHAR(256)    NULL,
        CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy   NVARCHAR(256)    NULL,
        UpdatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion  ROWVERSION,
        CONSTRAINT CK_OffBalanceEntries_Direction CHECK (Direction IN (1, 2)),
        CONSTRAINT CK_OffBalanceEntries_Status CHECK (Status IN (1, 2)),
        CONSTRAINT CK_OffBalanceEntries_Amount CHECK (Amount > 0),
        CONSTRAINT FK_OffBalanceEntries_Account FOREIGN KEY (AccountId) REFERENCES Accounts(Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OffBalanceEntries_Account_Date' AND object_id = OBJECT_ID('OffBalanceEntries'))
    CREATE INDEX IX_OffBalanceEntries_Account_Date ON OffBalanceEntries (TenantId, AccountId, EntryDate);
GO
