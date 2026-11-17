-- ============================================================
-- AuthDB Migration 038 - session policy (security review 2026-11, wave S2: SEC-M3 / SEC-M4).
-- docs/business/Identity/Sessions.md
--  * RefreshTokens.SessionStartedAt: the sign-in that started the session, carried through every rotation
--    (absolute limit); RefreshTokens.DeviceSessionId: the device session the token belongs to (revoke a device =
--    its tokens stop).
--  * SystemAuthSettings / TenantAuthSettings: back-office absolute limit (hours) and the customer pair (idle
--    minutes, absolute days). SessionTimeoutMinutes (back-office idle) already exists.
--  * Owner decision 2026-11-17: a back-office session ends after 15 minutes without activity. Rows still at the old
--    default 60 become 15 - ONCE, in the same step that adds the new columns, so a value chosen later is never reset.
-- Idempotent, append-only.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'SessionStartedAt')
    ALTER TABLE RefreshTokens ADD SessionStartedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'DeviceSessionId')
    ALTER TABLE RefreshTokens ADD DeviceSessionId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'IX_RefreshTokens_DeviceSessionId')
    CREATE INDEX IX_RefreshTokens_DeviceSessionId ON RefreshTokens (DeviceSessionId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'SessionAbsoluteHours')
BEGIN
    ALTER TABLE SystemAuthSettings ADD
        SessionAbsoluteHours INT NOT NULL CONSTRAINT DF_SystemAuthSettings_SessionAbsoluteHours DEFAULT 12,
        CustomerSessionTimeoutMinutes INT NOT NULL CONSTRAINT DF_SystemAuthSettings_CustomerSessionTimeoutMinutes DEFAULT 10080,
        CustomerSessionAbsoluteDays INT NOT NULL CONSTRAINT DF_SystemAuthSettings_CustomerSessionAbsoluteDays DEFAULT 30;
    EXEC('UPDATE SystemAuthSettings SET SessionTimeoutMinutes = 15 WHERE SessionTimeoutMinutes = 60');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'SessionAbsoluteHours')
BEGIN
    ALTER TABLE TenantAuthSettings ADD
        SessionAbsoluteHours INT NOT NULL CONSTRAINT DF_TenantAuthSettings_SessionAbsoluteHours DEFAULT 12,
        CustomerSessionTimeoutMinutes INT NOT NULL CONSTRAINT DF_TenantAuthSettings_CustomerSessionTimeoutMinutes DEFAULT 10080,
        CustomerSessionAbsoluteDays INT NOT NULL CONSTRAINT DF_TenantAuthSettings_CustomerSessionAbsoluteDays DEFAULT 30;
    EXEC('UPDATE TenantAuthSettings SET SessionTimeoutMinutes = 15 WHERE SessionTimeoutMinutes = 60');
END
GO

PRINT 'Migration 038 completed: session policy columns';
GO
