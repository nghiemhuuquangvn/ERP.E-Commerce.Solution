-- ============================================================
-- PaymentDB Migration 008 — Online payments (2026-10-27, ROADMAP P1-10)
--
-- * TenantGatewayCredentials: one tenant's merchant account at one gateway (VNPay / MoMo / ZaloPay); secrets are AES-encrypted by
--   the service (ISecretProtector), never plaintext.
-- * SystemPaymentSettings: platform switches MoMoAvailable / ZaloPayAvailable. TenantPaymentSettings: CodEnabled.
-- * Payments: Provider (what the customer chose: COD / VNPay / MoMo / ZaloPay), CustomerId, OrderNumber.
-- Idempotent.
-- ============================================================

USE PaymentDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantGatewayCredentials')
BEGIN
    CREATE TABLE TenantGatewayCredentials
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        Provider            NVARCHAR(20) NOT NULL,
        Enabled             BIT NOT NULL DEFAULT 0,
        Sandbox             BIT NOT NULL DEFAULT 1,
        PublicId            NVARCHAR(200) NULL,
        AccessKey           NVARCHAR(1000) NULL,
        Secret1Encrypted    NVARCHAR(1000) NULL,
        Secret2Encrypted    NVARCHAR(1000) NULL,
        Description         NVARCHAR(MAX) NULL,
        IsDeleted           BIT NOT NULL DEFAULT 0,
        CreatedBy           NVARCHAR(200) NULL,
        CreatedAt           DATETIME2 NOT NULL,
        UpdatedBy           NVARCHAR(200) NULL,
        UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantGatewayCredentials_Tenant_Provider' AND object_id = OBJECT_ID('TenantGatewayCredentials'))
    CREATE UNIQUE INDEX UX_TenantGatewayCredentials_Tenant_Provider ON TenantGatewayCredentials (TenantId, Provider);
GO

IF COL_LENGTH('SystemPaymentSettings', 'MoMoAvailable') IS NULL
    ALTER TABLE SystemPaymentSettings ADD MoMoAvailable BIT NOT NULL CONSTRAINT DF_SystemPaymentSettings_MoMo DEFAULT 1 WITH VALUES;
IF COL_LENGTH('SystemPaymentSettings', 'ZaloPayAvailable') IS NULL
    ALTER TABLE SystemPaymentSettings ADD ZaloPayAvailable BIT NOT NULL CONSTRAINT DF_SystemPaymentSettings_ZaloPay DEFAULT 1 WITH VALUES;
IF COL_LENGTH('TenantPaymentSettings', 'CodEnabled') IS NULL
    ALTER TABLE TenantPaymentSettings ADD CodEnabled BIT NOT NULL CONSTRAINT DF_TenantPaymentSettings_Cod DEFAULT 1 WITH VALUES;
GO

IF COL_LENGTH('Payments', 'Provider') IS NULL
    ALTER TABLE Payments ADD Provider NVARCHAR(20) NULL;
IF COL_LENGTH('Payments', 'CustomerId') IS NULL
    ALTER TABLE Payments ADD CustomerId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('Payments', 'OrderNumber') IS NULL
    ALTER TABLE Payments ADD OrderNumber NVARCHAR(50) NULL;
GO
