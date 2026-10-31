-- ============================================================
-- NotificationDB Migration 014 — Webhook delivery log (2026-10-31, ROADMAP P2-1b)
-- WebhookDeliveries: one row per attempt to deliver an event to a webhook endpoint (status, error, duration), incl. the "send test" button.
-- Kept 30 days (NotificationDispatchJob). Idempotent.
-- ============================================================

USE NotificationDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WebhookDeliveries')
CREATE TABLE WebhookDeliveries (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    WebhookEndpointId UNIQUEIDENTIFIER NOT NULL,
    EventType         NVARCHAR(100)    NOT NULL,
    Success           BIT              NOT NULL,
    StatusCode        INT              NULL,
    Error             NVARCHAR(500)    NULL,
    DurationMs        INT              NOT NULL DEFAULT 0,
    Description       NVARCHAR(MAX)    NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0,
    CreatedBy         NVARCHAR(256)    NULL,
    CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy         NVARCHAR(256)    NULL,
    UpdatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion        ROWVERSION
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WebhookDeliveries_Tenant_Endpoint_Created' AND object_id = OBJECT_ID('WebhookDeliveries'))
    CREATE INDEX IX_WebhookDeliveries_Tenant_Endpoint_Created ON WebhookDeliveries (TenantId, WebhookEndpointId, CreatedAt DESC);
GO
