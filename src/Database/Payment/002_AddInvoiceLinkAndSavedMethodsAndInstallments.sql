-- ============================================================
-- PaymentDB Schema Update — PaymentService (Port 5009, Code: PAY)
-- Migration 002 (2026-07-03): TransactionRef (webhook correlation), InvoiceId link,
-- SettlementId link, fraud-review flag, saved payment methods, installment plans.
-- Note: Payments.CancelledAt already existed in 001_CreatePaymentDB.sql with no C#
-- property until now (see docs/business/ERP-Business-Analysis.md section 3.6).
-- ============================================================

USE PaymentDB;
GO

-- 001_CreatePaymentDB.sql named this column GatewayRef, but the C# entity (and EF's
-- column-naming convention, since no [Column] override exists) has always used
-- GatewayTransactionId — a genuine name mismatch, not just documentation drift: a
-- database provisioned from the 001 script alone would be missing the column EF
-- actually queries/writes, and Payment.Authorize() would fail against a real DB.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'GatewayRef')
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'GatewayTransactionId')
    EXEC sp_rename 'Payments.GatewayRef', 'GatewayTransactionId', 'COLUMN';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'GatewayTransactionId')
    ALTER TABLE Payments ADD GatewayTransactionId NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'InvoiceId')
    ALTER TABLE Payments ADD InvoiceId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'TransactionRef')
    ALTER TABLE Payments ADD TransactionRef NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'SettlementId')
    ALTER TABLE Payments ADD SettlementId UNIQUEIDENTIFIER NULL REFERENCES Settlements(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'IsFlaggedForReview')
    ALTER TABLE Payments ADD IsFlaggedForReview BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'FraudReviewReason')
    ALTER TABLE Payments ADD FraudReviewReason NVARCHAR(500) NULL;
GO

CREATE INDEX IX_Payments_TransactionRef ON Payments (TransactionRef) WHERE TransactionRef IS NOT NULL;
GO

-- ============================================================
-- Saved Payment Methods (tokenized cards — opaque gateway token only, never a raw PAN)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SavedPaymentMethods')
CREATE TABLE SavedPaymentMethods (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CustomerId      UNIQUEIDENTIFIER NOT NULL,
    Provider        NVARCHAR(50)     NOT NULL,
    Token           NVARCHAR(500)    NOT NULL,
    Brand           NVARCHAR(50)     NULL,
    Last4           NVARCHAR(4)      NULL,
    ExpiryMonth     INT              NULL,
    ExpiryYear      INT              NULL,
    IsDefault       BIT              NOT NULL DEFAULT 0,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NULL
);
CREATE INDEX IX_SavedPaymentMethods_CustomerId ON SavedPaymentMethods (CustomerId);
GO

-- ============================================================
-- Installment Plans
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InstallmentPlans')
CREATE TABLE InstallmentPlans (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    PaymentId           UNIQUEIDENTIFIER NOT NULL REFERENCES Payments(Id),
    TotalInstallments   INT              NOT NULL,
    TotalAmount         DECIMAL(18, 2)   NOT NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt           DATETIME2        NULL
);
CREATE UNIQUE INDEX IX_InstallmentPlans_PaymentId ON InstallmentPlans (PaymentId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InstallmentLines')
CREATE TABLE InstallmentLines (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    InstallmentPlanId   UNIQUEIDENTIFIER NOT NULL REFERENCES InstallmentPlans(Id) ON DELETE CASCADE,
    SequenceNo          INT              NOT NULL,
    Amount              DECIMAL(18, 2)   NOT NULL,
    DueDate             DATETIME2        NOT NULL,
    Status              INT              NOT NULL DEFAULT 0, -- 0=Pending, 1=Paid, 2=Overdue
    PaidAt              DATETIME2        NULL
);
CREATE INDEX IX_InstallmentLines_PlanId ON InstallmentLines (InstallmentPlanId);
GO
