-- ============================================================
-- NotificationDB Migration 003 — WebhookEndpoints
-- WebhookEndpoint : AggregateRoot is fully wired (WebhookHandlers,
-- INotificationRepository, NotificationRepository), but no migration ever
-- created its table — every webhook read/write 500'd with
-- "Invalid object name 'WebhookEndpoints'".
-- ============================================================

USE NotificationDB;
GO

-- NotificationTemplate.Variables (string?, unconfigured -> EF default nvarchar(max)) was
-- never added to NotificationTemplates — every GET /templates 500'd with "Invalid column
-- name 'Variables'" once the table-name mismatch above it was fixed and the query actually
-- reached this table for the first time.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NotificationTemplates') AND name = 'Variables')
    ALTER TABLE NotificationTemplates ADD Variables NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WebhookEndpoints')
CREATE TABLE WebhookEndpoints (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Url         NVARCHAR(2000)   NOT NULL,
    Secret      NVARCHAR(500)    NULL,
    EventTypes  NVARCHAR(1000)   NULL,
    IsActive    BIT              NOT NULL DEFAULT 1,
    Description NVARCHAR(MAX)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion  ROWVERSION
);
CREATE INDEX IX_WebhookEndpoints_IsActive ON WebhookEndpoints (IsActive);
CREATE INDEX IX_WebhookEndpoints_TenantId ON WebhookEndpoints (TenantId);
GO
