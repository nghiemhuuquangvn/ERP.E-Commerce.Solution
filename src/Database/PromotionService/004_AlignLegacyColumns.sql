-- ============================================================
-- PromotionDB Migration 004 — legacy columns the entities do not write (ROADMAP P0-9)
--  * Promotions.Type / LoyaltyTransactions.Type: superseded by DiscountType / the entity's own columns;
--    kept (data preserved) and allowed to be NULL.
--  * LoyaltyAccounts.UserId: nvarchar while the entity uses a Guid (reads threw InvalidCast); converted.
-- Idempotent.
-- ============================================================

USE PromotionDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Promotions') AND name = 'Type' AND is_nullable = 0)
    ALTER TABLE Promotions ALTER COLUMN [Type] INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyTransactions') AND name = 'Type' AND is_nullable = 0)
    ALTER TABLE LoyaltyTransactions ALTER COLUMN [Type] INT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyAccounts') AND name = 'UserId' AND system_type_id = TYPE_ID('nvarchar'))
BEGIN
    IF EXISTS (SELECT 1 FROM LoyaltyAccounts WHERE TRY_CONVERT(UNIQUEIDENTIFIER, UserId) IS NULL)
        THROW 50026, 'LoyaltyAccounts.UserId holds a value that is not a GUID; fix the data before running Promotion 004.', 1;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoyaltyAccounts_UserId' AND object_id = OBJECT_ID('LoyaltyAccounts'))
        DROP INDEX IX_LoyaltyAccounts_UserId ON LoyaltyAccounts;
    ALTER TABLE LoyaltyAccounts ALTER COLUMN UserId UNIQUEIDENTIFIER NOT NULL;
    CREATE UNIQUE INDEX IX_LoyaltyAccounts_UserId ON LoyaltyAccounts (UserId, TenantId);
END
GO
