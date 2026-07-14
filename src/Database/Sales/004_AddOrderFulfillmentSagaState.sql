-- ============================================================
-- SalesDB Migration 004 — OrderFulfillmentSagaState table
-- Backs OrderFulfillmentSaga orchestration (Order→Payment→Inventory→Shipment).
-- ============================================================

USE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderFulfillmentSagas')
BEGIN
    CREATE TABLE OrderFulfillmentSagas
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        OrderId             UNIQUEIDENTIFIER NOT NULL,
        CurrentStep         INT NOT NULL,
        Status              INT NOT NULL,
        PaymentId           UNIQUEIDENTIFIER NULL,
        ReservationId       UNIQUEIDENTIFIER NULL,
        ShipmentId          UNIQUEIDENTIFIER NULL,
        Approved            BIT NOT NULL DEFAULT 0,
        InventoryReserved   BIT NOT NULL DEFAULT 0,
        ErrorMessage        NVARCHAR(1000) NULL,
        RetryCount          INT NOT NULL DEFAULT 0,
        LastUpdatedAt       DATETIME2 NOT NULL,
        CompletedAt         DATETIME2 NULL,
        CreatedAt           DATETIME2 NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OrderFulfillmentSagas_OrderId')
    CREATE UNIQUE INDEX UX_OrderFulfillmentSagas_OrderId ON OrderFulfillmentSagas(OrderId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrderFulfillmentSagas_Status_LastUpdatedAt')
    CREATE INDEX IX_OrderFulfillmentSagas_Status_LastUpdatedAt ON OrderFulfillmentSagas(Status, LastUpdatedAt);
GO
