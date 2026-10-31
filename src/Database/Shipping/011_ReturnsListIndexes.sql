-- ============================================================
-- ShippingDB Migration 011 — Admin Returns screen (2026-10-31, ROADMAP P2-1b)
-- Index for the list of return parcels (tenant, status, newest first). Idempotent.
-- ============================================================

USE ShippingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReturnShipments_Tenant_Status_Created' AND object_id = OBJECT_ID('ReturnShipments'))
    CREATE INDEX IX_ReturnShipments_Tenant_Status_Created ON ReturnShipments (TenantId, Status, CreatedAt DESC);
GO
