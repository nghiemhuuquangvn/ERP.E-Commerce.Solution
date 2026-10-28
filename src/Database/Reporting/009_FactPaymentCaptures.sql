-- ============================================================
-- ReportingDB Migration 009 — Cash on delivery captured per parcel (2026-10-28, ROADMAP R43)
-- FactPaymentCaptures: one row per part of a payment (id = the parcel's shipment), so a replayed PaymentProcessed event adds to
-- FactPayments.Amount only once. Idempotent.
-- ============================================================

USE ReportingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FactPaymentCaptures')
CREATE TABLE FactPaymentCaptures (
    CaptureId  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId   UNIQUEIDENTIFIER NOT NULL,
    PaymentId  UNIQUEIDENTIFIER NOT NULL,
    Amount     DECIMAL(18, 2)   NOT NULL,
    CapturedAt DATETIME2        NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FactPaymentCaptures_PaymentId' AND object_id = OBJECT_ID('FactPaymentCaptures'))
    CREATE INDEX IX_FactPaymentCaptures_PaymentId ON FactPaymentCaptures (PaymentId);
GO
