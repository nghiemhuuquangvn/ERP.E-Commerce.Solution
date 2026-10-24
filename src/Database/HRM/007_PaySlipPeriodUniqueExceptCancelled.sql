-- ============================================================
-- HrmDB Migration 007 — a cancelled pay slip no longer blocks its period (ROADMAP P1-1d)
--   IX_PaySlips_Period was unique over (EmployeeId, PeriodStart, PeriodEnd) for every row, so a pay slip that was cancelled
--   (PaymentStatus 2) could never be generated again for the same month. The index becomes filtered: unique among the slips
--   that are not cancelled.
-- Idempotent.
-- ============================================================

USE HrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaySlips_Period' AND object_id = OBJECT_ID('PaySlips') AND has_filter = 0)
    DROP INDEX IX_PaySlips_Period ON PaySlips;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaySlips_Period' AND object_id = OBJECT_ID('PaySlips'))
    CREATE UNIQUE INDEX IX_PaySlips_Period ON PaySlips (EmployeeId, PeriodStart, PeriodEnd) WHERE PaymentStatus <> 2;
GO
