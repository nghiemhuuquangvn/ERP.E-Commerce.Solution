-- ============================================================
-- Migration 005: Add missing BaseEntity audit columns
-- Adds CreatedBy, UpdatedBy, Description to tables that
-- inherit from BaseEntity but were missing these columns
-- ============================================================

USE AuthDB;
GO

-- Helper macro: add column only if it doesn't exist
-- Table: RefreshTokens
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'CreatedBy')
    ALTER TABLE RefreshTokens ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'UpdatedBy')
    ALTER TABLE RefreshTokens ADD UpdatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'Description')
    ALTER TABLE RefreshTokens ADD Description NVARCHAR(500) NULL;
GO

-- Table: DeviceSessions
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DeviceSessions') AND name = 'CreatedBy')
    ALTER TABLE DeviceSessions ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DeviceSessions') AND name = 'UpdatedBy')
    ALTER TABLE DeviceSessions ADD UpdatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DeviceSessions') AND name = 'Description')
    ALTER TABLE DeviceSessions ADD Description NVARCHAR(500) NULL;
GO

-- Table: SecurityAuditLogs
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SecurityAuditLogs') AND name = 'CreatedBy')
    ALTER TABLE SecurityAuditLogs ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SecurityAuditLogs') AND name = 'UpdatedBy')
    ALTER TABLE SecurityAuditLogs ADD UpdatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SecurityAuditLogs') AND name = 'Description')
    ALTER TABLE SecurityAuditLogs ADD Description NVARCHAR(500) NULL;
GO

-- Table: LoginAttempts (may exist as SecurityAuditLogs or separate)
IF OBJECT_ID('LoginAttempts') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LoginAttempts') AND name = 'CreatedBy')
        ALTER TABLE LoginAttempts ADD CreatedBy NVARCHAR(256) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LoginAttempts') AND name = 'UpdatedBy')
        ALTER TABLE LoginAttempts ADD UpdatedBy NVARCHAR(256) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LoginAttempts') AND name = 'Description')
        ALTER TABLE LoginAttempts ADD Description NVARCHAR(500) NULL;
END
GO

-- Table: EmailConfirmTokens
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmailConfirmTokens') AND name = 'CreatedBy')
    ALTER TABLE EmailConfirmTokens ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmailConfirmTokens') AND name = 'UpdatedBy')
    ALTER TABLE EmailConfirmTokens ADD UpdatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EmailConfirmTokens') AND name = 'Description')
    ALTER TABLE EmailConfirmTokens ADD Description NVARCHAR(500) NULL;
GO

-- Table: PasswordResetTokens
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PasswordResetTokens') AND name = 'CreatedBy')
    ALTER TABLE PasswordResetTokens ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PasswordResetTokens') AND name = 'UpdatedBy')
    ALTER TABLE PasswordResetTokens ADD UpdatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PasswordResetTokens') AND name = 'Description')
    ALTER TABLE PasswordResetTokens ADD Description NVARCHAR(500) NULL;
GO

-- Table: ExternalLogins
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ExternalLogins') AND name = 'CreatedBy')
    ALTER TABLE ExternalLogins ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ExternalLogins') AND name = 'UpdatedBy')
    ALTER TABLE ExternalLogins ADD UpdatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ExternalLogins') AND name = 'Description')
    ALTER TABLE ExternalLogins ADD Description NVARCHAR(500) NULL;
GO

-- Table: TwoFactorSettings (if it extends BaseEntity)
IF OBJECT_ID('TwoFactorSettings') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TwoFactorSettings') AND name = 'CreatedBy')
        ALTER TABLE TwoFactorSettings ADD CreatedBy NVARCHAR(256) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TwoFactorSettings') AND name = 'UpdatedBy')
        ALTER TABLE TwoFactorSettings ADD UpdatedBy NVARCHAR(256) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TwoFactorSettings') AND name = 'Description')
        ALTER TABLE TwoFactorSettings ADD Description NVARCHAR(500) NULL;
END
GO

-- Table: ApiKeys
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ApiKeys') AND name = 'UpdatedBy')
    ALTER TABLE ApiKeys ADD UpdatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ApiKeys') AND name = 'Description')
    ALTER TABLE ApiKeys ADD Description NVARCHAR(500) NULL;
GO

PRINT 'Migration 005 complete: BaseEntity audit columns added to all affected tables.';
GO
