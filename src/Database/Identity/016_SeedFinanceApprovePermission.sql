-- ============================================================
-- AuthDB Migration 016 — purchase_order:finance_approve permission
--
-- New second-level ("Finance") approval step for Purchase Orders (see
-- PurchaseOrder.ApproveFinance() in PurchaseOrderService). Mirrors
-- 013_SeedRoleDefinitionsIntoRolePermissions.sql's pattern: SuperAdmin gets
-- every permission automatically (Step 2 there), so this just needs the
-- Permission row to exist; Admin needs an explicit grant since Admin's list
-- there is a fixed, named set, not "everything".
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @PermId UNIQUEIDENTIFIER = NEWID();

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'purchase_order:finance_approve')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermId, 'purchase_order:finance_approve', 'Purchase Order Finance Approve', 'purchasing');
GO

-- SuperAdmin: grant this (and any other permission row not yet linked) —
-- same "every permission that exists" rule as 013's Step 2.
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE r.RoleCode = 'SuperAdmin'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO

-- Admin: explicit grant (Admin's permission set is a fixed named list, not "everything").
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE r.RoleCode = 'Admin'
AND p.PermissionCode = 'purchase_order:finance_approve'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
