-- ============================================================
-- AuthDB Migration 033 - Manufacturing (ManufacturingService, code MFG, port 5018):
--   1. module catalog entry "MFG" + license plan wiring (PROFESSIONAL and ENTERPRISE; STARTER does not get it),
--   2. the manufacturing:* permissions of Phase 0-2 (master data, settings, item profile, BOM, routing),
--   3. Admin and SuperAdmin hold them; a shop decides which of its own roles get them (Roles screen).
-- Existing tenants hold a TenantLicenseModules snapshot taken at issue time, so MFG is backfilled onto the currently
-- active licenses of those plans (same as 017 did for CHB). Idempotent.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'MFG')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive)
    VALUES ('40000000-0000-0000-0000-000000000017', 'MFG', 'Manufacturing', 1);
GO

DECLARE @ProfessionalPlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000002';
DECLARE @EnterprisePlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'MFG')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000033', @ProfessionalPlanId, 'MFG');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'MFG')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000034', @EnterprisePlanId, 'MFG');
GO

INSERT INTO TenantLicenseModules (Id, TenantLicenseId, ModuleCode)
SELECT NEWID(), tl.Id, 'MFG'
FROM TenantLicenses tl
WHERE tl.PlanId IN ('41000000-0000-0000-0000-000000000002', '41000000-0000-0000-0000-000000000003')
  AND tl.Status = 'Active'
  AND NOT EXISTS (SELECT 1 FROM TenantLicenseModules tlm WHERE tlm.TenantLicenseId = tl.Id AND tlm.ModuleCode = 'MFG');
GO

INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
SELECT NEWID(), v.Code, v.Name, 'manufacturing'
FROM (VALUES
    ('manufacturing:masterdata:view',        'View manufacturing master data (plants, calendars, work centers, machines, reasons, crews)'),
    ('manufacturing:masterdata:manage',      'Manage manufacturing master data'),
    ('manufacturing:settings:manage',        'Manage production parameters of a plant'),
    ('manufacturing:item-profile:manage',    'Manage the manufacturing profile of an item (make / buy, lead times, lot policy)'),
    ('manufacturing:bom:view',               'View bills of materials'),
    ('manufacturing:bom:manage',             'Create and edit draft bills of materials'),
    ('manufacturing:bom:approve',            'Approve and obsolete bills of materials'),
    ('manufacturing:routing:view',           'View routings'),
    ('manufacturing:routing:manage',         'Create and edit draft routings'),
    ('manufacturing:routing:approve',        'Approve and obsolete routings')
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
