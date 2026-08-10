-- ============================================================
-- NotificationDB Migration 007 — Add read/unread state for the Admin bell
--
-- Notification.Status tracks DELIVERY (Queued/Sending/Sent/Delivered/Failed/
-- Bounced), not whether the recipient has seen it in the bell dropdown — these
-- are separate concerns, so read state gets its own columns rather than
-- overloading Status.
-- ============================================================

USE NotificationDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'IsRead')
    ALTER TABLE Notifications ADD IsRead BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'ReadAt')
    ALTER TABLE Notifications ADD ReadAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Notifications') AND name = 'IX_Notifications_RecipientId_IsRead')
    CREATE INDEX IX_Notifications_RecipientId_IsRead ON Notifications (RecipientId, IsRead);
GO
