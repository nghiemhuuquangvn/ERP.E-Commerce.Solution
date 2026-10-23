-- ============================================================
-- AuthDB Schema Repair — Migration 025
-- EmailConfirmToken and PasswordResetToken derive from BaseEntity (UpdatedAt, IsDeleted) but
-- 001 created their tables without those columns and 005 only added CreatedBy/UpdatedBy/
-- Description. Every insert (register, tenant-signup, forgot-password, resend confirmation)
-- failed with "Invalid column name 'IsDeleted' / 'UpdatedAt'". Idempotent.
-- ============================================================

USE AuthDB;
GO

IF COL_LENGTH('EmailConfirmTokens', 'UpdatedAt') IS NULL
    ALTER TABLE EmailConfirmTokens ADD UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_EmailConfirmTokens_UpdatedAt DEFAULT GETUTCDATE();
GO
IF COL_LENGTH('EmailConfirmTokens', 'IsDeleted') IS NULL
    ALTER TABLE EmailConfirmTokens ADD IsDeleted BIT NOT NULL CONSTRAINT DF_EmailConfirmTokens_IsDeleted DEFAULT 0;
GO

IF COL_LENGTH('PasswordResetTokens', 'UpdatedAt') IS NULL
    ALTER TABLE PasswordResetTokens ADD UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_PasswordResetTokens_UpdatedAt DEFAULT GETUTCDATE();
GO
IF COL_LENGTH('PasswordResetTokens', 'IsDeleted') IS NULL
    ALTER TABLE PasswordResetTokens ADD IsDeleted BIT NOT NULL CONSTRAINT DF_PasswordResetTokens_IsDeleted DEFAULT 0;
GO
