-- ============================================================
-- AccountingDB Migration 008 — FiscalPeriods.FiscalYear / PeriodMonth (ROADMAP P0-9)
-- 001 created both as NOT NULL columns the FiscalPeriod entity never writes (it has StartDate/EndDate), so
-- opening a fiscal period failed. They are derived from StartDate, so they become persisted computed
-- columns; the unique (TenantId, FiscalYear, PeriodMonth) index is rebuilt on them.
-- Idempotent.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FiscalPeriods') AND name = 'FiscalYear' AND is_computed = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FiscalPeriods_Period' AND object_id = OBJECT_ID('FiscalPeriods'))
        DROP INDEX IX_FiscalPeriods_Period ON FiscalPeriods;
    ALTER TABLE FiscalPeriods DROP COLUMN FiscalYear;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FiscalPeriods') AND name = 'PeriodMonth' AND is_computed = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FiscalPeriods_Period' AND object_id = OBJECT_ID('FiscalPeriods'))
        DROP INDEX IX_FiscalPeriods_Period ON FiscalPeriods;
    ALTER TABLE FiscalPeriods DROP COLUMN PeriodMonth;
END
GO

IF COL_LENGTH('FiscalPeriods', 'FiscalYear') IS NULL
    ALTER TABLE FiscalPeriods ADD FiscalYear AS YEAR(StartDate) PERSISTED;
GO
IF COL_LENGTH('FiscalPeriods', 'PeriodMonth') IS NULL
    ALTER TABLE FiscalPeriods ADD PeriodMonth AS MONTH(StartDate) PERSISTED;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FiscalPeriods_Period' AND object_id = OBJECT_ID('FiscalPeriods'))
    CREATE UNIQUE INDEX IX_FiscalPeriods_Period ON FiscalPeriods (TenantId, FiscalYear, PeriodMonth);
GO
