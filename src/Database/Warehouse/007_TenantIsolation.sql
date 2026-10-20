-- ============================================================
-- WarehouseDB Migration 007 — Tenant isolation (2026-10-20, service authz phase 2)
--
-- WarehouseDbContext now applies a per-tenant query filter and stamps/validates TenantId on every
-- save (CLAUDE.md "Multi-Tenant Isolation"). Rows still at the empty GUID would become
-- invisible to every tenant, so — decision 2026-10-20, same as CRM 005 — they are
-- assigned to the SYSTEM tenant. System*Settings keep TenantId = empty: they are
-- platform-wide (IPlatformWideEntity).
-- Idempotent. Run BEFORE deploying the new WareHouseService.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1) Tables that had no tenant column (the entities were plain POCOs without TenantId).
--    Existing rows -> SYSTEM.
IF OBJECT_ID('Warehouses') IS NOT NULL AND COL_LENGTH('Warehouses', 'TenantId') IS NULL
    ALTER TABLE Warehouses ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_Warehouses_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('BarcodeMappings') IS NOT NULL AND COL_LENGTH('BarcodeMappings', 'TenantId') IS NULL
    ALTER TABLE BarcodeMappings ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_BarcodeMappings_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('CostConsumptionLogs') IS NOT NULL AND COL_LENGTH('CostConsumptionLogs', 'TenantId') IS NULL
    ALTER TABLE CostConsumptionLogs ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_CostConsumptionLogs_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('InventoryLocationStocks') IS NOT NULL AND COL_LENGTH('InventoryLocationStocks', 'TenantId') IS NULL
    ALTER TABLE InventoryLocationStocks ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_InventoryLocationStocks_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('InventoryLots') IS NOT NULL AND COL_LENGTH('InventoryLots', 'TenantId') IS NULL
    ALTER TABLE InventoryLots ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_InventoryLots_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('InventorySerials') IS NOT NULL AND COL_LENGTH('InventorySerials', 'TenantId') IS NULL
    ALTER TABLE InventorySerials ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_InventorySerials_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('InventoryValuationSnapshots') IS NOT NULL AND COL_LENGTH('InventoryValuationSnapshots', 'TenantId') IS NULL
    ALTER TABLE InventoryValuationSnapshots ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_InventoryValuationSnapshots_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('StockAdjustments') IS NOT NULL AND COL_LENGTH('StockAdjustments', 'TenantId') IS NULL
    ALTER TABLE StockAdjustments ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_StockAdjustments_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('Stocktakes') IS NOT NULL AND COL_LENGTH('Stocktakes', 'TenantId') IS NULL
    ALTER TABLE Stocktakes ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_Stocktakes_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
IF OBJECT_ID('TransferOrders') IS NOT NULL AND COL_LENGTH('TransferOrders', 'TenantId') IS NULL
    ALTER TABLE TransferOrders ADD TenantId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_TransferOrders_TenantId DEFAULT '00000000-0000-0000-0000-000000000001' WITH VALUES;
GO

-- 2) Codes/numbers are per tenant: two tenants may both have warehouse "WH01".
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventorySerials_SerialNumber' AND object_id = OBJECT_ID('InventorySerials'))
    DROP INDEX IX_InventorySerials_SerialNumber ON InventorySerials;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_InventorySerials_Tenant_SerialNumber')
    CREATE UNIQUE INDEX UX_InventorySerials_Tenant_SerialNumber ON InventorySerials (TenantId, SerialNumber);
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TransferOrders_Number' AND object_id = OBJECT_ID('TransferOrders'))
    DROP INDEX IX_TransferOrders_Number ON TransferOrders;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TransferOrders_Tenant_Number')
    CREATE UNIQUE INDEX UX_TransferOrders_Tenant_Number ON TransferOrders (TenantId, TransferNumber);
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Stocktakes_Number' AND object_id = OBJECT_ID('Stocktakes'))
    DROP INDEX IX_Stocktakes_Number ON Stocktakes;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Stocktakes_Tenant_Number')
    CREATE UNIQUE INDEX UX_Stocktakes_Tenant_Number ON Stocktakes (TenantId, StocktakeNumber);
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryLocationStocks_Product_Location' AND object_id = OBJECT_ID('InventoryLocationStocks'))
    DROP INDEX IX_InventoryLocationStocks_Product_Location ON InventoryLocationStocks;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_InventoryLocationStocks_Tenant_Product_Location')
    CREATE UNIQUE INDEX UX_InventoryLocationStocks_Tenant_Product_Location ON InventoryLocationStocks (TenantId, ProductId, LocationId);
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Warehouses_Code' AND object_id = OBJECT_ID('Warehouses'))
    DROP INDEX IX_Warehouses_Code ON Warehouses;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Warehouses_Tenant_Code')
    CREATE UNIQUE INDEX UX_Warehouses_Tenant_Code ON Warehouses (TenantId, Code);
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BarcodeMappings_Barcode' AND object_id = OBJECT_ID('BarcodeMappings'))
    DROP INDEX IX_BarcodeMappings_Barcode ON BarcodeMappings;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BarcodeMappings_Tenant_Barcode')
    CREATE UNIQUE INDEX UX_BarcodeMappings_Tenant_Barcode ON BarcodeMappings (TenantId, Barcode);
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryValuationSnapshots_Period_Warehouse' AND object_id = OBJECT_ID('InventoryValuationSnapshots'))
    DROP INDEX IX_InventoryValuationSnapshots_Period_Warehouse ON InventoryValuationSnapshots;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_InventoryValuationSnapshots_Tenant_Period_Warehouse')
    CREATE UNIQUE INDEX UX_InventoryValuationSnapshots_Tenant_Period_Warehouse ON InventoryValuationSnapshots (TenantId, PeriodCode, WarehouseId);
GO

-- Backfill: every tenant column still at the empty GUID -> SYSTEM.
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
             + N' SET TenantId = ''00000000-0000-0000-0000-000000000001'''
             + N' WHERE TenantId = ''00000000-0000-0000-0000-000000000000'';' + CHAR(10)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'TenantId'
WHERE t.name NOT LIKE 'System%Settings';
EXEC sp_executesql @sql;
GO
