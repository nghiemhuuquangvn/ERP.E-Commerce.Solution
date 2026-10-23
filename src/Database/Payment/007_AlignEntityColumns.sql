-- ============================================================
-- PaymentDB Migration 007 — columns the EF model needs (ROADMAP P0-9, LiveSchemaDriftTests)
-- InstallmentPlans and SavedPaymentMethods derive from BaseEntity (TenantId, audit columns, IsDeleted,
-- RowVersion) but were created without them; Settlements has no TransactionCount. (Settlement.SettledAt is
-- mapped to the existing CompletedAt column in PaymentDbContext.)
-- Both tables hold no rows in a fresh database; any existing row goes to the SYSTEM tenant (they have no
-- parent that names a tenant). Idempotent.
-- ============================================================

USE PaymentDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('InstallmentPlans', 'TenantId') IS NULL
    ALTER TABLE InstallmentPlans ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_InstallmentPlans_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH('InstallmentPlans', 'Description') IS NULL ALTER TABLE InstallmentPlans ADD Description NVARCHAR(MAX) NULL;
IF COL_LENGTH('InstallmentPlans', 'CreatedBy') IS NULL ALTER TABLE InstallmentPlans ADD CreatedBy NVARCHAR(256) NULL;
IF COL_LENGTH('InstallmentPlans', 'UpdatedBy') IS NULL ALTER TABLE InstallmentPlans ADD UpdatedBy NVARCHAR(256) NULL;
IF COL_LENGTH('InstallmentPlans', 'IsDeleted') IS NULL ALTER TABLE InstallmentPlans ADD IsDeleted BIT NOT NULL CONSTRAINT DF_InstallmentPlans_IsDeleted DEFAULT 0;
IF COL_LENGTH('InstallmentPlans', 'RowVersion') IS NULL ALTER TABLE InstallmentPlans ADD RowVersion ROWVERSION NOT NULL;
GO

IF COL_LENGTH('SavedPaymentMethods', 'TenantId') IS NULL
    ALTER TABLE SavedPaymentMethods ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SavedPaymentMethods_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH('SavedPaymentMethods', 'Description') IS NULL ALTER TABLE SavedPaymentMethods ADD Description NVARCHAR(MAX) NULL;
IF COL_LENGTH('SavedPaymentMethods', 'CreatedBy') IS NULL ALTER TABLE SavedPaymentMethods ADD CreatedBy NVARCHAR(256) NULL;
IF COL_LENGTH('SavedPaymentMethods', 'UpdatedBy') IS NULL ALTER TABLE SavedPaymentMethods ADD UpdatedBy NVARCHAR(256) NULL;
IF COL_LENGTH('SavedPaymentMethods', 'IsDeleted') IS NULL ALTER TABLE SavedPaymentMethods ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SavedPaymentMethods_IsDeleted DEFAULT 0;
IF COL_LENGTH('SavedPaymentMethods', 'RowVersion') IS NULL ALTER TABLE SavedPaymentMethods ADD RowVersion ROWVERSION NOT NULL;
GO

IF COL_LENGTH('Settlements', 'TransactionCount') IS NULL
    ALTER TABLE Settlements ADD TransactionCount INT NOT NULL CONSTRAINT DF_Settlements_TransactionCount DEFAULT 0;
GO
