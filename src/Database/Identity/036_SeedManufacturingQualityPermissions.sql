-- ============================================================
-- AuthDB Migration 036 - Manufacturing Phase 4 (P4-3) permissions: inspection plans and IPQC / FQC inspections.
-- Names follow manufacturing:<resource>:<action> (docs/business/Manufacturing/00_Mapping-to-ERP.md; spec Production.Qc.*). Admin and SuperAdmin of every tenant
-- hold them; a shop decides which of its other roles get them (Roles screen). Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, 'manufacturing'
FROM (VALUES
    ('manufacturing:qc:plan-view',    'View inspection plans'),
    ('manufacturing:qc:plan-manage',  'Create and edit draft inspection plans (copy, delete)'),
    ('manufacturing:qc:plan-approve', 'Approve or make obsolete an inspection plan'),
    ('manufacturing:qc:view',         'View IPQC / FQC inspections'),
    ('manufacturing:qc:inspect',      'Enter the results of an inspection and submit it'),
    ('manufacturing:qc:reinspect',    'Re-inspect a failed inspection')
) AS v(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.PermissionCode = v.Code);
GO

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode IN ('manufacturing:qc:plan-view', 'manufacturing:qc:plan-manage', 'manufacturing:qc:plan-approve',
                                           'manufacturing:qc:view', 'manufacturing:qc:inspect', 'manufacturing:qc:reinspect')
WHERE r.RoleCode IN ('SuperAdmin', 'Admin')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
