-- ============================================================
-- AccountingDB Migration 004 — Fix missing BaseEntity columns
-- Accounts/FiscalPeriods/JournalEntries/JournalEntryLines/Invoices/Bills
-- (all AggregateRoot : BaseEntity) were created in 001 without the full
-- BaseEntity column set (Description/CreatedBy/UpdatedBy/IsDeleted/
-- CreatedAt/UpdatedAt/RowVersion). EF Core maps those columns by
-- convention, so every query against these tables threw
-- "Invalid column name '...'" (surfaced as a generic 500), e.g. on
-- GET /acc/api/v1/fiscal-periods. Migrations 002/003 already got this
-- right for TaxRates/BankAccounts/BankStatements/SystemTaxSettings/
-- TenantTaxSettings — this brings the 001 tables in line.
-- All DDL is guarded with IF NOT EXISTS — safe to run multiple times.
-- ============================================================

USE AccountingDB;
GO

-- ============================================================
-- Accounts
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Accounts') AND name = 'Description')
    ALTER TABLE Accounts ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Accounts') AND name = 'RowVersion')
    ALTER TABLE Accounts ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- FiscalPeriods
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FiscalPeriods') AND name = 'Description')
    ALTER TABLE FiscalPeriods ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FiscalPeriods') AND name = 'CreatedBy')
    ALTER TABLE FiscalPeriods ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FiscalPeriods') AND name = 'UpdatedBy')
    ALTER TABLE FiscalPeriods ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FiscalPeriods') AND name = 'IsDeleted')
    ALTER TABLE FiscalPeriods ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FiscalPeriods') AND name = 'RowVersion')
    ALTER TABLE FiscalPeriods ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- JournalEntries
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JournalEntries') AND name = 'RowVersion')
    ALTER TABLE JournalEntries ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- JournalEntryLines
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JournalEntryLines') AND name = 'CreatedBy')
    ALTER TABLE JournalEntryLines ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JournalEntryLines') AND name = 'CreatedAt')
    ALTER TABLE JournalEntryLines ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JournalEntryLines') AND name = 'UpdatedBy')
    ALTER TABLE JournalEntryLines ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JournalEntryLines') AND name = 'UpdatedAt')
    ALTER TABLE JournalEntryLines ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JournalEntryLines') AND name = 'IsDeleted')
    ALTER TABLE JournalEntryLines ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JournalEntryLines') AND name = 'RowVersion')
    ALTER TABLE JournalEntryLines ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- Invoices
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'Description')
    ALTER TABLE Invoices ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'RowVersion')
    ALTER TABLE Invoices ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- Bills
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'Description')
    ALTER TABLE Bills ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'RowVersion')
    ALTER TABLE Bills ADD RowVersion ROWVERSION;
GO
