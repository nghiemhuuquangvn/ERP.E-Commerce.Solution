-- ============================================================
-- PurchasingDB Schema Update — PurchaseOrderService (Port 5003, Code: PO)
-- Migration 003 (2026-07-03): ScheduledReceiptDate column + SupplierRatings table.
-- Note: PurchaseOrderItems.ReceivedQuantity and the GoodsReceipts table already
-- existed in 001_CreatePurchasingDB.sql — they had no corresponding C# entity/usage
-- until this same pass (see docs/business/ERP-Business-Analysis.md section 3.4),
-- so no new column/table is needed for those two.
-- ============================================================

USE PurchasingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'ScheduledReceiptDate')
    ALTER TABLE PurchaseOrders ADD ScheduledReceiptDate DATETIME2 NULL;
GO

-- ============================================================
-- Supplier Ratings (evaluation/rating — previously entirely absent)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupplierRatings')
CREATE TABLE SupplierRatings (
    Id          UNIQUEIDENTIFIER NOT NULL,
    TenantId    UNIQUEIDENTIFIER NOT NULL,
    SupplierId  UNIQUEIDENTIFIER NOT NULL,
    Rating      INT              NOT NULL,
    Comment     NVARCHAR(2000)   NULL,
    RatedBy     NVARCHAR(256)    NOT NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY (Id, TenantId),
    CONSTRAINT FK_SupplierRatings_Supplier FOREIGN KEY (SupplierId, TenantId) REFERENCES Suppliers(Id, TenantId) ON DELETE CASCADE,
    CONSTRAINT CK_SupplierRatings_Rating CHECK (Rating BETWEEN 1 AND 5)
);
CREATE INDEX IX_SupplierRatings_SupplierId ON SupplierRatings (SupplierId, TenantId);
GO
