-- ============================================================
-- AccountingDB Migration 011 — closing policy (ROADMAP P1-2d)
--   ClosingMode: 1 Monthly (every fiscal period writes its closing entry; the behaviour before this script), 2 YearEnd (only the last
--   period of the fiscal year does, for everything not yet closed). FiscalYearEndMonth: the month the fiscal year ends (12 = December).
--   A tenant with no row is Monthly with a December year end. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClosingPolicies')
BEGIN
    CREATE TABLE ClosingPolicies
    (
        Id                 UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId           UNIQUEIDENTIFIER NOT NULL,
        ClosingMode        INT              NOT NULL CONSTRAINT DF_ClosingPolicies_Mode DEFAULT 1,
        FiscalYearEndMonth INT              NOT NULL CONSTRAINT DF_ClosingPolicies_Month DEFAULT 12,
        Description        NVARCHAR(MAX)    NULL,
        IsDeleted          BIT              NOT NULL DEFAULT 0,
        CreatedBy          NVARCHAR(256)    NULL,
        CreatedAt          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy          NVARCHAR(256)    NULL,
        UpdatedAt          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion         ROWVERSION,
        CONSTRAINT CK_ClosingPolicies_Mode CHECK (ClosingMode IN (1, 2)),
        CONSTRAINT CK_ClosingPolicies_Month CHECK (FiscalYearEndMonth BETWEEN 1 AND 12)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ClosingPolicies_Tenant' AND object_id = OBJECT_ID('ClosingPolicies'))
    CREATE UNIQUE INDEX UX_ClosingPolicies_Tenant ON ClosingPolicies (TenantId) WHERE IsDeleted = 0;
GO
