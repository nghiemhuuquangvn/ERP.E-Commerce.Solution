-- ============================================================
-- AuthDB Migration 029 - purchasing permissions (ROADMAP P1-8): RFQ, supplier invoices, returns to vendor, match settings
-- Same pattern as 023: SuperAdmin gets every permission at token issuance anyway; the tenant roles 'Admin' get all nine,
-- 'Manager' everything but approving a variance and the settings, 'Purchaser' also without approving. Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, v.Module
FROM (VALUES
    ('rfq:read',                          'View RFQs',                          'rfq'),
    ('rfq:manage',                        'Manage RFQs and quotations',         'rfq'),
    ('supplier_invoice:read',             'View supplier invoices',             'supplier_invoice'),
    ('supplier_invoice:manage',           'Record and match supplier invoices', 'supplier_invoice'),
    ('supplier_invoice:approve',          'Approve matched supplier invoices',  'supplier_invoice'),
    ('supplier_invoice:approve_variance', 'Approve supplier invoices on hold',  'supplier_invoice'),
    ('vendor_return:read',                'View returns to vendor',             'vendor_return'),
    ('vendor_return:manage',              'Manage returns to vendor',           'vendor_return'),
    ('purchasing_settings:manage',        'Manage purchasing match settings',   'purchasing_settings')
) AS v(Code, Name, Module)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.PermissionCode = v.Code);
GO

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode IN ('rfq:read','rfq:manage','supplier_invoice:read','supplier_invoice:manage','supplier_invoice:approve',
                                           'supplier_invoice:approve_variance','vendor_return:read','vendor_return:manage','purchasing_settings:manage')
WHERE r.RoleCode IN ('SuperAdmin', 'Admin', 'ADMIN')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode IN ('rfq:read','rfq:manage','supplier_invoice:read','supplier_invoice:manage','supplier_invoice:approve','vendor_return:read','vendor_return:manage')
WHERE r.RoleCode IN ('Manager', 'MANAGER')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode IN ('rfq:read','rfq:manage','supplier_invoice:read','supplier_invoice:manage','vendor_return:read','vendor_return:manage')
WHERE r.RoleCode IN ('Purchaser', 'PURCHASER')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
