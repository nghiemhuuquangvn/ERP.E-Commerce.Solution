-- ============================================================
-- NotificationDB Migration 015 - links to the record, device tokens (2026-11-05, Admin app notifications)
--   * Notifications.EntityType / EntityId / RouteKey   the record a message is about (PurchaseOrder / SalesOrder / LeaveRequest + its id),
--                                                       so the app opens it when the message is tapped
--   * DeviceTokens   one row per (tenant, user, device): the phone's push token, for push messages (FCM v1; switched off until Firebase is configured)
-- Idempotent.
-- ============================================================

USE NotificationDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('Notifications', 'EntityType') IS NULL
    ALTER TABLE Notifications ADD EntityType NVARCHAR(100) NULL;
GO

IF COL_LENGTH('Notifications', 'EntityId') IS NULL
    ALTER TABLE Notifications ADD EntityId NVARCHAR(100) NULL;
GO

IF COL_LENGTH('Notifications', 'RouteKey') IS NULL
    ALTER TABLE Notifications ADD RouteKey NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeviceTokens')
CREATE TABLE DeviceTokens (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    UserId      UNIQUEIDENTIFIER NOT NULL,
    Token       NVARCHAR(500)    NOT NULL,
    DeviceId    NVARCHAR(200)    NOT NULL,
    Platform    NVARCHAR(20)     NOT NULL DEFAULT 'android',
    LastSeenAt  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    Description NVARCHAR(MAX)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CreatedBy   NVARCHAR(256)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    RowVersion  ROWVERSION
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DeviceTokens_Tenant_Token' AND object_id = OBJECT_ID('DeviceTokens'))
    CREATE UNIQUE INDEX UX_DeviceTokens_Tenant_Token ON DeviceTokens (TenantId, Token);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DeviceTokens_Tenant_User_Device' AND object_id = OBJECT_ID('DeviceTokens'))
    CREATE UNIQUE INDEX UX_DeviceTokens_Tenant_User_Device ON DeviceTokens (TenantId, UserId, DeviceId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeviceTokens_Tenant_User' AND object_id = OBJECT_ID('DeviceTokens'))
    CREATE INDEX IX_DeviceTokens_Tenant_User ON DeviceTokens (TenantId, UserId);
GO
