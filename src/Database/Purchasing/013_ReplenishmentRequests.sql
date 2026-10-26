-- ============================================================
-- PurchasingDB Migration 013 — Replenishment (2026-10-26, ROADMAP P1-9)
--
-- PurchaseRequests: product code (the warehouse SKU), the figures the proposal was made on (available, on order, min / max /
-- safety), the preferred supplier with price and lead time, and the link to the draft purchase order or RFQ created from it.
-- SupplierItems: IsPreferred (one preferred supplier per product).
-- Idempotent.
-- ============================================================

USE PurchasingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('PurchaseRequests', 'ProductCode') IS NULL
    ALTER TABLE PurchaseRequests ADD ProductCode NVARCHAR(100) NOT NULL CONSTRAINT DF_PurchaseRequests_ProductCode DEFAULT '' WITH VALUES;
IF COL_LENGTH('PurchaseRequests', 'AvailableQuantity') IS NULL
    ALTER TABLE PurchaseRequests ADD AvailableQuantity INT NOT NULL CONSTRAINT DF_PurchaseRequests_Available DEFAULT 0 WITH VALUES;
IF COL_LENGTH('PurchaseRequests', 'OnOrderQuantity') IS NULL
    ALTER TABLE PurchaseRequests ADD OnOrderQuantity INT NOT NULL CONSTRAINT DF_PurchaseRequests_OnOrder DEFAULT 0 WITH VALUES;
IF COL_LENGTH('PurchaseRequests', 'MinStock') IS NULL
    ALTER TABLE PurchaseRequests ADD MinStock INT NOT NULL CONSTRAINT DF_PurchaseRequests_MinStock DEFAULT 0 WITH VALUES;
IF COL_LENGTH('PurchaseRequests', 'MaxStock') IS NULL
    ALTER TABLE PurchaseRequests ADD MaxStock INT NOT NULL CONSTRAINT DF_PurchaseRequests_MaxStock DEFAULT 0 WITH VALUES;
IF COL_LENGTH('PurchaseRequests', 'SafetyStock') IS NULL
    ALTER TABLE PurchaseRequests ADD SafetyStock INT NOT NULL CONSTRAINT DF_PurchaseRequests_SafetyStock DEFAULT 0 WITH VALUES;
IF COL_LENGTH('PurchaseRequests', 'PreferredSupplierId') IS NULL
    ALTER TABLE PurchaseRequests ADD PreferredSupplierId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('PurchaseRequests', 'UnitPrice') IS NULL
    ALTER TABLE PurchaseRequests ADD UnitPrice DECIMAL(18,2) NULL;
IF COL_LENGTH('PurchaseRequests', 'LeadTimeDays') IS NULL
    ALTER TABLE PurchaseRequests ADD LeadTimeDays INT NULL;
IF COL_LENGTH('PurchaseRequests', 'PurchaseOrderId') IS NULL
    ALTER TABLE PurchaseRequests ADD PurchaseOrderId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('PurchaseRequests', 'RfqId') IS NULL
    ALTER TABLE PurchaseRequests ADD RfqId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('PurchaseRequests', 'Note') IS NULL
    ALTER TABLE PurchaseRequests ADD Note NVARCHAR(500) NULL;
GO

IF COL_LENGTH('SupplierItems', 'IsPreferred') IS NULL
    ALTER TABLE SupplierItems ADD IsPreferred BIT NOT NULL CONSTRAINT DF_SupplierItems_IsPreferred DEFAULT 0 WITH VALUES;
GO

-- Rows that predate the policy: min = the reorder point, available = the quantity the alert carried.
UPDATE PurchaseRequests SET MinStock = ReorderPoint, AvailableQuantity = CurrentQuantity WHERE MinStock = 0 AND MaxStock = 0;
UPDATE PurchaseRequests SET ProductCode = LOWER(REPLACE(CONVERT(NVARCHAR(36), ProductId), '-', '')) WHERE ProductCode = '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseRequests_TenantId_ProductCode' AND object_id = OBJECT_ID('PurchaseRequests'))
    CREATE INDEX IX_PurchaseRequests_TenantId_ProductCode ON PurchaseRequests (TenantId, ProductCode);
GO
