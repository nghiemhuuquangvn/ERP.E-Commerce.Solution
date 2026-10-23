-- ============================================================
-- NotificationDB Migration 011 — BaseEntity columns on the entity tables (ROADMAP P0-9)
-- Every entity that derives from BaseEntity maps CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted,
-- Description and RowVersion. The original CREATE TABLE scripts of the tables below lack some of them; they used
-- to be added by the hand-run script src/Database/AllServices/004_FixMissingBaseEntityColumns.ps1 (now removed,
-- it carried a password), so a database built from the SQL chain alone failed on the first read or write.
-- Adds only what is missing; idempotent. Found by LiveSchemaDriftTests.
-- ============================================================

USE NotificationDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @tables TABLE (Name SYSNAME);
INSERT @tables (Name) VALUES ('NotificationLogs'),('NotificationPreferences'),('NotificationTemplates'),('Notifications');

DECLARE @t SYSNAME, @sql NVARCHAR(MAX);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @tables WHERE OBJECT_ID(Name) IS NOT NULL;
OPEN c;
FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'';
    IF COL_LENGTH(@t, 'CreatedAt') IS NULL
        SET @sql += N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT ' + QUOTENAME('DF_' + @t + '_CreatedAt') + N' DEFAULT GETUTCDATE();';
    IF COL_LENGTH(@t, 'UpdatedAt') IS NULL
        SET @sql += N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD UpdatedAt DATETIME2 NOT NULL CONSTRAINT ' + QUOTENAME('DF_' + @t + '_UpdatedAt') + N' DEFAULT GETUTCDATE();';
    IF COL_LENGTH(@t, 'CreatedBy') IS NULL
        SET @sql += N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD CreatedBy NVARCHAR(256) NULL;';
    IF COL_LENGTH(@t, 'UpdatedBy') IS NULL
        SET @sql += N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD UpdatedBy NVARCHAR(256) NULL;';
    IF COL_LENGTH(@t, 'IsDeleted') IS NULL
        SET @sql += N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD IsDeleted BIT NOT NULL CONSTRAINT ' + QUOTENAME('DF_' + @t + '_IsDeleted') + N' DEFAULT 0;';
    IF COL_LENGTH(@t, 'Description') IS NULL
        SET @sql += N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD Description NVARCHAR(MAX) NULL;';
    IF COL_LENGTH(@t, 'RowVersion') IS NULL
        SET @sql += N'ALTER TABLE ' + QUOTENAME(@t) + N' ADD RowVersion ROWVERSION NOT NULL;';

    IF @sql <> N'' EXEC (@sql);
    FETCH NEXT FROM c INTO @t;
END
CLOSE c;
DEALLOCATE c;
GO
