-- ============================================================
-- AccountingDB Migration 012 — report profile (ROADMAP P1-2e)
--   The header of the statements (company name, tax code, address), the signatories and the amount unit
--   (AmountUnit: 1 VND, 2 thousand VND, 3 million VND). A tenant with no row prints plain VND with no header. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReportProfiles')
BEGIN
    CREATE TABLE ReportProfiles
    (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        CompanyName     NVARCHAR(200)    NOT NULL CONSTRAINT DF_ReportProfiles_Name DEFAULT N'',
        TaxCode         NVARCHAR(20)     NOT NULL CONSTRAINT DF_ReportProfiles_Tax DEFAULT N'',
        Address         NVARCHAR(300)    NOT NULL CONSTRAINT DF_ReportProfiles_Addr DEFAULT N'',
        PreparedBy      NVARCHAR(300)    NOT NULL CONSTRAINT DF_ReportProfiles_Prep DEFAULT N'',
        ChiefAccountant NVARCHAR(300)    NOT NULL CONSTRAINT DF_ReportProfiles_Chief DEFAULT N'',
        Director        NVARCHAR(300)    NOT NULL CONSTRAINT DF_ReportProfiles_Dir DEFAULT N'',
        AmountUnit      INT              NOT NULL CONSTRAINT DF_ReportProfiles_Unit DEFAULT 1,
        Description     NVARCHAR(MAX)    NULL,
        IsDeleted       BIT              NOT NULL DEFAULT 0,
        CreatedBy       NVARCHAR(256)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy       NVARCHAR(256)    NULL,
        UpdatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion      ROWVERSION,
        CONSTRAINT CK_ReportProfiles_Unit CHECK (AmountUnit IN (1, 2, 3))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ReportProfiles_Tenant' AND object_id = OBJECT_ID('ReportProfiles'))
    CREATE UNIQUE INDEX UX_ReportProfiles_Tenant ON ReportProfiles (TenantId) WHERE IsDeleted = 0;
GO
