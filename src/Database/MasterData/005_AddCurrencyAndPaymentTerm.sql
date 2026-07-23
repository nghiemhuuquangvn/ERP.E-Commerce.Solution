-- ============================================================
-- MasterDataDB Migration 005 — Currency, Exchange Rate & Payment Term
-- Adds: Currencies, ExchangeRates, PaymentTerms.
-- All DDL is guarded with IF NOT EXISTS — safe to run multiple times.
-- ============================================================

USE MasterDataDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Currencies
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Currencies')
CREATE TABLE Currencies (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Code            NVARCHAR(20)     NOT NULL,
    Name            NVARCHAR(100)    NOT NULL,
    Symbol          NVARCHAR(10)     NOT NULL,
    DecimalPlaces   INT              NOT NULL DEFAULT 2,
    IsActive        BIT              NOT NULL DEFAULT 1,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Currencies_Code' AND object_id = OBJECT_ID('Currencies'))
    CREATE UNIQUE INDEX IX_Currencies_Code ON Currencies (Code) WHERE IsDeleted = 0;
GO

-- ============================================================
-- ExchangeRates (FK FromCurrencyId -> Currencies, FK ToCurrencyId -> Currencies)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExchangeRates')
CREATE TABLE ExchangeRates (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId            UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    FromCurrencyId      UNIQUEIDENTIFIER NOT NULL,
    ToCurrencyId        UNIQUEIDENTIFIER NOT NULL,
    Rate                DECIMAL(18, 6)   NOT NULL,
    EffectiveDate       DATETIME2        NOT NULL,
    Description         NVARCHAR(MAX)    NULL,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    RowVersion          ROWVERSION,
    CONSTRAINT FK_ExchangeRates_Currencies_From FOREIGN KEY (FromCurrencyId) REFERENCES Currencies(Id),
    CONSTRAINT FK_ExchangeRates_Currencies_To FOREIGN KEY (ToCurrencyId) REFERENCES Currencies(Id)
);
GO

-- Supports the "latest EffectiveDate <= asOfDate" lookup rule
-- (docs/business/MasterData/module-03-master-data.md §8) as a single seek.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExchangeRates_From_To_EffectiveDate' AND object_id = OBJECT_ID('ExchangeRates'))
    CREATE INDEX IX_ExchangeRates_From_To_EffectiveDate ON ExchangeRates (FromCurrencyId, ToCurrencyId, EffectiveDate DESC);
GO

-- ExchangeRate has no Code field — no unique code index.

-- ============================================================
-- PaymentTerms
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaymentTerms')
CREATE TABLE PaymentTerms (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    Code            NVARCHAR(20)     NOT NULL,
    Name            NVARCHAR(200)    NOT NULL,
    NetDays         INT              NOT NULL DEFAULT 0,
    DepositPercent  DECIMAL(5, 2)    NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    Description     NVARCHAR(MAX)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    RowVersion      ROWVERSION
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentTerms_Code' AND object_id = OBJECT_ID('PaymentTerms'))
    CREATE UNIQUE INDEX IX_PaymentTerms_Code ON PaymentTerms (Code) WHERE IsDeleted = 0;
GO
