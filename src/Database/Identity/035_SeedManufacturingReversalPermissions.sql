-- ============================================================
-- AuthDB Migration 035 - Manufacturing Phase 4 (P4-2) permissions: reversal of operation reports and of finished-goods receipts.
-- Names follow manufacturing:<resource>:<action> (docs/business/Manufacturing/00_Mapping-to-ERP.md). Admin and SuperAdmin of every tenant hold them;
-- a shop decides which of its other roles get them (Roles screen). Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, 'manufacturing'
FROM (VALUES
    ('manufacturing:operation:reverse', 'Reverse an operation report (and the material its backflush consumed)'),
    ('manufacturing:receipt:reverse',   'Reverse a finished-goods receipt')
) AS v(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.PermissionCode = v.Code);
GO

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode IN ('manufacturing:operation:reverse', 'manufacturing:receipt:reverse')
WHERE r.RoleCode IN ('SuperAdmin', 'Admin')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
