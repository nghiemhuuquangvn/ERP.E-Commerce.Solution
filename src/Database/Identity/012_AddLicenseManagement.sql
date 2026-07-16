-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 012: License Management — SuperAdmin-defined module/plan catalogs +
-- per-tenant license issue/renew/suspend/revoke. Enforcement is JWT-claim-based
-- ("licensed_module" claims embedded at login/refresh by AuthService/TokenService,
-- consumed by every other service's UserContext.HasModule), same eventual-consistency
-- model already used for the "permission" claim.
-- ============================================================

USE AuthDB;
GO

-- ============================================================
-- Module catalog — system catalog of the 14 licensable backend module codes
-- (everything except IdentityService, which is the platform itself).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ModuleDefinitions')
CREATE TABLE ModuleDefinitions (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    ModuleCode  NVARCHAR(20)     NOT NULL,
    ModuleName  NVARCHAR(200)    NOT NULL,
    IsActive    BIT              NOT NULL DEFAULT 1
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ModuleDefinitions_ModuleCode')
    CREATE UNIQUE INDEX IX_ModuleDefinitions_ModuleCode ON ModuleDefinitions (ModuleCode);
GO

-- ============================================================
-- License plan catalog — SuperAdmin-defined reusable plans (Starter/Professional/
-- Enterprise). LicensePlanModules is the plan's default module set (mirrors
-- Roles<->Permissions, not a JSON blob column).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LicensePlans')
CREATE TABLE LicensePlans (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    PlanCode    NVARCHAR(50)     NOT NULL,
    PlanName    NVARCHAR(200)    NOT NULL,
    Description NVARCHAR(500)    NULL,
    MaxUsers    INT              NULL,
    IsActive    BIT              NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LicensePlans_PlanCode')
    CREATE UNIQUE INDEX IX_LicensePlans_PlanCode ON LicensePlans (PlanCode);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LicensePlanModules')
CREATE TABLE LicensePlanModules (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    LicensePlanId UNIQUEIDENTIFIER NOT NULL,
    ModuleCode    NVARCHAR(20)     NOT NULL,
    CONSTRAINT FK_LicensePlanModules_LicensePlan FOREIGN KEY (LicensePlanId) REFERENCES LicensePlans(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LicensePlanModules_PlanId_ModuleCode')
    CREATE UNIQUE INDEX IX_LicensePlanModules_PlanId_ModuleCode ON LicensePlanModules (LicensePlanId, ModuleCode);
GO

-- ============================================================
-- Tenant licenses — the issued record per tenant. One current license per tenant
-- (unique on TenantId); a fresh "Issue" after a prior Revoke reuses that same row.
-- Expiry is NOT a stored status — computed live from Status + ExpiresAt + StartsAt
-- (see TenantLicense.IsCurrentlyValid in code). TenantLicenseModules is a snapshot
-- taken at issue time (independently editable afterwards, not a live link to the plan).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantLicenses')
CREATE TABLE TenantLicenses (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    PlanId      UNIQUEIDENTIFIER NULL,
    MaxUsers    INT              NULL,
    LicenseKey  NVARCHAR(50)     NOT NULL,
    IssuedAt    DATETIME2        NOT NULL,
    StartsAt    DATETIME2        NOT NULL,
    ExpiresAt   DATETIME2        NULL,
    Status      NVARCHAR(20)     NOT NULL DEFAULT 'Active',
    IssuedBy    NVARCHAR(256)    NOT NULL,
    Notes       NVARCHAR(1000)   NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_TenantLicenses_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
    CONSTRAINT FK_TenantLicenses_LicensePlan FOREIGN KEY (PlanId) REFERENCES LicensePlans(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantLicenses_TenantId')
    CREATE UNIQUE INDEX IX_TenantLicenses_TenantId ON TenantLicenses (TenantId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantLicenseModules')
CREATE TABLE TenantLicenseModules (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantLicenseId UNIQUEIDENTIFIER NOT NULL,
    ModuleCode      NVARCHAR(20)     NOT NULL,
    CONSTRAINT FK_TenantLicenseModules_TenantLicense FOREIGN KEY (TenantLicenseId) REFERENCES TenantLicenses(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantLicenseModules_LicenseId_ModuleCode')
    CREATE UNIQUE INDEX IX_TenantLicenseModules_LicenseId_ModuleCode ON TenantLicenseModules (TenantLicenseId, ModuleCode);
GO

-- ============================================================
-- Seed: 14 ModuleDefinition rows, one per ERP.Shared.Authorization.ModuleCodes.All entry.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'SO')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000001', 'SO', 'Sales Orders', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'PO')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000002', 'PO', 'Purchase Orders', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'WH')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000003', 'WH', 'Warehouse', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'ACC')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000004', 'ACC', 'Accounting', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'CRM')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000005', 'CRM', 'CRM', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'HRM')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000006', 'HRM', 'HR Management', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'PRD')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000007', 'PRD', 'Product Catalog', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'PAY')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000008', 'PAY', 'Payments', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'SHP')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000009', 'SHP', 'Shipping', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'POS')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000010', 'POS', 'Point of Sale', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'PRM')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000011', 'PRM', 'Promotions', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'NTF')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000012', 'NTF', 'Notifications', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'RPT')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000013', 'RPT', 'Reporting', 1);
IF NOT EXISTS (SELECT 1 FROM ModuleDefinitions WHERE ModuleCode = 'MST')
    INSERT INTO ModuleDefinitions (Id, ModuleCode, ModuleName, IsActive) VALUES ('40000000-0000-0000-0000-000000000014', 'MST', 'Master Data', 1);
GO

-- ============================================================
-- Seed: 3 starter LicensePlan rows + their default module sets.
-- ============================================================
DECLARE @StarterPlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000001';
DECLARE @ProfessionalPlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000002';
DECLARE @EnterprisePlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM LicensePlans WHERE PlanCode = 'STARTER')
    INSERT INTO LicensePlans (Id, PlanCode, PlanName, Description, MaxUsers, IsActive)
    VALUES (@StarterPlanId, 'STARTER', 'Starter', 'Core sales, product, payment and warehouse modules for a small single-site business.', 10, 1);

IF NOT EXISTS (SELECT 1 FROM LicensePlans WHERE PlanCode = 'PROFESSIONAL')
    INSERT INTO LicensePlans (Id, PlanCode, PlanName, Description, MaxUsers, IsActive)
    VALUES (@ProfessionalPlanId, 'PROFESSIONAL', 'Professional', 'Adds CRM, shipping, POS, promotions and purchasing to the Starter set for a growing multi-channel business.', 50, 1);

IF NOT EXISTS (SELECT 1 FROM LicensePlans WHERE PlanCode = 'ENTERPRISE')
    INSERT INTO LicensePlans (Id, PlanCode, PlanName, Description, MaxUsers, IsActive)
    VALUES (@EnterprisePlanId, 'ENTERPRISE', 'Enterprise', 'All 14 licensable modules with unlimited users, for a full ERP deployment.', NULL, 1);
GO

DECLARE @StarterPlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000001';
DECLARE @ProfessionalPlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000002';
DECLARE @EnterprisePlanId UNIQUEIDENTIFIER = '41000000-0000-0000-0000-000000000003';

-- STARTER: SO, PRD, PAY, WH
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @StarterPlanId AND ModuleCode = 'SO')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000001', @StarterPlanId, 'SO');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @StarterPlanId AND ModuleCode = 'PRD')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000002', @StarterPlanId, 'PRD');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @StarterPlanId AND ModuleCode = 'PAY')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000003', @StarterPlanId, 'PAY');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @StarterPlanId AND ModuleCode = 'WH')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000004', @StarterPlanId, 'WH');

-- PROFESSIONAL: Starter set + CRM, SHP, POS, PRM, PO
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'SO')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000005', @ProfessionalPlanId, 'SO');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'PRD')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000006', @ProfessionalPlanId, 'PRD');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'PAY')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000007', @ProfessionalPlanId, 'PAY');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'WH')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000008', @ProfessionalPlanId, 'WH');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'CRM')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000009', @ProfessionalPlanId, 'CRM');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'SHP')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000010', @ProfessionalPlanId, 'SHP');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'POS')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000011', @ProfessionalPlanId, 'POS');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'PRM')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000012', @ProfessionalPlanId, 'PRM');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @ProfessionalPlanId AND ModuleCode = 'PO')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000013', @ProfessionalPlanId, 'PO');

-- ENTERPRISE: all 14 modules
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'SO')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000014', @EnterprisePlanId, 'SO');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'PO')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000015', @EnterprisePlanId, 'PO');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'WH')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000016', @EnterprisePlanId, 'WH');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'ACC')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000017', @EnterprisePlanId, 'ACC');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'CRM')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000018', @EnterprisePlanId, 'CRM');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'HRM')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000019', @EnterprisePlanId, 'HRM');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'PRD')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000020', @EnterprisePlanId, 'PRD');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'PAY')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000021', @EnterprisePlanId, 'PAY');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'SHP')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000022', @EnterprisePlanId, 'SHP');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'POS')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000023', @EnterprisePlanId, 'POS');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'PRM')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000024', @EnterprisePlanId, 'PRM');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'NTF')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000025', @EnterprisePlanId, 'NTF');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'RPT')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000026', @EnterprisePlanId, 'RPT');
IF NOT EXISTS (SELECT 1 FROM LicensePlanModules WHERE LicensePlanId = @EnterprisePlanId AND ModuleCode = 'MST')
    INSERT INTO LicensePlanModules (Id, LicensePlanId, ModuleCode) VALUES ('42000000-0000-0000-0000-000000000027', @EnterprisePlanId, 'MST');
GO

-- ============================================================
-- New permissions: license:modules:manage, license:plans:{view|manage},
-- license:tenant:{view|issue}. SuperAdmin gets all 5. Admin gets ONLY
-- license:tenant:view (can see their own tenant's license, read-only — never
-- issue/renew/suspend/revoke, which is commercially controlled and SuperAdmin-only
-- by design; TenantLicensesController additionally double-checks this at runtime
-- via a SystemManage override, not just RBAC, so this is defense in depth, not the
-- only gate).
-- ============================================================
DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';

DECLARE @PermLicenseModulesManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000062';
DECLARE @PermLicensePlansViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000063';
DECLARE @PermLicensePlansManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000064';
DECLARE @PermLicenseTenantViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000065';
DECLARE @PermLicenseTenantIssueId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000066';

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'license:modules:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermLicenseModulesManageId, 'license:modules:manage', 'Manage Licensable Module Catalog', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'license:plans:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermLicensePlansViewId, 'license:plans:view', 'View License Plans', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'license:plans:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermLicensePlansManageId, 'license:plans:manage', 'Manage License Plans', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'license:tenant:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermLicenseTenantViewId, 'license:tenant:view', 'View Tenant License', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'license:tenant:issue')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermLicenseTenantIssueId, 'license:tenant:issue', 'Issue/Renew/Suspend/Revoke Tenant License', 'Identity');

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermLicenseModulesManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermLicenseModulesManageId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermLicensePlansViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermLicensePlansViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermLicensePlansManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermLicensePlansManageId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermLicenseTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermLicenseTenantViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermLicenseTenantIssueId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermLicenseTenantIssueId);

IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermLicenseTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermLicenseTenantViewId);
GO
