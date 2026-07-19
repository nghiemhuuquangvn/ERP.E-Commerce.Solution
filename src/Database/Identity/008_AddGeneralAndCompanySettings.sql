-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 008: General Settings (System/Tenant) + Company Settings (Tenant-only)
-- ============================================================

USE AuthDB;
GO

-- Required for INSERTs into tables with filtered indexes; some sqlcmd
-- sessions don't inherit ON even when the DB-level default is ON.
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- SystemGeneralSettings (single well-known-GUID row)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemGeneralSettings')
CREATE TABLE SystemGeneralSettings (
    Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Description             NVARCHAR(MAX)    NULL,
    MaintenanceModeEnabled  BIT              NOT NULL DEFAULT 0,
    DefaultLanguageCode     NVARCHAR(10)     NOT NULL DEFAULT 'en',
    CreatedBy               NVARCHAR(256)    NULL,
    CreatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy               NVARCHAR(256)    NULL,
    UpdatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted               BIT              NOT NULL DEFAULT 0
);
GO

DECLARE @SystemGeneralSettingsId UNIQUEIDENTIFIER = 'C0000000-0000-0000-0000-000000000001';
IF NOT EXISTS (SELECT 1 FROM SystemGeneralSettings WHERE Id = @SystemGeneralSettingsId)
    INSERT INTO SystemGeneralSettings (Id, MaintenanceModeEnabled, DefaultLanguageCode)
    VALUES (@SystemGeneralSettingsId, 0, 'en');
GO

-- ============================================================
-- TenantGeneralSettings (one row per tenant; backfilled below)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantGeneralSettings')
CREATE TABLE TenantGeneralSettings (
    Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    Description             NVARCHAR(MAX)    NULL,
    SiteName                NVARCHAR(200)    NOT NULL DEFAULT '',
    DefaultLanguageCode     NVARCHAR(10)     NOT NULL DEFAULT 'en',
    TimeZone                NVARCHAR(100)    NOT NULL DEFAULT 'UTC',
    DateFormat              NVARCHAR(20)     NOT NULL DEFAULT 'yyyy-MM-dd',
    MaintenanceModeEnabled  BIT              NOT NULL DEFAULT 0,
    CreatedBy               NVARCHAR(256)    NULL,
    CreatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy               NVARCHAR(256)    NULL,
    UpdatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted               BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantGeneralSettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
CREATE UNIQUE INDEX IX_TenantGeneralSettings_TenantId ON TenantGeneralSettings (TenantId);
GO

INSERT INTO TenantGeneralSettings (Id, TenantId, SiteName, DefaultLanguageCode, TimeZone, DateFormat, MaintenanceModeEnabled)
SELECT NEWID(), t.Id, '', 'en', 'UTC', 'yyyy-MM-dd', 0
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantGeneralSettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- TenantCompanySettings (one row per tenant; backfilled below). No System tier.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantCompanySettings')
CREATE TABLE TenantCompanySettings (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    CompanyName     NVARCHAR(200)    NOT NULL DEFAULT '',
    LegalName       NVARCHAR(200)    NULL,
    TaxCode         NVARCHAR(50)     NULL,
    Address         NVARCHAR(500)    NULL,
    Phone           NVARCHAR(50)     NULL,
    Email           NVARCHAR(200)    NULL,
    LogoUrl         NVARCHAR(500)    NULL,
    Website         NVARCHAR(200)    NULL,
    CreatedBy       NVARCHAR(256)    NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       NVARCHAR(256)    NULL,
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TenantCompanySettings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
CREATE UNIQUE INDEX IX_TenantCompanySettings_TenantId ON TenantCompanySettings (TenantId);
GO

INSERT INTO TenantCompanySettings (Id, TenantId, CompanyName)
SELECT NEWID(), t.Id, ''
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM TenantCompanySettings s WHERE s.TenantId = t.Id);
GO

-- ============================================================
-- New permissions: settings:general:{system|tenant}:{view|manage}, settings:company:tenant:{view|manage}
-- SuperAdmin gets all 6; Admin gets the tenant-scoped 4.
-- ============================================================
DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';

DECLARE @PermGeneralSystemViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000020';
DECLARE @PermGeneralSystemManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000021';
DECLARE @PermGeneralTenantViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000022';
DECLARE @PermGeneralTenantManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000023';
DECLARE @PermCompanyTenantViewId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000024';
DECLARE @PermCompanyTenantManageId UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000025';

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:general:system:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermGeneralSystemViewId, 'settings:general:system:view', 'View System General Settings', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:general:system:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermGeneralSystemManageId, 'settings:general:system:manage', 'Manage System General Settings', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:general:tenant:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermGeneralTenantViewId, 'settings:general:tenant:view', 'View Tenant General Settings', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:general:tenant:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermGeneralTenantManageId, 'settings:general:tenant:manage', 'Manage Tenant General Settings', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:company:tenant:view')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermCompanyTenantViewId, 'settings:company:tenant:view', 'View Tenant Company Settings', 'Identity');

IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = 'settings:company:tenant:manage')
    INSERT INTO Permissions (Id, PermissionCode, PermissionName, Module)
    VALUES (@PermCompanyTenantManageId, 'settings:company:tenant:manage', 'Manage Tenant Company Settings', 'Identity');

-- Grant all 6 to SuperAdmin
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermGeneralSystemViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermGeneralSystemViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermGeneralSystemManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermGeneralSystemManageId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermGeneralTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermGeneralTenantViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermGeneralTenantManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermGeneralTenantManageId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermCompanyTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermCompanyTenantViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = @PermCompanyTenantManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminRoleId, @PermCompanyTenantManageId);

-- Grant only the tenant-scoped 4 to Admin
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermGeneralTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermGeneralTenantViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermGeneralTenantManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermGeneralTenantManageId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermCompanyTenantViewId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermCompanyTenantViewId);
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = @PermCompanyTenantManageId)
    INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (@AdminRoleId, @PermCompanyTenantManageId);
GO
