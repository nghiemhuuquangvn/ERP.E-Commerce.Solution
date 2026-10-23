-- ============================================================
-- PosDB Migration 006 — TransactionItems.TenantId
-- TransactionItem derives from BaseEntity (tenant-owned), but 001 created TransactionItems
-- without a TenantId column and 005 only covered the cache tables, so every read/write of
-- a sale's lines failed with "Invalid column name 'TenantId'" (adding an item to a sale
-- returned HTTP 500). Existing rows take the tenant of their parent transaction.
-- Idempotent.
-- ============================================================

USE PosDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('TransactionItems', 'TenantId') IS NULL
    ALTER TABLE TransactionItems ADD TenantId UNIQUEIDENTIFIER NULL;
GO

UPDATE i
SET i.TenantId = t.TenantId
FROM TransactionItems i
JOIN Transactions t ON t.Id = i.TransactionId
WHERE i.TenantId IS NULL;
GO

-- Orphans (no parent row) belong to the SYSTEM tenant, like every other empty-tenant row.
UPDATE TransactionItems SET TenantId = '00000000-0000-0000-0000-000000000001' WHERE TenantId IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TransactionItems') AND name = 'TenantId' AND is_nullable = 1)
    ALTER TABLE TransactionItems ALTER COLUMN TenantId UNIQUEIDENTIFIER NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TransactionItems_Tenant_Transaction' AND object_id = OBJECT_ID('TransactionItems'))
    CREATE INDEX IX_TransactionItems_Tenant_Transaction ON TransactionItems (TenantId, TransactionId);
GO
