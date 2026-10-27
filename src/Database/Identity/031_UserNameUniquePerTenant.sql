-- Risk register R19: Users.UserName was unique across ALL tenants (IX_Users_UserName). Registration used the person's display name as the
-- user name, so two customers called "Shop Walker" - or the same e-mail in two shops, now that the account name is the e-mail - could not both
-- register (HTTP 500 on the second). The account name only has to be unique inside its tenant (e-mail already is, migration 014).
-- Idempotent.
USE AuthDB;
SET QUOTED_IDENTIFIER ON;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_UserName' AND object_id = OBJECT_ID('dbo.Users'))
    DROP INDEX IX_Users_UserName ON dbo.Users;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_TenantId_UserName' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE UNIQUE INDEX IX_Users_TenantId_UserName ON dbo.Users (TenantId, UserName) WHERE IsDeleted = 0 AND UserName IS NOT NULL;
GO
