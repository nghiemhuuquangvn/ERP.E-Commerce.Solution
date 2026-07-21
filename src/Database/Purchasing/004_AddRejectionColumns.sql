-- ============================================================
-- PurchasingDB Schema — PurchaseOrderService (Port 5003)
-- Migration 004: Rejection tracking columns for the Reject-PO flow
-- (PurchaseOrder.RejectionReason, PurchaseOrderItem.RejectedQuantity) —
-- the domain entities already had these properties, but no migration ever
-- added the backing columns, so any query touching them failed with
-- "Invalid column name" (SQL error 207).
-- ============================================================

USE PurchasingDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PurchaseOrders' AND COLUMN_NAME = 'RejectionReason'
)
    ALTER TABLE PurchaseOrders ADD RejectionReason NVARCHAR(500) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PurchaseOrderItems' AND COLUMN_NAME = 'RejectedQuantity'
)
    ALTER TABLE PurchaseOrderItems ADD RejectedQuantity DECIMAL(18, 2) NOT NULL DEFAULT 0;
GO
