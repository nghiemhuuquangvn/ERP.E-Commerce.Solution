-- ============================================================
-- NotificationDB Migration 010 — NotificationPreferences.UserId (ROADMAP P0-9)
-- The column was nvarchar while the entity uses a Guid (reads throw InvalidCast). Converted in place; the
-- script stops if a stored value is not a GUID. Idempotent.
-- ============================================================

USE NotificationDB;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NotificationPreferences') AND name = 'UserId' AND system_type_id = TYPE_ID('nvarchar'))
BEGIN
    IF EXISTS (SELECT 1 FROM NotificationPreferences WHERE TRY_CONVERT(UNIQUEIDENTIFIER, UserId) IS NULL)
        THROW 50026, 'NotificationPreferences.UserId holds a value that is not a GUID; fix the data before running Notification 010.', 1;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Preferences_UserId' AND object_id = OBJECT_ID('NotificationPreferences'))
        DROP INDEX IX_Preferences_UserId ON NotificationPreferences;
    ALTER TABLE NotificationPreferences ALTER COLUMN UserId UNIQUEIDENTIFIER NOT NULL;
    CREATE UNIQUE INDEX IX_Preferences_UserId ON NotificationPreferences (UserId, TenantId);
END
GO
