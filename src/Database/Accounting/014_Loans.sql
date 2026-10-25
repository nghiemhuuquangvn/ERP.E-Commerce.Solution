-- ============================================================
-- AccountingDB Migration 014 — loan ledger (ROADMAP P1-2i)
--   A loan with its repayment schedule. Each installment keeps the day it was repaid, so the amount outstanding — and the part due after 12 months,
--   which B01 shows as a long-term borrowing — is exact for any past date. Status: 1 Active, 2 Repaid, 3 Cancelled. Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Loans')
BEGIN
    CREATE TABLE Loans
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        LoanNumber          NVARCHAR(50)     NOT NULL,
        Lender              NVARCHAR(200)    NOT NULL,
        Principal           DECIMAL(18,2)    NOT NULL,
        InterestRatePercent DECIMAL(9,4)     NOT NULL CONSTRAINT DF_Loans_Rate DEFAULT 0,
        StartDate           DATE             NOT NULL,
        Status              INT              NOT NULL CONSTRAINT DF_Loans_Status DEFAULT 1,
        CancelReason        NVARCHAR(500)    NULL,
        Description         NVARCHAR(MAX)    NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        CreatedBy           NVARCHAR(256)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy           NVARCHAR(256)    NULL,
        UpdatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION,
        CONSTRAINT CK_Loans_Status CHECK (Status IN (1, 2, 3)),
        CONSTRAINT CK_Loans_Principal CHECK (Principal > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Loans_Number' AND object_id = OBJECT_ID('Loans'))
    CREATE UNIQUE INDEX UX_Loans_Number ON Loans (TenantId, LoanNumber) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LoanInstallments')
BEGIN
    CREATE TABLE LoanInstallments
    (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        LoanId          UNIQUEIDENTIFIER NOT NULL,
        DueDate         DATE             NOT NULL,
        PrincipalAmount DECIMAL(18,2)    NOT NULL,
        InterestAmount  DECIMAL(18,2)    NOT NULL CONSTRAINT DF_LoanInstallments_Interest DEFAULT 0,
        PaidOn          DATE             NULL,
        Description     NVARCHAR(MAX)    NULL,
        IsDeleted       BIT              NOT NULL DEFAULT 0,
        CreatedBy       NVARCHAR(256)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy       NVARCHAR(256)    NULL,
        UpdatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion      ROWVERSION,
        CONSTRAINT CK_LoanInstallments_Principal CHECK (PrincipalAmount > 0),
        CONSTRAINT FK_LoanInstallments_Loan FOREIGN KEY (LoanId) REFERENCES Loans(Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoanInstallments_Loan' AND object_id = OBJECT_ID('LoanInstallments'))
    CREATE INDEX IX_LoanInstallments_Loan ON LoanInstallments (LoanId);
GO
