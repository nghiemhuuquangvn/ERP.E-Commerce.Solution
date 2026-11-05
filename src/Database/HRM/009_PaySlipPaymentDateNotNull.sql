-- ============================================================
-- HrmDB Migration 009 - PaySlips.PaymentDate becomes NOT NULL (DEFERRED R131)
--   The model (PaySlip.PaymentDate, PaySlipDto.PaymentDate) needs a value, but migration 003 added the column as NULL and the demo
--   seed left it NULL for pending slips, so listing pay slips without a year / month failed with SqlNullValueException (HTTP 500).
--   Existing NULL rows get the last day of their own month (what PaySlip.Create sets), then the column is tightened. Idempotent.
-- ============================================================

USE HrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PaySlips') AND name = 'PaymentDate' AND is_nullable = 1)
BEGIN
    UPDATE PaySlips
       SET PaymentDate = EOMONTH(DATEFROMPARTS([Year], [Month], 1))
     WHERE PaymentDate IS NULL;

    ALTER TABLE PaySlips ALTER COLUMN PaymentDate DATETIME2 NOT NULL;
END
GO
