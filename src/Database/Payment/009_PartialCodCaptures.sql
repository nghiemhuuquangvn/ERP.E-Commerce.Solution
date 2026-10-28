-- ============================================================
-- PaymentDB Migration 009 — Cash on delivery captured per parcel (2026-10-28, ROADMAP R43)
--
-- * Payments.CapturedAmount: the cash collected so far. Payments that were already captured / refunded are fully collected
--   (backfilled with Amount); new status 7 = PartiallyCaptured (some parcels of a COD order delivered and paid, the rest on the way).
-- * PaymentCaptures: one row per parcel's cash; the unique index (TenantId, PaymentId, ShipmentId) makes a redelivered delivery message harmless.
-- Idempotent.
-- ============================================================

USE PaymentDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('Payments', 'CapturedAmount') IS NULL
BEGIN
    ALTER TABLE Payments ADD CapturedAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Payments_CapturedAmount DEFAULT 0 WITH VALUES;
END
GO

-- Status: 2 Captured, 5 Refunded, 6 PartiallyRefunded (see PaymentStatus). Done once: only rows still at 0.
UPDATE Payments SET CapturedAmount = Amount WHERE CapturedAmount = 0 AND Status IN (2, 5, 6);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaymentCaptures')
BEGIN
    CREATE TABLE PaymentCaptures
    (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        PaymentId   UNIQUEIDENTIFIER NOT NULL,
        ShipmentId  UNIQUEIDENTIFIER NOT NULL,
        Amount      DECIMAL(18,2)    NOT NULL,
        CapturedAt  DATETIME2        NOT NULL,
        Description NVARCHAR(MAX)    NULL,
        IsDeleted   BIT              NOT NULL DEFAULT 0,
        CreatedBy   NVARCHAR(200)    NULL,
        CreatedAt   DATETIME2        NOT NULL,
        UpdatedBy   NVARCHAR(200)    NULL,
        UpdatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion  ROWVERSION,
        CONSTRAINT FK_PaymentCaptures_Payments FOREIGN KEY (PaymentId) REFERENCES Payments (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PaymentCaptures_Tenant_Payment_Shipment' AND object_id = OBJECT_ID('PaymentCaptures'))
    CREATE UNIQUE INDEX UX_PaymentCaptures_Tenant_Payment_Shipment ON PaymentCaptures (TenantId, PaymentId, ShipmentId);
GO
