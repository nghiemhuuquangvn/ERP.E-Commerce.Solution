-- ============================================================
-- AuthDB Migration 023 — Product catalog permissions (2026-10-20 service authz review)
--
-- ProductService's catalog endpoints (products, variants, categories, attributes,
-- images, pricing, bundles, relations, reindex, bulk import/export) only required a
-- signed-in user, so even storefront Customers could change the catalog. They now
-- require these codes — the same ones the Admin SPA already gates on, which did not
-- exist until now (PermissionConstants.Products_*).
--
-- Grants: SuperAdmin gets every permission at token issuance anyway (catalog-wide);
-- tenant roles 'Admin' get all five, 'Manager' gets view/create/edit. Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, 'Product'
FROM (VALUES
    ('products:view',   'View Products'),
    ('products:create', 'Create Products'),
    ('products:edit',   'Edit Products'),
    ('products:delete', 'Delete Products'),
    ('products:manage', 'Bulk Import/Export & Reindex Products')
) AS v(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.PermissionCode = v.Code);
GO

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode IN ('products:view', 'products:create', 'products:edit', 'products:delete', 'products:manage')
WHERE r.RoleCode IN ('SuperAdmin', 'Admin', 'ADMIN')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode IN ('products:view', 'products:create', 'products:edit')
WHERE r.RoleCode IN ('Manager', 'MANAGER')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
