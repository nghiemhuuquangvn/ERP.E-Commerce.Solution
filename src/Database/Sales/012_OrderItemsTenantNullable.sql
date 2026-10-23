-- ============================================================
-- SalesDB Migration 012 — SalesOrderItems.TenantId (ROADMAP P0-9)
-- The line items are an owned collection of the order (OrderLineItem has no tenant of its own; the parent
-- SalesOrder is tenant-filtered and stamped). The column is NOT NULL with no default, so inserting an order
-- with lines failed with "Cannot insert the value NULL into column 'TenantId'". It may be NULL; existing
-- values are kept, and a tenant is never invented for new rows. Idempotent.
-- ============================================================

USE SalesDB;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesOrderItems') AND name = 'TenantId' AND is_nullable = 0)
    ALTER TABLE SalesOrderItems ALTER COLUMN TenantId UNIQUEIDENTIFIER NULL;
GO
