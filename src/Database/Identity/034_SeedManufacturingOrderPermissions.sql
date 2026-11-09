-- ============================================================
-- AuthDB Migration 034 - Manufacturing Phase 3 permissions (production orders, material, operations, finished goods, interim quality
-- release, stock documents). BA spec 01 section 9; names follow manufacturing:<resource>:<action> (docs/business/Manufacturing/00_Mapping-to-ERP.md).
-- Admin and SuperAdmin of every tenant hold them; a shop decides which of its other roles get them (Roles screen). Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, 'manufacturing'
FROM (VALUES
    ('manufacturing:order:view',                'View production orders'),
    ('manufacturing:order:create',              'Create and edit draft production orders'),
    ('manufacturing:order:release',             'Release production orders'),
    ('manufacturing:order:cancel',              'Cancel production orders'),
    ('manufacturing:order:hold',                'Hold and resume production orders'),
    ('manufacturing:order:change-qty',          'Change the quantity of a released production order'),
    ('manufacturing:order:short-close',         'Complete a production order below the completion tolerance'),
    ('manufacturing:order:reopen',              'Reopen a completed production order'),
    ('manufacturing:order:close',               'Close production orders'),
    ('manufacturing:material:issue',            'Issue material to a production order'),
    ('manufacturing:material:return',           'Return material from a production order'),
    ('manufacturing:material:over-issue',       'Issue material beyond the tolerance'),
    ('manufacturing:operation:execute',         'Start, pause and report production operations'),
    ('manufacturing:operation:complete-manual', 'Complete or reopen an operation by hand'),
    ('manufacturing:receipt:post',              'Receive finished goods of a production order'),
    ('manufacturing:qc:disposition',            'Release finished goods from quality hold (interim, until FQC inspections exist)'),
    ('manufacturing:stock-document:retry',      'Retry or cancel a stock document that is pending or failed')
) AS v(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.PermissionCode = v.Code);
GO

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode LIKE 'manufacturing:%'
WHERE r.RoleCode IN ('SuperAdmin', 'Admin')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
