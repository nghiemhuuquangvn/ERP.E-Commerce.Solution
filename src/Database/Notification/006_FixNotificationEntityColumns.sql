-- ============================================================
-- NotificationDB Migration 006 — Fix Notification/NotificationLog column gaps
--
-- Pre-existing bug (not introduced by this session's seeding/workflow work,
-- just never surfaced before because SendNotificationCommandHandler was
-- never exercised end-to-end via a live Kafka-consumed event until now):
--   - Notifications table has no RecipientDeviceToken column, but the EF
--     entity (ERP.NTF.Domain.Entities.Notification) has always had that
--     property -> every INSERT failed with "Invalid column name
--     'RecipientDeviceToken'".
--   - NotificationLogs table has no TenantId column, but NotificationLog
--     inherits BaseEntity (which has TenantId) -> every INSERT failed with
--     "Invalid column name 'TenantId'".
--   - Notification.ErrorMessage has no matching column at all (the real
--     column is named LastError) -> fixed in code instead (NotificationDbContext
--     .HasColumnName("LastError"), mirroring the existing OptOutAt/OptedOutAt
--     precedent already in that file), not here.
-- ============================================================

USE NotificationDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'RecipientDeviceToken')
    ALTER TABLE Notifications ADD RecipientDeviceToken NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NotificationLogs') AND name = 'TenantId')
    ALTER TABLE NotificationLogs ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001';
GO
