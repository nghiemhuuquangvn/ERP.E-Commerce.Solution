-- ============================================================
-- WarehouseDB Migration 006 — Demo data seed
-- Seeds ~5 Warehouses, ~50 WarehouseLocations, 1000 Inventories (one per
-- seeded Product, PK = ProductId), 2000+ StockMovements, and ~300
-- Reservations (a subset referencing a seeded Sales SalesOrder).
--
-- GUID formulas follow src/Database/SEED_DATA_CONVENTIONS.md exactly:
--   Warehouse           80000000-0001-0000-0000-{N}   N in [1,5]
--   WarehouseLocation   80000000-0002-0000-0000-{N}   N in [1,50]
--   Inventories.ProductId (PK, not a separate entity) = Product formula
--                       50000000-0003-0000-0000-{N}   N in [1,1000]
--   StockMovements.ProductId  -> same Product formula, random N per row
--   Reservations.OrderId      -> Sales SalesOrder 70000000-0001-0000-0000-{N},
--                                N in [1,1000], for ~30% of rows
--
-- Notes on the actual schema (verified against 001/002/004/005, not assumed):
--   - Warehouses (added in 004) has NO TenantId column — every other table
--     touched here (WarehouseLocations, Inventories, StockMovements,
--     Reservations) does have TenantId and gets the fixed system tenant.
--   - Inventories.AvailableQuantity is a PERSISTED COMPUTED column
--     (TotalQuantity - ReservedQuantity - LockedQuantity) — never included
--     in an insert column list.
--   - Inventories.ProductId is the table's PRIMARY KEY, not a surrogate Id;
--     one row per seeded Product, so this is a 1:1 iterate-every-N case
--     (per SEED_DATA_CONVENTIONS.md note 5), not a pseudo-random pick.
--   - WarehouseLocations.LocationCode is unique per (LocationCode, TenantId)
--     filtered on IsActive = 1; codes are made globally unique (LOC-001..050)
--     so the filtered unique index is never violated.
--   - Neither StockMovements nor Reservations carry a FK to WarehouseLocations
--     or to another service's tables, so no referential surprises there —
--     Reservations.OrderId is a plain UNIQUEIDENTIFIER NOT NULL with no FK.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- NOTE: @TenantId is NOT declared as a T-SQL variable — each GO starts a new
-- batch and local variables do not survive across GO, so every statement below
-- uses the literal '00000000-0000-0000-0000-000000000001' directly instead.

-- ============================================================
-- 1. Warehouses (~5 rows) — additive alongside the pre-existing NEWID()-keyed
--    'MAIN' warehouse inserted by 004_...sql; these use the deterministic
--    formula so other services' seed scripts can reference them if needed.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Warehouses WHERE Id = '80000000-0001-0000-0000-000000000001')
INSERT INTO Warehouses (Id, Code, Name, Address, IsActive)
VALUES
    ('80000000-0001-0000-0000-000000000001', 'WH-HN',  N'Main Warehouse - Hanoi',        N'Km12 Phap Van, Hoang Liet, Hoang Mai, Hanoi',            1),
    ('80000000-0001-0000-0000-000000000002', 'WH-HCM', N'South Warehouse - HCMC',        N'Lot C4-3, Tan Tao Industrial Park, Binh Tan, HCMC',      1),
    ('80000000-0001-0000-0000-000000000003', 'WH-DN',  N'Central Warehouse - Da Nang',   N'Hoa Khanh Industrial Zone, Lien Chieu, Da Nang',         1),
    ('80000000-0001-0000-0000-000000000004', 'WH-CT',  N'Mekong Warehouse - Can Tho',    N'Tra Noc Industrial Zone, Binh Thuy, Can Tho',            1),
    ('80000000-0001-0000-0000-000000000005', 'WH-HP',  N'North Warehouse - Hai Phong',   N'Nomura Industrial Zone, An Duong, Hai Phong',            1);
GO

-- ============================================================
-- 2. WarehouseLocations (~50 rows), distributed across the 5 warehouses above.
-- ============================================================
;WITH Numbers AS (
    SELECT TOP (50) number + 1 AS N
    FROM master..spt_values
    WHERE type = 'P'
    ORDER BY number
)
INSERT INTO WarehouseLocations
    (LocationId, TenantId, LocationCode, Name, Zone, Aisle, Shelf, Bin, Capacity, CurrentOccupancy, IsActive, CreatedAt, UpdatedAt, WarehouseId)
SELECT
    CAST('80000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
    '00000000-0000-0000-0000-000000000001',
    'LOC-' + RIGHT('000' + CONVERT(VARCHAR(3), n.N), 3),
    N'Zone ' + CHAR(65 + ((n.N - 1) / 10)) + N' - Aisle ' + RIGHT('0' + CONVERT(VARCHAR(2), ((n.N - 1) % 10) + 1), 2),
    CHAR(65 + ((n.N - 1) / 10)),                                   -- Zone A..E
    RIGHT('0' + CONVERT(VARCHAR(2), ((n.N - 1) % 10) + 1), 2),     -- Aisle 01..10
    CONVERT(VARCHAR(1), 1 + ((n.N - 1) / 2) % 3),                  -- Shelf 1..3
    CONVERT(VARCHAR(1), 1 + ((n.N - 1) % 2)),                      -- Bin 1..2
    500 + ((n.N - 1) % 5) * 100,                                   -- Capacity 500..900
    100 + ((n.N * 37) % 400),                                      -- CurrentOccupancy, plausible < Capacity
    1,
    DATEADD(DAY, -1 * (400 + (n.N % 300)), GETUTCDATE()),
    DATEADD(DAY, -1 * (n.N % 60), GETUTCDATE()),
    CAST('80000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N - 1) % 5) + 1), 12) AS UNIQUEIDENTIFIER)
FROM Numbers n
WHERE NOT EXISTS (
    SELECT 1 FROM WarehouseLocations wl
    WHERE wl.LocationId = CAST('80000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
);
GO

-- ============================================================
-- 3. Inventories (1000 rows) — one row per seeded Product (N in [1,1000]);
--    ProductId is the table's PRIMARY KEY, computed directly from the
--    Product GUID formula (50000000-0003-...). ProductName/ProductSku are
--    denormalized display fields local to this DB, synthesized here since
--    ProductService's real catalog is not queryable cross-database.
-- ============================================================
;WITH Numbers AS (
    SELECT TOP (1000) number + 1 AS N
    FROM master..spt_values
    WHERE type = 'P'
    ORDER BY number
),
Adjectives AS (
    SELECT * FROM (VALUES
        (1,N'Premium'),(2,N'Classic'),(3,N'Deluxe'),(4,N'Standard'),(5,N'Compact'),
        (6,N'Portable'),(7,N'Heavy-Duty'),(8,N'Eco'),(9,N'Smart'),(10,N'Wireless'),
        (11,N'Professional'),(12,N'Basic'),(13,N'Advanced'),(14,N'Ultra'),(15,N'Everyday'),
        (16,N'Rugged'),(17,N'Slim'),(18,N'Modular'),(19,N'Vintage'),(20,N'Signature')
    ) AS t(Idx, Word)
),
Nouns AS (
    SELECT * FROM (VALUES
        (1,N'Widget'),(2,N'Gadget'),(3,N'Component'),(4,N'Assembly'),(5,N'Fixture'),
        (6,N'Container'),(7,N'Adapter'),(8,N'Panel'),(9,N'Module'),(10,N'Kit'),
        (11,N'Bracket'),(12,N'Cable'),(13,N'Sensor'),(14,N'Casing'),(15,N'Toolset'),
        (16,N'Cartridge'),(17,N'Frame'),(18,N'Connector'),(19,N'Battery'),(20,N'Display')
    ) AS t(Idx, Word)
)
INSERT INTO Inventories
    (ProductId, TenantId, ProductName, ProductSku, TotalQuantity, ReservedQuantity, LockedQuantity,
     ReorderPoint, ReorderQuantity, Cost, LocationId, LastMovementDate, UpdatedAt, ValuationMethod)
SELECT
    CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
    '00000000-0000-0000-0000-000000000001',
    a.Word + N' ' + b.Word + N' #' + RIGHT('0000' + CONVERT(VARCHAR(4), n.N), 4),
    'SKU-' + RIGHT('000000' + CONVERT(VARCHAR(6), n.N), 6),
    tq.TotalQuantity,
    rq.ReservedQuantity,
    lq.LockedQuantity,
    10 + (n.N % 40),                                                -- ReorderPoint 10..49
    50 + (n.N % 150),                                               -- ReorderQuantity 50..199
    CAST(5 + ((n.N * 97) % 49600) / 100.0 AS DECIMAL(18,2)),        -- Cost 5.00..500.99
    CAST('80000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 13) % 50) + 1), 12) AS UNIQUEIDENTIFIER),
    DATEADD(DAY, -1 * (n.N % 365), GETUTCDATE()),
    GETUTCDATE(),
    0
FROM Numbers n
JOIN Adjectives a ON a.Idx = ((n.N - 1) % 20) + 1
JOIN Nouns b ON b.Idx = ((n.N / 20) % 20) + 1
CROSS APPLY (SELECT CONVERT(INT, 50 + ((n.N * 53) % 1950)) AS TotalQuantity) tq
CROSS APPLY (SELECT CONVERT(INT, tq.TotalQuantity * (0.05 + ((n.N % 20) * 0.02))) AS ReservedQuantity) rq
CROSS APPLY (SELECT CONVERT(INT, (n.N % 5)) AS LockedQuantity) lq
WHERE NOT EXISTS (
    SELECT 1 FROM Inventories inv
    WHERE inv.ProductId = CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
);
GO

-- ============================================================
-- 4. StockMovements (2000 rows) — random Product per row, spread over the
--    last ~12 months, MovementType in {0=Receipt,1=Issue,2=Transfer,3=Adjustment}.
-- ============================================================
;WITH Numbers AS (
    SELECT TOP (2000) number + 1 AS N
    FROM master..spt_values
    WHERE type = 'P'
    ORDER BY number
),
Reasons AS (
    SELECT * FROM (VALUES
        (0, N'Receipt',    N'Purchase order receipt'),
        (1, N'Issue',      N'Customer sales order fulfillment'),
        (2, N'Transfer',   N'Inter-warehouse stock transfer'),
        (3, N'Adjustment', N'Physical stocktake correction')
    ) AS t(TypeCode, TypeName, ReasonText)
)
INSERT INTO StockMovements
    (MovementId, TenantId, ProductId, Quantity, MovementType, FromLocationId, ToLocationId,
     FromLocation, ToLocation, Reference, ReferenceId, Reason, RecordedBy, RecordedAt)
SELECT
    NEWID(),
    '00000000-0000-0000-0000-000000000001',
    CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), prod.ProductN), 12) AS UNIQUEIDENTIFIER),
    CASE r.TypeCode
        WHEN 0 THEN 10 + (n.N % 200)          -- Receipt: positive in
        WHEN 1 THEN -1 * (5 + (n.N % 100))    -- Issue: negative out
        WHEN 2 THEN CASE WHEN n.N % 2 = 0 THEN 10 + (n.N % 50) ELSE -1 * (10 + (n.N % 50)) END
        ELSE CASE WHEN n.N % 3 = 0 THEN -1 * (1 + (n.N % 20)) ELSE (1 + (n.N % 20)) END
    END,
    r.TypeCode,
    CASE WHEN r.TypeCode IN (1,2) THEN
        CAST('80000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 7) % 50) + 1), 12) AS UNIQUEIDENTIFIER)
    ELSE NULL END,
    CASE WHEN r.TypeCode IN (0,2) THEN
        CAST('80000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 11) % 50) + 1), 12) AS UNIQUEIDENTIFIER)
    ELSE NULL END,
    CASE WHEN r.TypeCode IN (1,2) THEN 'LOC-' + RIGHT('000' + CONVERT(VARCHAR(3), ((n.N * 7) % 50) + 1), 3) ELSE NULL END,
    CASE WHEN r.TypeCode IN (0,2) THEN 'LOC-' + RIGHT('000' + CONVERT(VARCHAR(3), ((n.N * 11) % 50) + 1), 3) ELSE NULL END,
    CASE r.TypeCode
        WHEN 0 THEN 'PO-2026-' + RIGHT('00000' + CONVERT(VARCHAR(5), (n.N % 500) + 1), 5)
        WHEN 1 THEN 'SO-2026-' + RIGHT('00000' + CONVERT(VARCHAR(5), (n.N % 1000) + 1), 5)
        WHEN 2 THEN 'TRF-2026-' + RIGHT('00000' + CONVERT(VARCHAR(5), (n.N % 200) + 1), 5)
        ELSE 'ADJ-2026-' + RIGHT('00000' + CONVERT(VARCHAR(5), (n.N % 300) + 1), 5)
    END,
    NULL,
    r.ReasonText,
    'warehouse.staff' + CONVERT(VARCHAR(2), (n.N % 10) + 1) + '@erp.local',
    DATEADD(MINUTE, -1 * (n.N % 1440), DATEADD(DAY, -1 * (n.N % 365), GETUTCDATE()))
FROM Numbers n
CROSS APPLY (SELECT ((n.N * 31) % 1000) + 1 AS ProductN) prod
JOIN Reasons r ON r.TypeCode = (n.N % 4)
WHERE (SELECT COUNT(*) FROM StockMovements) < 2000;
GO

-- ============================================================
-- 5. Reservations (300 rows) — ~30% reference a seeded Sales SalesOrder
--    (70000000-0001-0000-0000-{N}); the remainder are non-order (manual/POS)
--    holds and get a fresh GUID since Reservations.OrderId has no FK.
-- ============================================================
;WITH Numbers AS (
    SELECT TOP (300) number + 1 AS N
    FROM master..spt_values
    WHERE type = 'P'
    ORDER BY number
)
INSERT INTO Reservations
    (ReservationId, TenantId, OrderId, ProductId, Quantity, Status, HoldUntil, ReservedAt, ReleasedAt, ConsumedAt, ReleaseReason)
SELECT
    NEWID(),
    '00000000-0000-0000-0000-000000000001',
    CASE WHEN n.N % 10 < 3 THEN
        CAST('70000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 17) % 1000) + 1), 12) AS UNIQUEIDENTIFIER)
    ELSE NEWID() END,
    CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 41) % 1000) + 1), 12) AS UNIQUEIDENTIFIER),
    1 + (n.N % 20),
    st.Status,
    DATEADD(DAY, 7, ra.ReservedAt),
    ra.ReservedAt,
    CASE WHEN st.Status = 1 THEN DATEADD(DAY, 1 + (n.N % 5), ra.ReservedAt) ELSE NULL END,
    CASE WHEN st.Status = 2 THEN DATEADD(DAY, 1 + (n.N % 5), ra.ReservedAt) ELSE NULL END,
    CASE WHEN st.Status = 1 THEN N'Order cancelled' WHEN st.Status = 3 THEN N'Hold expired' ELSE NULL END
FROM Numbers n
CROSS APPLY (SELECT DATEADD(DAY, -1 * (n.N % 365), GETUTCDATE()) AS ReservedAt) ra
CROSS APPLY (SELECT CASE (n.N % 4) WHEN 0 THEN 0 WHEN 1 THEN 1 WHEN 2 THEN 2 ELSE 3 END AS Status) st
WHERE (SELECT COUNT(*) FROM Reservations) < 300;
GO
