-- ============================================================
-- HrmDB Migration 008 - real payroll (ROADMAP P1-7)
--   * EmployeeCompensations  salary terms of an employee, from an effective date (history kept)
--   * Dependents             people the employee supports (family deduction)
--   * PayrollRateSets/PitBrackets  insurance rates, deductions, caps and the progressive tax table, from an effective date
--   * LeaveBalances          gets the BaseEntity columns so the entity can use the table (annual leave balance)
--   * PaySlips               calculation breakdown (base, allowances, worked days, insurance, tax, employer cost)
-- The old PaySlips.TaxAmount / SocialInsurance columns (SQL-first design, never written) are reused for the income tax and the
-- employee's social insurance. Idempotent.
-- ============================================================

USE HrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PayrollRateSets')
CREATE TABLE PayrollRateSets (
    Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId                    UNIQUEIDENTIFIER NOT NULL,
    EffectiveFrom               DATETIME2        NOT NULL,
    Name                        NVARCHAR(200)    NOT NULL,
    SocialEmployeeRate          DECIMAL(9, 6)    NOT NULL,
    HealthEmployeeRate          DECIMAL(9, 6)    NOT NULL,
    UnemploymentEmployeeRate    DECIMAL(9, 6)    NOT NULL,
    SocialEmployerRate          DECIMAL(9, 6)    NOT NULL,
    HealthEmployerRate          DECIMAL(9, 6)    NOT NULL,
    UnemploymentEmployerRate    DECIMAL(9, 6)    NOT NULL,
    PersonalDeduction           DECIMAL(18, 2)   NOT NULL,
    DependentDeduction          DECIMAL(18, 2)   NOT NULL,
    InsuranceSalaryCap          DECIMAL(18, 2)   NOT NULL,
    UnemploymentSalaryCap       DECIMAL(18, 2)   NOT NULL,
    AnnualLeaveDays             DECIMAL(5, 1)    NOT NULL DEFAULT 12,
    IsConfirmed                 BIT              NOT NULL DEFAULT 0,
    Description                 NVARCHAR(MAX)    NULL,
    IsDeleted                   BIT              NOT NULL DEFAULT 0,
    CreatedBy                   NVARCHAR(MAX)    NULL,
    CreatedAt                   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy                   NVARCHAR(MAX)    NULL,
    UpdatedAt                   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion                  ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PayrollRateSets_Tenant_From')
    CREATE UNIQUE INDEX UX_PayrollRateSets_Tenant_From ON PayrollRateSets (TenantId, EffectiveFrom) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PitBrackets')
CREATE TABLE PitBrackets (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    RateSetId   UNIQUEIDENTIFIER NOT NULL,
    UpTo        DECIMAL(18, 2)   NULL,
    Rate        DECIMAL(9, 6)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CreatedBy   NVARCHAR(MAX)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy   NVARCHAR(MAX)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion  ROWVERSION       NOT NULL,
    CONSTRAINT FK_PitBrackets_RateSet FOREIGN KEY (RateSetId) REFERENCES PayrollRateSets(Id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PitBrackets_RateSet')
    CREATE INDEX IX_PitBrackets_RateSet ON PitBrackets (RateSetId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmployeeCompensations')
CREATE TABLE EmployeeCompensations (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EmployeeId        UNIQUEIDENTIFIER NOT NULL,
    EffectiveFrom     DATETIME2        NOT NULL,
    BaseSalary        DECIMAL(18, 2)   NOT NULL,
    TaxableAllowance  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    ExemptAllowance   DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    InsuranceSalary   DECIMAL(18, 2)   NULL,
    InsuranceEnabled  BIT              NOT NULL DEFAULT 1,
    TaxCode           NVARCHAR(20)     NULL,
    InsuranceNumber   NVARCHAR(20)     NULL,
    Description       NVARCHAR(MAX)    NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0,
    CreatedBy         NVARCHAR(MAX)    NULL,
    CreatedAt         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy         NVARCHAR(MAX)    NULL,
    UpdatedAt         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion        ROWVERSION       NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_EmployeeCompensations_Employee_From')
    CREATE UNIQUE INDEX UX_EmployeeCompensations_Employee_From ON EmployeeCompensations (TenantId, EmployeeId, EffectiveFrom) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Dependents')
CREATE TABLE Dependents (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL,
    EmployeeId    UNIQUEIDENTIFIER NOT NULL,
    FullName      NVARCHAR(200)    NOT NULL,
    Relationship  NVARCHAR(50)     NOT NULL,
    TaxCode       NVARCHAR(20)     NULL,
    FromMonth     DATETIME2        NOT NULL,
    ToMonth       DATETIME2        NULL,
    Description   NVARCHAR(MAX)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CreatedBy     NVARCHAR(MAX)    NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedBy     NVARCHAR(MAX)    NULL,
    UpdatedAt     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion    ROWVERSION      NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dependents_Employee')
    CREATE INDEX IX_Dependents_Employee ON Dependents (TenantId, EmployeeId);
GO

-- LeaveBalances: the table exists since 001 but no entity used it. Add the BaseEntity columns; the unique key gains the tenant.
IF COL_LENGTH('LeaveBalances', 'Description') IS NULL ALTER TABLE LeaveBalances ADD Description NVARCHAR(MAX) NULL;
IF COL_LENGTH('LeaveBalances', 'IsDeleted')   IS NULL ALTER TABLE LeaveBalances ADD IsDeleted BIT NOT NULL CONSTRAINT DF_LeaveBalances_IsDeleted DEFAULT 0;
IF COL_LENGTH('LeaveBalances', 'CreatedBy')   IS NULL ALTER TABLE LeaveBalances ADD CreatedBy NVARCHAR(MAX) NULL;
IF COL_LENGTH('LeaveBalances', 'CreatedAt')   IS NULL ALTER TABLE LeaveBalances ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_LeaveBalances_CreatedAt DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('LeaveBalances', 'UpdatedBy')   IS NULL ALTER TABLE LeaveBalances ADD UpdatedBy NVARCHAR(MAX) NULL;
IF COL_LENGTH('LeaveBalances', 'RowVersion')  IS NULL ALTER TABLE LeaveBalances ADD RowVersion ROWVERSION NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeaveBalance_Key' AND object_id = OBJECT_ID('LeaveBalances'))
    DROP INDEX IX_LeaveBalance_Key ON LeaveBalances;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_LeaveBalances_Tenant_Key')
    CREATE UNIQUE INDEX UX_LeaveBalances_Tenant_Key ON LeaveBalances (TenantId, EmployeeId, Year, LeaveType) WHERE IsDeleted = 0;
GO

-- PaySlips: calculation breakdown. Allowances, TaxAmount and SocialInsurance already exist. BasicSalary was nullable and unwritten:
-- it becomes the base salary of the slip (0 for the old ones).
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'BasicSalary' AND is_nullable = 1)
BEGIN
    EXEC (N'UPDATE PaySlips SET BasicSalary = 0 WHERE BasicSalary IS NULL');
    ALTER TABLE PaySlips ALTER COLUMN BasicSalary DECIMAL(18, 2) NOT NULL;
END
GO
IF COL_LENGTH('PaySlips', 'WorkedDays')           IS NULL ALTER TABLE PaySlips ADD WorkedDays DECIMAL(5, 1) NOT NULL CONSTRAINT DF_PaySlips_WorkedDays DEFAULT 0;
IF COL_LENGTH('PaySlips', 'StandardDays')         IS NULL ALTER TABLE PaySlips ADD StandardDays DECIMAL(5, 1) NOT NULL CONSTRAINT DF_PaySlips_StandardDays DEFAULT 0;
IF COL_LENGTH('PaySlips', 'EmployeeHealth')       IS NULL ALTER TABLE PaySlips ADD EmployeeHealth DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PaySlips_EmployeeHealth DEFAULT 0;
IF COL_LENGTH('PaySlips', 'EmployeeUnemployment') IS NULL ALTER TABLE PaySlips ADD EmployeeUnemployment DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PaySlips_EmployeeUnemployment DEFAULT 0;
IF COL_LENGTH('PaySlips', 'EmployerSocial')       IS NULL ALTER TABLE PaySlips ADD EmployerSocial DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PaySlips_EmployerSocial DEFAULT 0;
IF COL_LENGTH('PaySlips', 'EmployerHealth')       IS NULL ALTER TABLE PaySlips ADD EmployerHealth DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PaySlips_EmployerHealth DEFAULT 0;
IF COL_LENGTH('PaySlips', 'EmployerUnemployment') IS NULL ALTER TABLE PaySlips ADD EmployerUnemployment DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PaySlips_EmployerUnemployment DEFAULT 0;
IF COL_LENGTH('PaySlips', 'TaxableIncome')        IS NULL ALTER TABLE PaySlips ADD TaxableIncome DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PaySlips_TaxableIncome DEFAULT 0;
IF COL_LENGTH('PaySlips', 'DependentCount')       IS NULL ALTER TABLE PaySlips ADD DependentCount INT NOT NULL CONSTRAINT DF_PaySlips_DependentCount DEFAULT 0;
IF COL_LENGTH('PaySlips', 'RateSetId')            IS NULL ALTER TABLE PaySlips ADD RateSetId UNIQUEIDENTIFIER NULL;
GO
