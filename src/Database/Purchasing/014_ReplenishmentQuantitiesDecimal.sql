-- ============================================================
-- PurchasingDB Migration 014 - PurchaseRequests quantities INT -> DECIMAL(18,6) (Warehouse upgrade W1)
-- A stock alert from the Warehouse now carries decimal quantities. Deploy this BEFORE the Warehouse change.
-- Idempotent: only columns that are still INT are altered; each default is replaced by a named one.
-- ============================================================

USE PurchasingDB;
GO

DECLARE @cols TABLE (ColumnName SYSNAME, DefaultName SYSNAME);
INSERT INTO @cols VALUES
    ('CurrentQuantity', 'DF_PurchaseRequests_CurrentQuantity'), ('ReorderPoint', 'DF_PurchaseRequests_ReorderPoint'), ('SuggestedQuantity', 'DF_PurchaseRequests_SuggestedQuantity'),
    ('AvailableQuantity', 'DF_PurchaseRequests_Available'), ('OnOrderQuantity', 'DF_PurchaseRequests_OnOrder'), ('MinStock', 'DF_PurchaseRequests_MinStock'),
    ('MaxStock', 'DF_PurchaseRequests_MaxStock'), ('SafetyStock', 'DF_PurchaseRequests_SafetyStock');

DECLARE @col SYSNAME, @dn SYSNAME, @df NVARCHAR(256), @sql NVARCHAR(MAX);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT ColumnName, DefaultName FROM @cols;
OPEN c;
FETCH NEXT FROM c INTO @col, @dn;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PurchaseRequests') AND name = @col AND TYPE_NAME(user_type_id) = 'int')
    BEGIN
        SET @df = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns k ON k.object_id = dc.parent_object_id AND k.column_id = dc.parent_column_id
                   WHERE dc.parent_object_id = OBJECT_ID('dbo.PurchaseRequests') AND k.name = @col);
        IF @df IS NOT NULL EXEC('ALTER TABLE dbo.PurchaseRequests DROP CONSTRAINT ' + @df);
        SET @sql = 'ALTER TABLE dbo.PurchaseRequests ALTER COLUMN ' + QUOTENAME(@col) + ' DECIMAL(18,6) NOT NULL; '
                 + 'ALTER TABLE dbo.PurchaseRequests ADD CONSTRAINT ' + QUOTENAME(@dn) + ' DEFAULT 0 FOR ' + QUOTENAME(@col) + ';';
        EXEC(@sql);
    END
    FETCH NEXT FROM c INTO @col, @dn;
END
CLOSE c;
DEALLOCATE c;
GO
