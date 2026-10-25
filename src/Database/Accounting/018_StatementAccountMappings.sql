-- ============================================================
-- AccountingDB Migration 018 — a tenant's own placement of accounts on statement rows (ROADMAP P1-2j)
--   Every account whose number starts with AccountPrefix counts in row RowCode of statement StatementId (B01-DN / B02-DN), in addition to the
--   standard layout. Side: 0 net balance, 1 debit balances only, 2 credit balances only. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StatementAccountMappings')
BEGIN
    CREATE TABLE StatementAccountMappings
    (
        Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId      UNIQUEIDENTIFIER NOT NULL,
        StatementId   NVARCHAR(20)     NOT NULL,
        RowCode       NVARCHAR(20)     NOT NULL,
        AccountPrefix NVARCHAR(30)     NOT NULL,
        Side          INT              NOT NULL CONSTRAINT DF_StatementAccountMappings_Side DEFAULT 0,
        Description   NVARCHAR(MAX)    NULL,
        IsDeleted     BIT              NOT NULL DEFAULT 0,
        CreatedBy     NVARCHAR(256)    NULL,
        CreatedAt     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy     NVARCHAR(256)    NULL,
        UpdatedAt     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion    ROWVERSION,
        CONSTRAINT CK_StatementAccountMappings_Side CHECK (Side IN (0, 1, 2))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_StatementAccountMappings_Key' AND object_id = OBJECT_ID('StatementAccountMappings'))
    CREATE UNIQUE INDEX UX_StatementAccountMappings_Key ON StatementAccountMappings (TenantId, StatementId, RowCode, AccountPrefix) WHERE IsDeleted = 0;
GO
