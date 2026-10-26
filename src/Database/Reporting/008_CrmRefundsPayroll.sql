-- ============================================================
-- ReportingDB Migration 008 - CRM wins, refunds per payment, pay slip breakdown (ROADMAP P1-6)
--   * FactCrmOpportunities  opportunities CRM marked as won (crm-topic OpportunityWon)
--   * FactPaymentRefunds    one row per refund, so a replayed event counts once and a partial refund is told from a full one
--                           (FactPayments.RefundedAmount already exists; the model now writes it)
--   * FactHRPayslips        gross, deductions, income tax and the employer's insurance next to the net pay (TotalAmount
--                           was always 0: the event sends NetPay)
-- Idempotent.
-- ============================================================

USE ReportingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FactCrmOpportunities')
CREATE TABLE FactCrmOpportunities (
    OpportunityId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    ContactId     UNIQUEIDENTIFIER NOT NULL,
    Name          NVARCHAR(300)    NOT NULL DEFAULT N'',
    Value         DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    WonAt         DATETIME2        NOT NULL,
    DateKey       INT              NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactCrmOpportunities_DateKey')
    CREATE INDEX IX_FactCrmOpportunities_DateKey ON FactCrmOpportunities (TenantId, DateKey);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FactPaymentRefunds')
CREATE TABLE FactPaymentRefunds (
    RefundId   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId   UNIQUEIDENTIFIER NOT NULL,
    PaymentId  UNIQUEIDENTIFIER NOT NULL,
    Amount     DECIMAL(18, 2)   NOT NULL,
    RefundedAt DATETIME2        NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactPaymentRefunds_PaymentId')
    CREATE INDEX IX_FactPaymentRefunds_PaymentId ON FactPaymentRefunds (PaymentId);
GO

IF COL_LENGTH('FactHRPayslips', 'GrossSalary')       IS NULL ALTER TABLE FactHRPayslips ADD GrossSalary DECIMAL(18, 2) NOT NULL CONSTRAINT DF_FactHRPayslips_GrossSalary DEFAULT 0;
IF COL_LENGTH('FactHRPayslips', 'Deductions')        IS NULL ALTER TABLE FactHRPayslips ADD Deductions DECIMAL(18, 2) NOT NULL CONSTRAINT DF_FactHRPayslips_Deductions DEFAULT 0;
IF COL_LENGTH('FactHRPayslips', 'IncomeTax')         IS NULL ALTER TABLE FactHRPayslips ADD IncomeTax DECIMAL(18, 2) NOT NULL CONSTRAINT DF_FactHRPayslips_IncomeTax DEFAULT 0;
IF COL_LENGTH('FactHRPayslips', 'EmployerInsurance') IS NULL ALTER TABLE FactHRPayslips ADD EmployerInsurance DECIMAL(18, 2) NOT NULL CONSTRAINT DF_FactHRPayslips_EmployerInsurance DEFAULT 0;
GO
