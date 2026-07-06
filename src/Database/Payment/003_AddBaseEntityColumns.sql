-- ============================================================
-- PaymentDB Schema Update — PaymentService (Port 5009, Code: PAY)
-- Migration 003: Add missing BaseEntity columns (Description, IsDeleted,
-- CreatedBy, UpdatedBy, RowVersion) to Payments, PaymentRefunds, Settlements.
-- Also adds FailedAt to Payments (set by Payment.Fail()).
-- ============================================================

USE PaymentDB;
GO

-- ============================================================
-- Payments table — missing BaseEntity + FailedAt columns
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'Description')
    ALTER TABLE Payments ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'IsDeleted')
    ALTER TABLE Payments ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'CreatedBy')
    ALTER TABLE Payments ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'UpdatedBy')
    ALTER TABLE Payments ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'RowVersion')
    ALTER TABLE Payments ADD RowVersion ROWVERSION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'FailedAt')
    ALTER TABLE Payments ADD FailedAt DATETIME2 NULL;
GO

-- ============================================================
-- PaymentRefunds table — missing BaseEntity columns
-- (UpdatedAt is also missing from the 001 CREATE TABLE)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaymentRefunds') AND name = 'UpdatedAt')
    ALTER TABLE PaymentRefunds ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaymentRefunds') AND name = 'Description')
    ALTER TABLE PaymentRefunds ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaymentRefunds') AND name = 'IsDeleted')
    ALTER TABLE PaymentRefunds ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaymentRefunds') AND name = 'CreatedBy')
    ALTER TABLE PaymentRefunds ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaymentRefunds') AND name = 'UpdatedBy')
    ALTER TABLE PaymentRefunds ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaymentRefunds') AND name = 'RowVersion')
    ALTER TABLE PaymentRefunds ADD RowVersion ROWVERSION;
GO

-- ============================================================
-- Settlements table — missing BaseEntity columns
-- (UpdatedAt is also missing from the 001 CREATE TABLE)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Settlements') AND name = 'UpdatedAt')
    ALTER TABLE Settlements ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Settlements') AND name = 'Description')
    ALTER TABLE Settlements ADD Description NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Settlements') AND name = 'IsDeleted')
    ALTER TABLE Settlements ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Settlements') AND name = 'CreatedBy')
    ALTER TABLE Settlements ADD CreatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Settlements') AND name = 'UpdatedBy')
    ALTER TABLE Settlements ADD UpdatedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Settlements') AND name = 'RowVersion')
    ALTER TABLE Settlements ADD RowVersion ROWVERSION;
GO
