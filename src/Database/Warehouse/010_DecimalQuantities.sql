-- ============================================================
-- WarehouseDB Migration 010 - every stock quantity INT -> DECIMAL(18,6)  (Warehouse upgrade W1, docs/architecture/adr-warehouse-upgrade.md)
-- Manufacturing needs fractional quantities (kg, litre, m3). Tables: Inventories, InventoryLots, InventoryLocationStocks, StockMovements,
-- Reservations (legacy column), Reservation_ReservedItems, StockAdjustments, StocktakeLines, TransferOrderLines, CostConsumptionLogs,
-- InventoryValuationSnapshotLines, WarehouseLocations (capacity), TenantInventorySettings (low-stock threshold).
--
-- Deploy order: Product 019, Purchasing 014, Reporting 010 and the services that read Warehouse events FIRST, then this script and the Warehouse service.
-- Idempotent: a column that is no longer INT is skipped, so a re-run (db-migrate runs every file) changes nothing.
-- Each column keeps its nullability and its default VALUE; the default constraint gets a name (DF_<Table>_<Column>).
-- Inventories.AvailableQuantity (persisted computed) and the two indexes over altered columns are dropped first and recreated after.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Objects that depend on the columns being altered (only while Inventories still has INT quantities).
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Inventories') AND name = 'TotalQuantity' AND TYPE_NAME(user_type_id) = 'int')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Inventories') AND name = 'IX_Inventory_LowStock')
        DROP INDEX IX_Inventory_LowStock ON dbo.Inventories;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Inventories') AND name = 'AvailableQuantity')
        ALTER TABLE dbo.Inventories DROP COLUMN AvailableQuantity;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InventoryLots') AND name = 'RemainingQuantity' AND TYPE_NAME(user_type_id) = 'int')
   AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.InventoryLots') AND name = 'IX_InventoryLots_Product')
    DROP INDEX IX_InventoryLots_Product ON dbo.InventoryLots;
GO

-- 2. Alter the columns.
DECLARE @cols TABLE (TableName SYSNAME, ColumnName SYSNAME);
INSERT INTO @cols VALUES
    ('Inventories', 'TotalQuantity'), ('Inventories', 'ReservedQuantity'), ('Inventories', 'LockedQuantity'), ('Inventories', 'ReorderPoint'), ('Inventories', 'ReorderQuantity'),
    ('Inventories', 'MinStock'), ('Inventories', 'MaxStock'), ('Inventories', 'SafetyStock'),
    ('InventoryLots', 'ReceivedQuantity'), ('InventoryLots', 'RemainingQuantity'), ('InventoryLots', 'ReservedQuantity'),
    ('InventoryLocationStocks', 'Quantity'), ('InventoryLocationStocks', 'ReservedQuantity'), ('InventoryLocationStocks', 'LockedQuantity'),
    ('StockMovements', 'Quantity'),
    ('Reservations', 'Quantity'),
    ('Reservation_ReservedItems', 'Quantity'),
    ('StockAdjustments', 'QuantityDelta'),
    ('StocktakeLines', 'SystemQuantity'), ('StocktakeLines', 'CountedQuantity'),
    ('TransferOrderLines', 'Quantity'),
    ('CostConsumptionLogs', 'Quantity'),
    ('InventoryValuationSnapshotLines', 'Quantity'),
    ('WarehouseLocations', 'Capacity'), ('WarehouseLocations', 'CurrentOccupancy'),
    ('TenantInventorySettings', 'LowStockThreshold');

DECLARE @t SYSNAME, @c SYSNAME, @obj INT, @nullable BIT, @dfName SYSNAME, @dfDef NVARCHAR(MAX), @sql NVARCHAR(MAX);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnName FROM @cols;
OPEN cur;
FETCH NEXT FROM cur INTO @t, @c;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @obj = OBJECT_ID('dbo.' + @t);
    IF @obj IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @obj AND name = @c AND TYPE_NAME(user_type_id) = 'int')
    BEGIN
        SELECT @nullable = is_nullable FROM sys.columns WHERE object_id = @obj AND name = @c;
        SET @dfName = NULL; SET @dfDef = NULL;
        SELECT @dfName = dc.name, @dfDef = dc.definition
        FROM sys.default_constraints dc JOIN sys.columns k ON k.object_id = dc.parent_object_id AND k.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = @obj AND k.name = @c;

        IF @dfName IS NOT NULL EXEC('ALTER TABLE dbo.' + @t + ' DROP CONSTRAINT ' + @dfName);
        SET @sql = 'ALTER TABLE dbo.' + @t + ' ALTER COLUMN ' + QUOTENAME(@c) + ' DECIMAL(18,6) ' + CASE WHEN @nullable = 1 THEN 'NULL' ELSE 'NOT NULL' END + ';';
        EXEC(@sql);
        IF @dfDef IS NOT NULL
        BEGIN
            SET @sql = 'ALTER TABLE dbo.' + @t + ' ADD CONSTRAINT ' + QUOTENAME('DF_' + @t + '_' + @c) + ' DEFAULT ' + @dfDef + ' FOR ' + QUOTENAME(@c) + ';';
            EXEC(@sql);
        END
    END
    FETCH NEXT FROM cur INTO @t, @c;
END
CLOSE cur;
DEALLOCATE cur;
GO

-- 3. Recreate what was dropped.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Inventories') AND name = 'AvailableQuantity')
    ALTER TABLE dbo.Inventories ADD AvailableQuantity AS (TotalQuantity - ReservedQuantity - LockedQuantity) PERSISTED;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Inventories') AND name = 'IX_Inventory_LowStock')
    CREATE INDEX IX_Inventory_LowStock ON dbo.Inventories (TenantId, AvailableQuantity, ReorderPoint);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.InventoryLots') AND name = 'IX_InventoryLots_Product')
    CREATE INDEX IX_InventoryLots_Product ON dbo.InventoryLots (ProductId, RemainingQuantity);
GO
