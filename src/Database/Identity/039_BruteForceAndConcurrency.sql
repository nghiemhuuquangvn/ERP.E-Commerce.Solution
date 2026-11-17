-- ============================================================
-- AuthDB Migration 039 - brute-force protection and refresh concurrency (security review 2026-11, wave S3:
-- SEC-M1 / SEC-M2 / SEC-M5). docs/business/Identity/BruteForceProtection.md
--  * RefreshTokens.RevokedReason already exists since 001 (NVARCHAR(200), unused until now): it now holds 'Rotated'
--    (a successor exists) or 'Revoked'. Only a token ROTATED seconds ago may be presented again without counting as
--    theft (parallel refresh of two tabs, BR-BF-09). The guarded ADD below never runs on a database built from 001.
--  * LoginAttempts by account: the per-device (BR-BF-01) and account-wide (BR-BF-02) counts read by UserId.
--  * TwoFactorChallenges by creation time: at most N codes per account per hour (BR-BF-04).
--  * SystemAuthSettings / TenantAuthSettings: account lock threshold (20) and window (60 min), owner decision 2026-11-17.
-- Idempotent, append-only.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RefreshTokens') AND name = 'RevokedReason')
    ALTER TABLE RefreshTokens ADD RevokedReason NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('LoginAttempts') AND name = 'IX_LoginAttempts_UserId')
    CREATE INDEX IX_LoginAttempts_UserId ON LoginAttempts (UserId, AttemptedAt DESC) INCLUDE (IpAddress, IsSuccessful) WHERE UserId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('TwoFactorChallenges') AND name = 'IX_TwoFactorChallenges_UserId_CreatedAt')
    CREATE INDEX IX_TwoFactorChallenges_UserId_CreatedAt ON TwoFactorChallenges (UserId, CreatedAt DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SystemAuthSettings') AND name = 'AccountLockThreshold')
    ALTER TABLE SystemAuthSettings ADD
        AccountLockThreshold INT NOT NULL CONSTRAINT DF_SystemAuthSettings_AccountLockThreshold DEFAULT 20,
        AccountLockWindowMinutes INT NOT NULL CONSTRAINT DF_SystemAuthSettings_AccountLockWindowMinutes DEFAULT 60;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TenantAuthSettings') AND name = 'AccountLockThreshold')
    ALTER TABLE TenantAuthSettings ADD
        AccountLockThreshold INT NOT NULL CONSTRAINT DF_TenantAuthSettings_AccountLockThreshold DEFAULT 20,
        AccountLockWindowMinutes INT NOT NULL CONSTRAINT DF_TenantAuthSettings_AccountLockWindowMinutes DEFAULT 60;
GO

PRINT 'Migration 039 completed: brute-force protection and refresh concurrency';
GO
