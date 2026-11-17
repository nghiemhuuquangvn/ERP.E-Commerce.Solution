-- ============================================================
-- AuthDB Migration 037 - Manufacturing Phase 4 (P4-4) permission: use-as-is approval of a non-conformance report.
-- BR-NCR-03: a UseAsIs disposition needs manufacturing:qc:approve-deviation (spec Production.Qc.ApproveDeviation, role R-QAM). Admin and SuperAdmin of every
-- tenant hold it; a shop gives it to its quality managers on the Roles screen. Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, 'manufacturing'
FROM (VALUES
    ('manufacturing:qc:approve-deviation', 'Approve use-as-is (conditional acceptance) of non-conforming goods')
) AS v(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.PermissionCode = v.Code);
GO

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
JOIN Permissions p ON p.PermissionCode = 'manufacturing:qc:approve-deviation'
WHERE r.RoleCode IN ('SuperAdmin', 'Admin')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
GO
