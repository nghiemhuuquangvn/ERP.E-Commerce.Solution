-- Add Customer, Staff, and User roles to the SYSTEM tenant
-- Required for storefront customer registration (UserType.Customer → role 'CUSTOMER')

-- Every other migration in this folder starts with this; this one didn't, so
-- sqlcmd (a fresh connection per file, whose login default database is "master",
-- not AuthDB) ran it against the wrong database — "Invalid object name 'Tenants'".
-- Confirmed by running the full migration set against a genuinely fresh database
-- (CommonServiceRoadmap.md §7 Part B export verification).
USE AuthDB;
GO

DECLARE @SystemTenantId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Tenants WHERE TenantCode = 'SYSTEM');

IF @SystemTenantId IS NULL
BEGIN
    RAISERROR('SYSTEM tenant not found — run 001_CreateAuthDB.sql first', 16, 1);
    RETURN;
END

-- Customer role
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'CUSTOMER' AND TenantId = @SystemTenantId)
    INSERT INTO Roles (Id, TenantId, RoleCode, RoleName, IsSystem, IsActive)
    VALUES (NEWID(), @SystemTenantId, 'CUSTOMER', 'Customer', 1, 1);

-- Staff role
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'STAFF' AND TenantId = @SystemTenantId)
    INSERT INTO Roles (Id, TenantId, RoleCode, RoleName, IsSystem, IsActive)
    VALUES (NEWID(), @SystemTenantId, 'STAFF', 'Staff', 1, 1);

-- User role (generic fallback)
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'USER' AND TenantId = @SystemTenantId)
    INSERT INTO Roles (Id, TenantId, RoleCode, RoleName, IsSystem, IsActive)
    VALUES (NEWID(), @SystemTenantId, 'USER', 'User', 1, 1);

GO
