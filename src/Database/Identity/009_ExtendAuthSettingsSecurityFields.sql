-- ============================================================
-- AuthDB Schema — IdentityService (Port 5001)
-- Migration 009: Extend SystemAuthSettings/TenantAuthSettings with User & Security
-- Settings fields (password policy, lockout, session timeout). Storage+CRUD only —
-- PasswordPolicyService/User.RecordLoginFail() still use hardcoded/appsettings
-- values; wiring them to read these columns is a follow-up.
-- ============================================================

USE AuthDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'MinPasswordLength')
    ALTER TABLE SystemAuthSettings ADD MinPasswordLength INT NOT NULL DEFAULT 8;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'RequireUppercase')
    ALTER TABLE SystemAuthSettings ADD RequireUppercase BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'RequireLowercase')
    ALTER TABLE SystemAuthSettings ADD RequireLowercase BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'RequireDigit')
    ALTER TABLE SystemAuthSettings ADD RequireDigit BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'RequireSymbol')
    ALTER TABLE SystemAuthSettings ADD RequireSymbol BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'MaxLoginAttempts')
    ALTER TABLE SystemAuthSettings ADD MaxLoginAttempts INT NOT NULL DEFAULT 5;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'LockoutDurationMinutes')
    ALTER TABLE SystemAuthSettings ADD LockoutDurationMinutes INT NOT NULL DEFAULT 15;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'SessionTimeoutMinutes')
    ALTER TABLE SystemAuthSettings ADD SessionTimeoutMinutes INT NOT NULL DEFAULT 60;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'MinPasswordLength')
    ALTER TABLE TenantAuthSettings ADD MinPasswordLength INT NOT NULL DEFAULT 8;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'RequireUppercase')
    ALTER TABLE TenantAuthSettings ADD RequireUppercase BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'RequireLowercase')
    ALTER TABLE TenantAuthSettings ADD RequireLowercase BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'RequireDigit')
    ALTER TABLE TenantAuthSettings ADD RequireDigit BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'RequireSymbol')
    ALTER TABLE TenantAuthSettings ADD RequireSymbol BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'MaxLoginAttempts')
    ALTER TABLE TenantAuthSettings ADD MaxLoginAttempts INT NOT NULL DEFAULT 5;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'LockoutDurationMinutes')
    ALTER TABLE TenantAuthSettings ADD LockoutDurationMinutes INT NOT NULL DEFAULT 15;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'SessionTimeoutMinutes')
    ALTER TABLE TenantAuthSettings ADD SessionTimeoutMinutes INT NOT NULL DEFAULT 60;
GO
