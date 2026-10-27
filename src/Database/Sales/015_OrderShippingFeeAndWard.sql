-- ============================================================
-- SalesDB Migration 015 - the delivery fee and the ward on the order (2026-10-27, ROADMAP P1-11b)
--
-- SalesOrders.ShippingFee: what the customer paid for delivery (already inside TotalAmount); 0 on orders made before this.
-- SalesOrders.ShippingAddress_Ward: ward (phuong / xa) of the delivery address; '' on orders made before this.
-- Idempotent.
-- ============================================================

USE SalesDB;
GO

IF COL_LENGTH('SalesOrders', 'ShippingFee') IS NULL
    ALTER TABLE SalesOrders ADD ShippingFee DECIMAL(18,2) NOT NULL CONSTRAINT DF_SalesOrders_ShippingFee DEFAULT 0;
GO

IF COL_LENGTH('SalesOrders', 'ShippingAddress_Ward') IS NULL
    ALTER TABLE SalesOrders ADD ShippingAddress_Ward NVARCHAR(100) NOT NULL CONSTRAINT DF_SalesOrders_ShippingAddress_Ward DEFAULT '';
GO
