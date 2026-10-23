-- ============================================================
-- PurchasingDB Migration 010 — SupplierItems BaseEntity columns (ROADMAP P0-9)
-- SupplierItem derives from BaseEntity (Description, RowVersion) but the table lacks both. (GoodsReceipts is
-- fixed in the model: PurchaseOrderId is mapped to the existing POId column.) Idempotent.
-- ============================================================

USE PurchasingDB;
GO

IF COL_LENGTH('SupplierItems', 'Description') IS NULL
    ALTER TABLE SupplierItems ADD Description NVARCHAR(MAX) NULL;
GO
IF COL_LENGTH('SupplierItems', 'RowVersion') IS NULL
    ALTER TABLE SupplierItems ADD RowVersion ROWVERSION NOT NULL;
GO
