-- ============================================================
-- ShippingDB Migration 008 — Carrier integrations (2026-10-27, ROADMAP P1-11)
--
-- * TenantCarrierCredentials: one tenant's account at one carrier (GHTK / VTP / GHN / JNT); secrets are AES-encrypted by the service
--   (ISecretProtector), never plaintext.
-- * CarrierSelectionRules: "orders like this go with that carrier", tried in priority order; orders no rule matches go to the cheapest quote.
-- * CarrierWebhookLogs: carrier notifications already applied (unique), so a retry changes nothing.
-- * Shipments: COD amount, declared value, carrier fee / order code, why this carrier, booking error, last carrier sync, review note.
-- * SystemShippingSettings: platform switches per carrier. TenantShippingSettings: the shop's pickup address and default item weight.
-- Idempotent.
-- ============================================================

USE ShippingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantCarrierCredentials')
BEGIN
    CREATE TABLE TenantCarrierCredentials
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        CarrierCode         NVARCHAR(20) NOT NULL,
        Enabled             BIT NOT NULL DEFAULT 0,
        Sandbox             BIT NOT NULL DEFAULT 1,
        PublicId            NVARCHAR(200) NULL,
        Username            NVARCHAR(200) NULL,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TenantCarrierCredentials_Tenant_Carrier' AND object_id = OBJECT_ID('TenantCarrierCredentials'))
    CREATE UNIQUE INDEX UX_TenantCarrierCredentials_Tenant_Carrier ON TenantCarrierCredentials (TenantId, CarrierCode);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CarrierSelectionRules')
BEGIN
    CREATE TABLE CarrierSelectionRules
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        Name                NVARCHAR(200) NOT NULL,
        Priority            INT NOT NULL DEFAULT 100,
        IsActive            BIT NOT NULL DEFAULT 1,
        MatchProvinces      NVARCHAR(1000) NULL,
        MinWeightGrams      INT NULL,
        MaxWeightGrams      INT NULL,
        MinOrderValue       DECIMAL(18,2) NULL,
        MaxOrderValue       DECIMAL(18,2) NULL,
        CodOnly             BIT NULL,
        CarrierCode         NVARCHAR(20) NOT NULL,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CarrierSelectionRules_Tenant_Priority' AND object_id = OBJECT_ID('CarrierSelectionRules'))
    CREATE INDEX IX_CarrierSelectionRules_Tenant_Priority ON CarrierSelectionRules (TenantId, Priority);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CarrierWebhookLogs')
BEGIN
    CREATE TABLE CarrierWebhookLogs
    (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        CarrierCode         NVARCHAR(20) NOT NULL,
        TrackingCode        NVARCHAR(100) NOT NULL,
        CarrierStatus       NVARCHAR(50) NOT NULL,
        ActionTime          DATETIME2 NOT NULL,
        ReceivedAt          DATETIME2 NOT NULL,
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CarrierWebhookLogs_Event' AND object_id = OBJECT_ID('CarrierWebhookLogs'))
    CREATE UNIQUE INDEX UX_CarrierWebhookLogs_Event ON CarrierWebhookLogs (TenantId, CarrierCode, TrackingCode, CarrierStatus, ActionTime);
GO

IF COL_LENGTH('Shipments', 'CodAmount') IS NULL
    ALTER TABLE Shipments ADD CodAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Shipments_CodAmount DEFAULT 0 WITH VALUES;
IF COL_LENGTH('Shipments', 'DeclaredValue') IS NULL
    ALTER TABLE Shipments ADD DeclaredValue DECIMAL(18,2) NOT NULL CONSTRAINT DF_Shipments_DeclaredValue DEFAULT 0 WITH VALUES;
IF COL_LENGTH('Shipments', 'CarrierFee') IS NULL
    ALTER TABLE Shipments ADD CarrierFee DECIMAL(18,2) NULL;
IF COL_LENGTH('Shipments', 'CarrierOrderCode') IS NULL
    ALTER TABLE Shipments ADD CarrierOrderCode NVARCHAR(100) NULL;
IF COL_LENGTH('Shipments', 'SelectionNote') IS NULL
    ALTER TABLE Shipments ADD SelectionNote NVARCHAR(1000) NULL;
IF COL_LENGTH('Shipments', 'BookingError') IS NULL
    ALTER TABLE Shipments ADD BookingError NVARCHAR(1000) NULL;
IF COL_LENGTH('Shipments', 'LastCarrierSyncAt') IS NULL
    ALTER TABLE Shipments ADD LastCarrierSyncAt DATETIME2 NULL;
IF COL_LENGTH('Shipments', 'ReviewNote') IS NULL
    ALTER TABLE Shipments ADD ReviewNote NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Shipments_TrackingNumber' AND object_id = OBJECT_ID('Shipments'))
    CREATE INDEX IX_Shipments_TrackingNumber ON Shipments (TrackingNumber);
GO

IF COL_LENGTH('SystemShippingSettings', 'GhtkAvailable') IS NULL
    ALTER TABLE SystemShippingSettings ADD GhtkAvailable BIT NOT NULL CONSTRAINT DF_SystemShippingSettings_Ghtk DEFAULT 1 WITH VALUES;
IF COL_LENGTH('SystemShippingSettings', 'VtpAvailable') IS NULL
    ALTER TABLE SystemShippingSettings ADD VtpAvailable BIT NOT NULL CONSTRAINT DF_SystemShippingSettings_Vtp DEFAULT 1 WITH VALUES;
IF COL_LENGTH('SystemShippingSettings', 'GhnAvailable') IS NULL
    ALTER TABLE SystemShippingSettings ADD GhnAvailable BIT NOT NULL CONSTRAINT DF_SystemShippingSettings_Ghn DEFAULT 1 WITH VALUES;
IF COL_LENGTH('SystemShippingSettings', 'JntAvailable') IS NULL
    ALTER TABLE SystemShippingSettings ADD JntAvailable BIT NOT NULL CONSTRAINT DF_SystemShippingSettings_Jnt DEFAULT 1 WITH VALUES;
GO

IF COL_LENGTH('TenantShippingSettings', 'SenderName') IS NULL
    ALTER TABLE TenantShippingSettings ADD SenderName NVARCHAR(200) NULL;
IF COL_LENGTH('TenantShippingSettings', 'SenderPhone') IS NULL
    ALTER TABLE TenantShippingSettings ADD SenderPhone NVARCHAR(50) NULL;
IF COL_LENGTH('TenantShippingSettings', 'SenderAddress') IS NULL
    ALTER TABLE TenantShippingSettings ADD SenderAddress NVARCHAR(300) NULL;
IF COL_LENGTH('TenantShippingSettings', 'SenderWard') IS NULL
    ALTER TABLE TenantShippingSettings ADD SenderWard NVARCHAR(100) NULL;
IF COL_LENGTH('TenantShippingSettings', 'SenderDistrict') IS NULL
    ALTER TABLE TenantShippingSettings ADD SenderDistrict NVARCHAR(100) NULL;
IF COL_LENGTH('TenantShippingSettings', 'SenderProvince') IS NULL
    ALTER TABLE TenantShippingSettings ADD SenderProvince NVARCHAR(100) NULL;
IF COL_LENGTH('TenantShippingSettings', 'DefaultItemWeightGrams') IS NULL
    ALTER TABLE TenantShippingSettings ADD DefaultItemWeightGrams INT NOT NULL CONSTRAINT DF_TenantShippingSettings_ItemWeight DEFAULT 500 WITH VALUES;
GO
