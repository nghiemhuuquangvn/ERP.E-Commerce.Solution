-- ============================================================
-- AuthDB Migration 032 - sales_order:price_override (R113): book an order / quotation line at a price that differs from the system price.
-- Sales and POS price every line themselves now; this permission is the only way a person can book another price (a manual price, noted on the order).
-- Same pattern as 029: the permission row exists so the Roles screen can grant it; only SuperAdmin holds it by default (it gets every permission at token
-- issuance anyway). A shop decides which of its own roles may override prices. Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, v.Module
FROM (VALUES
    ('sales_order:price_override', 'Book an order or quotation line at a price other than the system price', 'sales_order')
) AS v(Code, Name, Module)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.PermissionCode = v.Code);
GO

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode = 'sales_order:price_override'
WHERE r.RoleCode IN ('SuperAdmin')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
