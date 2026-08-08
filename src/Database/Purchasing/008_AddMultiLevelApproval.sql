-- ============================================================
-- PurchasingDB Migration 008 — Multi-level approval columns
--
-- Supports PurchaseOrder's new two-step approval chain (Manager -> Finance
-- for orders at/above PurchaseOrder.FinanceApprovalThreshold). See
-- ERP.PO.Domain.Entities.PurchaseOrder.Approve()/ApproveFinance().
-- ============================================================

USE PurchasingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'ManagerApprovedBy')
    ALTER TABLE PurchaseOrders ADD ManagerApprovedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'ManagerApprovedAt')
    ALTER TABLE PurchaseOrders ADD ManagerApprovedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'FinanceApprovedBy')
    ALTER TABLE PurchaseOrders ADD FinanceApprovedBy NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'FinanceApprovedAt')
    ALTER TABLE PurchaseOrders ADD FinanceApprovedAt DATETIME2 NULL;
GO
