-- ============================================================
-- PurchasingDB Schema Update — PurchaseOrderService (Port 5003, Code: PO)
-- Migration 005 (2026-07-23): SupplierItems table + Suppliers.PaymentTermId.
-- Note: Suppliers.TaxCode, PaymentTermDays, CurrencyCode already existed in
-- 001_CreatePurchasingDB.sql — they had no corresponding C# entity property until
-- this same pass, so no new column is needed for those three.
-- ============================================================

USE PurchasingDB;
GO

-- ============================================================
-- Suppliers.PaymentTermId — cross-service reference to SharedMasterDataService's
-- PaymentTerm by Id only (no DB FK, per CLAUDE.md database ownership rules).
-- Null means PaymentTermDays applies as a plain net-days fallback.
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Suppliers' AND COLUMN_NAME = 'PaymentTermId'
)
    ALTER TABLE Suppliers ADD PaymentTermId UNIQUEIDENTIFIER NULL;
GO

-- ============================================================
-- Supplier Items — what a Supplier can supply, at what reference price/lead time.
-- ProductId/VariantId reference ProductService's Product/ProductVariant by Id only
-- (cross-service, no DB FK).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupplierItems')
CREATE TABLE SupplierItems (
    Id                  UNIQUEIDENTIFIER NOT NULL,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    SupplierId          UNIQUEIDENTIFIER NOT NULL,
    ProductId           UNIQUEIDENTIFIER NOT NULL,
    VariantId           UNIQUEIDENTIFIER NULL,
    SupplierItemCode    NVARCHAR(50)     NULL,
    LastPurchasePrice   DECIMAL(18, 2)   NOT NULL DEFAULT 0,
    LeadTimeDays        INT              NOT NULL DEFAULT 0,
    CreatedBy           NVARCHAR(256)    NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    PRIMARY KEY (Id, TenantId),
    CONSTRAINT FK_SupplierItems_Supplier FOREIGN KEY (SupplierId, TenantId) REFERENCES Suppliers(Id, TenantId) ON DELETE CASCADE
);
CREATE INDEX IX_SupplierItems_SupplierId ON SupplierItems (SupplierId, TenantId);
CREATE INDEX IX_SupplierItems_ProductId  ON SupplierItems (ProductId, TenantId);
GO
