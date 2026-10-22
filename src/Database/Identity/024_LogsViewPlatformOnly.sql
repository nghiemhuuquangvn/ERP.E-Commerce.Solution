-- ============================================================
-- AuthDB Migration 024 — logs:view becomes a platform permission (2026-10-22)
--
-- Centralised logs (LoggingService, Elasticsearch `service-logs*`) carry no TenantId: they
-- hold every tenant's requests, errors and stack traces. Seed 013 granted logs:view to the
-- ADMIN role template of every tenant, so any tenant admin could read other tenants' logs.
-- logs:view is now in PermissionScopes.PlatformPermissions (src/Shared/Authorization):
-- stripped from tokens outside the SYSTEM tenant and not grantable there. This migration
-- removes the existing grants outside the SYSTEM tenant and the unused duplicate code
-- 'logging:logs:view' (seed 001; nothing checks it).
--
-- KEEP IN SYNC with PermissionScopes.PlatformPermissions and 021's list.
-- Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @PlatformTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

-- 1) No role outside the platform tenant keeps logs:view.
DELETE rp
FROM RolePermissions rp
JOIN Roles r ON r.Id = rp.RoleId
JOIN Permissions p ON p.Id = rp.PermissionId
WHERE p.PermissionCode = 'logs:view'
  AND r.TenantId <> @PlatformTenantId;

IF OBJECT_ID('UserPermissions') IS NOT NULL
    DELETE up
    FROM UserPermissions up
    JOIN Permissions p ON p.Id = up.PermissionId
    WHERE p.PermissionCode = 'logs:view'
      AND up.TenantId <> @PlatformTenantId;

-- 2) The platform super-admin holds it (021 granted the whole catalog; repeat in case
--    logs:view was seeded after 021 ran).
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE r.TenantId = @PlatformTenantId AND r.RoleCode = 'SuperAdmin'
  AND p.PermissionCode = 'logs:view'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

-- 3) Drop the unused duplicate permission code.
DELETE rp
FROM RolePermissions rp
JOIN Permissions p ON p.Id = rp.PermissionId
WHERE p.PermissionCode = 'logging:logs:view';

IF OBJECT_ID('UserPermissions') IS NOT NULL
    DELETE up
    FROM UserPermissions up
    JOIN Permissions p ON p.Id = up.PermissionId
    WHERE p.PermissionCode = 'logging:logs:view';

DELETE FROM Permissions WHERE PermissionCode = 'logging:logs:view';
GO

-- 4) Report (not auto-fixed, same as 021): active API keys outside the platform tenant
--    whose scopes include logs:view. Revoke them manually.
SELECT k.Id, k.TenantId, k.Name, k.KeyPrefix, k.Scopes
FROM ApiKeys k
WHERE k.IsActive = 1
  AND k.TenantId <> '00000000-0000-0000-0000-000000000001'
  AND k.Scopes LIKE '%logs:view%';
GO
