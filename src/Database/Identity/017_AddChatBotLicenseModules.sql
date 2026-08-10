-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 017: ChatBot module catalog entries + LicensePlan wiring.
-- Adds "CHB" (ChatBotService gate, checked by its ModuleIdentity/
-- LicenseEnforcementMiddleware like every other service) and "CHB_PREMIUM" (a
-- tier flag, not a real service — read directly via UserContext.HasModule inside
-- ChatBotService to pick Claude vs the free ChatGPT provider; see
-- ERP.Shared.Authorization.ModuleCodes and docs/business/Identity/License.md).
-- STARTER gets CHB only (ChatGPT free); PROFESSIONAL/ENTERPRISE get CHB +
-- CHB_PREMIUM (Claude). Reuses the existing License/LicensePlan mechanism —
-- no new billing/subscription infrastructure.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Module catalog additions
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'CHB')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000015', 'CHB', 'AI ChatBot', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'CHB_PREMIUM')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000016', 'CHB_PREMIUM', 'AI ChatBot — Claude AI (Premium)', 1);
GO

-- ============================================================
-- LicensePlanModules wiring: STARTER -> CHB only; PROFESSIONAL/ENTERPRISE -> CHB + CHB_PREMIUM
-- ============================================================
DECLARE @StarterPlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000001';
DECLARE @ProfessionalPlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000002';
DECLARE @EnterprisePlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @StarterPlanId AND ModuleCode = 'CHB')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000028', @StarterPlanId, 'CHB');

IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'CHB')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000029', @ProfessionalPlanId, 'CHB');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'CHB_PREMIUM')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000030', @ProfessionalPlanId, 'CHB_PREMIUM');

IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'CHB')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000031', @EnterprisePlanId, 'CHB');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'CHB_PREMIUM')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000032', @EnterprisePlanId, 'CHB_PREMIUM');
GO

-- ============================================================
-- Existing tenants already issued against STARTER/PROFESSIONAL/ENTERPRISE before this
-- migration hold a TenantLicenseModules snapshot taken at issue time (see License.md —
-- editing a plan does not retroactively change already-issued tenants). Backfill CHB/
-- CHB_PREMIUM onto currently-valid tenant licenses whose plan now includes them, so
-- existing tenants get ChatBot access without a SuperAdmin having to manually re-issue
-- every one of them.
-- ============================================================
INSERT INTO TenantLicenseModules (Id, TenantLicenseId, ModuleCode)
SELECT NEWID(), tl.Id, 'CHB'
FROM TenantLicenses tl
WHERE tl.PlanId IN (
        '41000000-0000-0000-0000-000000000001',
        '41000000-0000-0000-0000-000000000002',
        '41000000-0000-0000-0000-000000000003'
      )
  AND tl.Status = 'Active'
  AND NOT EXISTS (
        SELECT 1 FROM TenantLicenseModules tlm
        WHERE tlm.TenantLicenseId = tl.Id AND tlm.ModuleCode = 'CHB'
      );

INSERT INTO TenantLicenseModules (Id, TenantLicenseId, ModuleCode)
SELECT NEWID(), tl.Id, 'CHB_PREMIUM'
FROM TenantLicenses tl
WHERE tl.PlanId IN (
        '41000000-0000-0000-0000-000000000002',
        '41000000-0000-0000-0000-000000000003'
      )
  AND tl.Status = 'Active'
  AND NOT EXISTS (
        SELECT 1 FROM TenantLicenseModules tlm
        WHERE tlm.TenantLicenseId = tl.Id AND tlm.ModuleCode = 'CHB_PREMIUM'
      );
GO
