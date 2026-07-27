-- ============================================================
-- PurchasingDB Schema Update — PurchaseOrderService (Port 5003, Code: PO)
-- Migration 006 — Module 8 (Inventory) reconciliation: suggestion-only
-- PurchaseRequests, auto-created from WareHouseService's StockAlertEvent
-- (first-ever inbound event consumer in this service).
--
-- Unlike every other table in this database, PurchaseRequests has NO TenantId
-- and no composite (Id, TenantId) key — the upstream signal (WareHouseService's
-- Inventory aggregate) carries no tenant concept, so there is nothing correct to
-- backfill a TenantId from. Do not add one later without a real per-tenant stock
-- signal to justify it.
-- ============================================================

USE PurchasingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseRequests')
CREATE TABLE PurchaseRequests (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProductId           UNIQUEIDENTIFIER NOT NULL,
    ProductName         NVARCHAR(255)    NOT NULL,
    CurrentQuantity     INT              NOT NULL,
    ReorderPoint        INT              NOT NULL,
    SuggestedQuantity   INT              NOT NULL,
    Status              INT              NOT NULL DEFAULT 0, -- 0=Suggested, 1=Dismissed, 2=Converted
    CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ResolvedAt          DATETIME2        NULL
);
CREATE INDEX IX_PurchaseRequests_ProductId ON PurchaseRequests (ProductId);
CREATE INDEX IX_PurchaseRequests_Status ON PurchaseRequests (Status);
GO
