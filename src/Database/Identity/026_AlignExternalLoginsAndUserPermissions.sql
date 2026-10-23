-- ============================================================
-- AuthDB Migration 026 — columns the EF model needs (ROADMAP P0-9, LiveSchemaDriftTests)
-- ExternalLogin derives from BaseEntity (CreatedAt, TenantId) and UserPermission has IsGranted;
-- the original tables have neither, so linking an external login or granting a user permission failed.
-- Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('ExternalLogins', 'CreatedAt') IS NULL
    ALTER TABLE ExternalLogins ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ExternalLogins_CreatedAt DEFAULT GETUTCDATE();
GO

IF COL_LENGTH('ExternalLogins', 'TenantId') IS NULL
    ALTER TABLE ExternalLogins ADD TenantId UNIQUEIDENTIFIER NULL;
GO

-- A link belongs to the tenant of its user; orphans go to the SYSTEM tenant.
UPDATE e SET e.TenantId = u.TenantId
FROM ExternalLogins e JOIN Users u ON u.Id = e.UserId
WHERE e.TenantId IS NULL;
UPDATE ExternalLogins SET TenantId = '00000000-0000-0000-0000-000000000001' WHERE TenantId IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ExternalLogins') AND name = 'TenantId' AND is_nullable = 1)
    ALTER TABLE ExternalLogins ALTER COLUMN TenantId UNIQUEIDENTIFIER NOT NULL;
GO

IF COL_LENGTH('UserPermissions', 'IsGranted') IS NULL
    ALTER TABLE UserPermissions ADD IsGranted BIT NOT NULL CONSTRAINT DF_UserPermissions_IsGranted DEFAULT 1;
GO
