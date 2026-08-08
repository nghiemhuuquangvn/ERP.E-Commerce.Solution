-- ============================================================
-- ProductDB Demo Data Seed — ProductService (Port 5008, Code: PRD)
-- Migration 011: Seeds ~30 Categories (2-level nesting), ~40 Brands,
-- 1000+ Products, 1000+ ProductVariants, ~5 PriceLists + PriceListItems.
--
-- Follows src/Database/SEED_DATA_CONVENTIONS.md:
--   - Category      : 50000000-0001-0000-0000-{N}   (N = 1..30)
--   - Brand         : 50000000-0002-0000-0000-{N}   (N = 1..40)
--   - Product       : 50000000-0003-0000-0000-{N}   (N = 1..1050)
--   - ProductVariant : 50000000-0004-0000-0000-{N}  (N = 1..~1399)
--   - PriceList     : 50000000-0005-0000-0000-{N}   (N = 1..5)
--   - Product.BaseUomId references SharedMasterDataService's UnitOfMeasure
--     registry (40000000-0005-0000-0000-{N}) by formula only — no DB FK,
--     cross-service by convention (ProductDB cannot verify these rows exist).
--   - PriceLists.CurrencyId is left NULL ("tenant default currency" per the
--     column's own doc comment in 010_...sql) rather than guessing at
--     SharedMasterDataService's Currency seeding order, which does not exist
--     in this repo yet (no src/Database/SharedMasterData folder as of this
--     writing) and so cannot be verified.
--   - TenantId = '00000000-0000-0000-0000-000000000001' everywhere.
--
-- All rows generated set-based (recursive-CTE numbers table cross joined
-- with small word/brand/descriptor arrays) — no hand-written 1000+ literal
-- INSERT VALUES rows. Idempotent: every INSERT is guarded by NOT EXISTS on
-- the target's deterministic Id (or, for PriceListItems, its unique key).
-- Safe to run multiple times.
-- ============================================================

USE ProductDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @ProductCount INT = 1050;   -- 1000+ products (target)

-- ============================================================
-- STEP 0: Numbers table (1..1100, covers products + spare headroom)
-- ============================================================
IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;

;WITH Nums AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Nums WHERE N < 1100
)
SELECT N INTO #Numbers FROM Nums OPTION (MAXRECURSION 0);

-- ============================================================
-- STEP 1: Category staging — 8 top-level parents + 22 leaf children
-- (2-level nesting via ParentCategoryN, e.g. Electronics > Smartphones)
-- ============================================================
IF OBJECT_ID('tempdb..#CatStaging') IS NOT NULL DROP TABLE #CatStaging;

SELECT * INTO #CatStaging
FROM (VALUES
    -- N, Name, Code, ParentCategoryN, SortOrder
    (1,  N'Electronics',            'ELEC',     NULL, 1),
    (2,  N'Fashion',                 'FASH',     NULL, 2),
    (3,  N'Home & Living',           'HOME',     NULL, 3),
    (4,  N'Sports & Outdoors',       'SPRT',     NULL, 4),
    (5,  N'Beauty & Health',         'BEAU',     NULL, 5),
    (6,  N'Groceries',               'GROC',     NULL, 6),
    (7,  N'Toys & Games',            'TOYS',     NULL, 7),
    (8,  N'Automotive',              'AUTO',     NULL, 8),
    (9,  N'Smartphones',             'ELEC-PHN', 1,    1),
    (10, N'Laptops',                 'ELEC-LAP', 1,    2),
    (11, N'Audio & Headphones',      'ELEC-AUD', 1,    3),
    (12, N'Wearables',               'ELEC-WER', 1,    4),
    (13, N'Men''s Clothing',         'FASH-MEN', 2,    1),
    (14, N'Women''s Clothing',       'FASH-WOM', 2,    2),
    (15, N'Footwear',                'FASH-SHO', 2,    3),
    (16, N'Furniture',               'HOME-FUR', 3,    1),
    (17, N'Kitchenware',             'HOME-KIT', 3,    2),
    (18, N'Home Decor',              'HOME-DEC', 3,    3),
    (19, N'Bedding & Bath',          'HOME-BED', 3,    4),
    (20, N'Fitness Equipment',       'SPRT-FIT', 4,    1),
    (21, N'Outdoor Gear',            'SPRT-OUT', 4,    2),
    (22, N'Skincare',                'BEAU-SKN', 5,    1),
    (23, N'Personal Care',           'BEAU-PER', 5,    2),
    (24, N'Snacks & Beverages',      'GROC-SNK', 6,    1),
    (25, N'Pantry Staples',          'GROC-PAN', 6,    2),
    (26, N'Action Figures & Toys',   'TOYS-ACT', 7,    1),
    (27, N'Board Games',             'TOYS-BRD', 7,    2),
    (28, N'Video Games',             'TOYS-VID', 7,    3),
    (29, N'Car Accessories',         'AUTO-CAR', 8,    1),
    (30, N'Motorbike Parts',         'AUTO-MOB', 8,    2)
) AS v(N, Name, Code, ParentCategoryN, SortOrder);

-- Category price range per top-level parent (used for Product pricing below)
IF OBJECT_ID('tempdb..#CategoryMeta') IS NOT NULL DROP TABLE #CategoryMeta;

SELECT * INTO #CategoryMeta
FROM (VALUES
    (1, CAST(15.00 AS DECIMAL(18,2)), CAST(2000.00 AS DECIMAL(18,2))),  -- Electronics
    (2, CAST(8.00  AS DECIMAL(18,2)), CAST(250.00  AS DECIMAL(18,2))),  -- Fashion
    (3, CAST(15.00 AS DECIMAL(18,2)), CAST(800.00  AS DECIMAL(18,2))),  -- Home & Living
    (4, CAST(10.00 AS DECIMAL(18,2)), CAST(500.00  AS DECIMAL(18,2))),  -- Sports & Outdoors
    (5, CAST(5.00  AS DECIMAL(18,2)), CAST(150.00  AS DECIMAL(18,2))),  -- Beauty & Health
    (6, CAST(5.00  AS DECIMAL(18,2)), CAST(60.00   AS DECIMAL(18,2))),  -- Groceries
    (7, CAST(8.00  AS DECIMAL(18,2)), CAST(200.00  AS DECIMAL(18,2))),  -- Toys & Games
    (8, CAST(10.00 AS DECIMAL(18,2)), CAST(900.00  AS DECIMAL(18,2)))   -- Automotive
) AS v(ParentN, PriceMin, PriceMax);

-- Compute Id / ParentCategoryId (self-referencing) and insert into Categories
IF OBJECT_ID('tempdb..#Categories') IS NOT NULL DROP TABLE #Categories;

SELECT
    cs.N,
    CAST('50000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), cs.N), 12) AS UNIQUEIDENTIFIER) AS Id,
    cs.Name,
    cs.Code,
    cs.ParentCategoryN,
    CASE WHEN cs.ParentCategoryN IS NULL THEN NULL
         ELSE CAST('50000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), cs.ParentCategoryN), 12) AS UNIQUEIDENTIFIER)
    END AS ParentCategoryId,
    cs.SortOrder
INTO #Categories
FROM #CatStaging cs;

INSERT INTO Categories (Id, TenantId, Name, ParentCategoryId, SortOrder, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted, Description, Code)
SELECT
    c.Id, @TenantId, c.Name, c.ParentCategoryId, c.SortOrder,
    'seed-script', DATEADD(DAY, -300, GETUTCDATE()), 'seed-script', DATEADD(DAY, -300, GETUTCDATE()), 0,
    NULL, c.Code
FROM #Categories c
WHERE NOT EXISTS (SELECT 1 FROM Categories x WHERE x.Id = c.Id);

-- ============================================================
-- STEP 2: Brands — 40 rows, 5 per top-level parent category (grouped by
-- position: rows 1-5 Electronics-flavored, 6-10 Fashion-flavored, ...)
-- ============================================================
IF OBJECT_ID('tempdb..#Brands') IS NOT NULL DROP TABLE #Brands;

SELECT N, Name, ((N - 1) / 5) + 1 AS ParentN, ((N - 1) % 5) + 1 AS Seq
INTO #Brands
FROM (VALUES
    (1,  N'NovaTech'),           (2,  N'Zentron'),            (3,  N'Pulsar Electronics'), (4,  N'Quantex'),            (5,  N'SilverLine'),
    (6,  N'Urban Thread'),       (7,  N'Velora'),              (8,  N'StitchCraft'),        (9,  N'Meridian Wear'),      (10, N'CasualEdge'),
    (11, N'HearthCraft'),        (12, N'CozyNest'),            (13, N'Lumina Home'),        (14, N'Artisan Living'),     (15, N'Homestead Co'),
    (16, N'PeakForm'),           (17, N'TrailBlaze'),          (18, N'IronCore'),           (19, N'SummitGear'),        (20, N'ActiveEdge'),
    (21, N'PureGlow'),           (22, N'VitaCare'),            (23, N'Bloom Naturals'),     (24, N'Radiance Labs'),      (25, N'Aurelia'),
    (26, N'FarmFresh'),          (27, N'GoldenHarvest'),       (28, N'PantryPride'),        (29, N'GreenValley'),        (30, N'NatureBest'),
    (31, N'FunSpark'),           (32, N'PlayForge'),           (33, N'KidVenture'),         (34, N'BrightPlay'),         (35, N'ToyNest'),
    (36, N'RoadMaster'),         (37, N'GearDrive'),           (38, N'AutoForge'),          (39, N'TorquePro'),          (40, N'DriveTech')
) AS v(N, Name);

INSERT INTO Brands (Id, TenantId, Code, Name, LogoUrl, IsActive, Description, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
SELECT
    CAST('50000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.N), 12) AS UNIQUEIDENTIFIER),
    @TenantId,
    'BRD' + RIGHT('00' + CONVERT(VARCHAR(2), b.N), 2),
    b.Name,
    NULL,
    1,
    NULL,
    'seed-script', DATEADD(DAY, -300, GETUTCDATE()), 'seed-script', DATEADD(DAY, -300, GETUTCDATE()), 0
FROM #Brands b
WHERE NOT EXISTS (
    SELECT 1 FROM Brands x
    WHERE x.Id = CAST('50000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.N), 12) AS UNIQUEIDENTIFIER)
);

-- ============================================================
-- STEP 3: Word pools for Product naming — 48 rows each (6 per parent
-- category, grouped by position, same /6 trick as Brands' /5)
-- ============================================================
IF OBJECT_ID('tempdb..#ModelWords') IS NOT NULL DROP TABLE #ModelWords;

SELECT N, Word, ((N - 1) / 6) + 1 AS ParentN, ((N - 1) % 6) + 1 AS Seq
INTO #ModelWords
FROM (VALUES
    (1,  N'Galaxy'), (2,  N'Nova'), (3,  N'Pulse'), (4,  N'Vertex'), (5,  N'Zenith'), (6,  N'Quantum'),               -- Electronics
    (7,  N'Classic'), (8,  N'Urban'), (9,  N'Metro'), (10, N'Heritage'), (11, N'Aria'), (12, N'Drift'),               -- Fashion
    (13, N'Comfort'), (14, N'Haven'), (15, N'Nest'), (16, N'Aurora'), (17, N'Serene'), (18, N'Meadow'),               -- Home & Living
    (19, N'Trailblazer'), (20, N'Summit'), (21, N'Endurance'), (22, N'Velocity'), (23, N'Apex'), (24, N'Odyssey'),    -- Sports & Outdoors
    (25, N'Radiance'), (26, N'Bloom'), (27, N'Pure'), (28, N'Renew'), (29, N'Glow'), (30, N'Vitality'),               -- Beauty & Health
    (31, N'Harvest'), (32, N'Orchard'), (33, N'Valley'), (34, N'Farmhouse'), (35, N'Golden'), (36, N'Pantry'),        -- Groceries
    (37, N'Adventure'), (38, N'Wonder'), (39, N'Spark'), (40, N'Quest'), (41, N'Funtime'), (42, N'Playtime'),         -- Toys & Games
    (43, N'Roadster'), (44, N'Torque'), (45, N'Highway'), (46, N'Ignition'), (47, N'Cruiser'), (48, N'Voyager')       -- Automotive
) AS v(N, Word);

IF OBJECT_ID('tempdb..#Descriptors') IS NOT NULL DROP TABLE #Descriptors;

SELECT N, Descriptor, ((N - 1) / 6) + 1 AS ParentN, ((N - 1) % 6) + 1 AS Seq
INTO #Descriptors
FROM (VALUES
    (1,  N'128GB Black'), (2,  N'256GB Blue'), (3,  N'64GB Silver'), (4,  N'512GB Gray'), (5,  N'8GB RAM White'), (6,  N'1TB Gold'),                       -- Electronics
    (7,  N'Size S Black'), (8,  N'Size M Navy'), (9,  N'Size L Gray'), (10, N'Size XL Beige'), (11, N'Size 42 Brown'), (12, N'Size 38 White'),              -- Fashion
    (13, N'Set of 4'), (14, N'Set of 6'), (15, N'King Size'), (16, N'Queen Size'), (17, N'3-Piece Set'), (18, N'6-Piece Set'),                              -- Home & Living
    (19, N'Size M'), (20, N'Size L'), (21, N'10kg'), (22, N'20kg'), (23, N'Adjustable Fit'), (24, N'One Size'),                                            -- Sports & Outdoors
    (25, N'50ml'), (26, N'100ml'), (27, N'200ml'), (28, N'30g Tube'), (29, N'Travel Size'), (30, N'Family Pack'),                                          -- Beauty & Health
    (31, N'500g Pack'), (32, N'1kg Pack'), (33, N'6-Pack'), (34, N'12-Pack'), (35, N'250ml Bottle'), (36, N'1L Bottle'),                                   -- Groceries
    (37, N'Ages 3+'), (38, N'Ages 6+'), (39, N'2-Player Set'), (40, N'4-Player Set'), (41, N'Deluxe Edition'), (42, N'Standard Edition'),                  -- Toys & Games
    (43, N'Universal Fit'), (44, N'Set of 2'), (45, N'Set of 4'), (46, N'All-Weather'), (47, N'Heavy Duty'), (48, N'Compact Size')                          -- Automotive
) AS v(N, Descriptor);

-- ============================================================
-- STEP 4: Products staging — 1050 rows, set-based via #Numbers cross
-- joined against the leaf-category / brand / word / descriptor pools above
-- ============================================================
IF OBJECT_ID('tempdb..#ProductsStaging') IS NOT NULL DROP TABLE #ProductsStaging;

SELECT
    n.N,
    CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER) AS Id,
    lc.Id AS CategoryId,
    lc.ParentCategoryN,
    CAST('50000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), br.N), 12) AS UNIQUEIDENTIFIER) AS BrandId,
    (br.Name + ' ' + mw.Word + ' ' +
        (CHAR(65 + (n.N % 26)) + CONVERT(VARCHAR(3), 10 + ((n.N * 37) % 90))) + ' ' + ds.Descriptor) AS Name,
    'SKU-' + RIGHT('0000000' + CONVERT(VARCHAR(7), n.N), 7) AS Sku,
    CASE WHEN (ABS(CHECKSUM(n.N, 'status')) % 100) < 5 THEN 0
         WHEN (ABS(CHECKSUM(n.N, 'status')) % 100) < 8 THEN 2
         ELSE 1
    END AS Status,
    CAST(FLOOR(cm.PriceMin + ((ABS(CHECKSUM(n.N, 'price')) % 10000) / 10000.0) * (cm.PriceMax - cm.PriceMin)) AS DECIMAL(18,2)) + 0.99 AS RefPrice,
    CAST('40000000-0005-0000-0000-' +
        RIGHT('000000000000' + CONVERT(VARCHAR(12), CASE WHEN lc.ParentCategoryN = 6 THEN 1 + (n.N % 4) ELSE 1 END), 12)
        AS UNIQUEIDENTIFIER) AS BaseUomId,
    DATEADD(HOUR, -(ABS(CHECKSUM(n.N, 'hours')) % 24), DATEADD(DAY, -(ABS(CHECKSUM(n.N, 'days')) % 365), GETUTCDATE())) AS CreatedAt
INTO #ProductsStaging
FROM #Numbers n
JOIN #Categories lc ON lc.N = ((n.N - 1) % 22) + 9   -- cycle through the 22 leaf categories
JOIN #CategoryMeta cm ON cm.ParentN = lc.ParentCategoryN
JOIN #Brands br ON br.ParentN = lc.ParentCategoryN AND br.Seq = ((n.N + lc.ParentCategoryN * 2) % 5) + 1
JOIN #ModelWords mw ON mw.ParentN = lc.ParentCategoryN AND mw.Seq = ((n.N + lc.ParentCategoryN) % 6) + 1
JOIN #Descriptors ds ON ds.ParentN = lc.ParentCategoryN AND ds.Seq = ((n.N * 3 + lc.ParentCategoryN) % 6) + 1
WHERE n.N <= @ProductCount;

INSERT INTO Products
    (Id, TenantId, Sku, Name, Description, ProductType, Status, CategoryId, HasImage,
     Slug, MetaTitle, MetaDescription, StockQuantity, LastStockSyncAt, ExtendedDataJson,
     CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted, BrandId, BaseUomId)
SELECT
    ps.Id, @TenantId, ps.Sku, ps.Name,
    N'Demo seed product: ' + ps.Name,
    0, ps.Status, ps.CategoryId, 0,
    LOWER(REPLACE(REPLACE(ps.Name, ' ', '-'), '.', '')) + '-' + CONVERT(VARCHAR(10), ps.N),
    ps.Name, NULL,
    100 + (ABS(CHECKSUM(ps.N, 'stock')) % 900), NULL, NULL,
    'seed-script', ps.CreatedAt, 'seed-script', ps.CreatedAt, 0,
    ps.BrandId, ps.BaseUomId
FROM #ProductsStaging ps
WHERE NOT EXISTS (SELECT 1 FROM Products x WHERE x.Id = ps.Id);

-- ============================================================
-- STEP 5: ProductVariants staging — 1 base variant per product, plus a
-- 2nd variant for ~25% of products and a 3rd for ~8% of products.
-- ============================================================
IF OBJECT_ID('tempdb..#VariantsRaw') IS NOT NULL DROP TABLE #VariantsRaw;

SELECT ps.N AS ProductN, ps.Id AS ProductId, ps.RefPrice, ps.Sku AS ProductSku, 1 AS VariantIndex
INTO #VariantsRaw
FROM #ProductsStaging ps

UNION ALL

SELECT ps.N, ps.Id, ps.RefPrice, ps.Sku, x.VariantIndex
FROM #ProductsStaging ps
CROSS JOIN (VALUES (2), (3)) AS x(VariantIndex)
WHERE (ps.N % 4 = 0 AND x.VariantIndex = 2)
   OR (ps.N % 12 = 0 AND x.VariantIndex = 3);

IF OBJECT_ID('tempdb..#VariantsFinal') IS NOT NULL DROP TABLE #VariantsFinal;

SELECT
    ROW_NUMBER() OVER (ORDER BY ProductN, VariantIndex) AS GlobalN,
    ProductN, ProductId, ProductSku, VariantIndex,
    CAST(RefPrice * (1 + (VariantIndex - 1) * 0.03) AS DECIMAL(18,2)) AS VariantPrice
INTO #VariantsFinal
FROM #VariantsRaw;

INSERT INTO ProductVariants
    (Id, TenantId, ProductId, Sku, Barcode, Cost, Weight, Dimensions, Length, Width, Height,
     DimensionUnit, WeightUnit, ExtendedDataJson, CreatedAt, UpdatedAt, IsDeleted, CreatedBy, Description, UpdatedBy)
SELECT
    CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER),
    @TenantId,
    vf.ProductId,
    vf.ProductSku + '-V' + CONVERT(VARCHAR(1), vf.VariantIndex),
    '890' + RIGHT('0000000000' + CONVERT(VARCHAR(10), vf.GlobalN), 10),
    CAST(vf.VariantPrice * 0.6 AS DECIMAL(18,2)) AS Cost,
    CAST(0.1 + (ABS(CHECKSUM(vf.GlobalN, 'weight')) % 490) / 100.0 AS DECIMAL(18,3)) AS Weight,
    NULL,
    CAST(5 + (ABS(CHECKSUM(vf.GlobalN, 'len')) % 95) AS DECIMAL(18,3)),
    CAST(5 + (ABS(CHECKSUM(vf.GlobalN, 'wid')) % 95) AS DECIMAL(18,3)),
    CAST(5 + (ABS(CHECKSUM(vf.GlobalN, 'hgt')) % 95) AS DECIMAL(18,3)),
    'cm', 'kg', NULL,
    DATEADD(DAY, -280, GETUTCDATE()), DATEADD(DAY, -280, GETUTCDATE()), 0,
    'seed-script',
    CASE vf.VariantIndex WHEN 1 THEN NULL WHEN 2 THEN N'Alternate color/size option' ELSE N'Additional color/size option' END,
    'seed-script'
FROM #VariantsFinal vf
WHERE NOT EXISTS (
    SELECT 1 FROM ProductVariants x
    WHERE x.Id = CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER)
);

-- Fill in Dimensions free-text field from the structured L/W/H columns just inserted
UPDATE pv
SET Dimensions = CONVERT(VARCHAR(10), pv.Length) + 'x' + CONVERT(VARCHAR(10), pv.Width) + 'x' + CONVERT(VARCHAR(10), pv.Height) + ' cm'
FROM ProductVariants pv
WHERE pv.Dimensions IS NULL
  AND EXISTS (SELECT 1 FROM #VariantsFinal vf WHERE CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER) = pv.Id);

-- ============================================================
-- STEP 6: PriceLists — 5 rows (Retail / Wholesale / Ecommerce / VIP
-- Wholesale / Flash Sale). CurrencyId left NULL (tenant default currency) —
-- see header note.
-- ============================================================
IF OBJECT_ID('tempdb..#PriceLists') IS NOT NULL DROP TABLE #PriceLists;

SELECT * INTO #PriceLists
FROM (VALUES
    (1, N'Retail Price List',            N'Standard in-store retail pricing',        0),
    (2, N'Wholesale Price List',         N'Bulk wholesale pricing (min qty 10)',      1),
    (3, N'Ecommerce Storefront Price List', N'Storefront/online pricing',             2),
    (4, N'VIP Wholesale Price List',     N'Preferred wholesale partner pricing (min qty 50)', 1),
    (5, N'Flash Sale - Ecommerce',       N'Time-limited online flash-sale pricing',   2)
) AS v(N, Name, Description, Channel);

INSERT INTO PriceLists (Id, TenantId, Name, Description, EffectiveDate, ExpiryDate, IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted, Channel, CurrencyId)
SELECT
    CAST('50000000-0005-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), pl.N), 12) AS UNIQUEIDENTIFIER),
    @TenantId, pl.Name, pl.Description,
    DATEADD(DAY, -180, GETUTCDATE()),
    CASE WHEN pl.N = 5 THEN DATEADD(DAY, 14, GETUTCDATE()) ELSE NULL END,
    1, 'seed-script', DATEADD(DAY, -180, GETUTCDATE()), 'seed-script', DATEADD(DAY, -180, GETUTCDATE()), 0,
    pl.Channel, NULL
FROM #PriceLists pl
WHERE NOT EXISTS (
    SELECT 1 FROM PriceLists x
    WHERE x.Id = CAST('50000000-0005-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), pl.N), 12) AS UNIQUEIDENTIFIER)
);

-- ============================================================
-- STEP 7: PriceListItems — Retail/Wholesale/Ecommerce cover every variant;
-- VIP Wholesale and Flash Sale cover a subset.
-- ============================================================

-- Retail (list 1): all variants, ~1/7 carry a small discount
INSERT INTO PriceListItems (PriceListId, VariantId, BasePrice, DiscountedPrice, MinQuantity)
SELECT
    CAST('50000000-0005-0000-0000-000000000001' AS UNIQUEIDENTIFIER),
    CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER),
    vf.VariantPrice,
    CASE WHEN vf.GlobalN % 7 = 0 THEN CAST(vf.VariantPrice * 0.9 AS DECIMAL(18,2)) ELSE NULL END,
    1
FROM #VariantsFinal vf
WHERE NOT EXISTS (
    SELECT 1 FROM PriceListItems x
    WHERE x.PriceListId = CAST('50000000-0005-0000-0000-000000000001' AS UNIQUEIDENTIFIER)
      AND x.VariantId = CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER)
      AND x.MinQuantity = 1
);

-- Wholesale (list 2): all variants at 80% of retail, min qty 10
INSERT INTO PriceListItems (PriceListId, VariantId, BasePrice, DiscountedPrice, MinQuantity)
SELECT
    CAST('50000000-0005-0000-0000-000000000002' AS UNIQUEIDENTIFIER),
    CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER),
    CAST(vf.VariantPrice * 0.8 AS DECIMAL(18,2)),
    NULL,
    10
FROM #VariantsFinal vf
WHERE NOT EXISTS (
    SELECT 1 FROM PriceListItems x
    WHERE x.PriceListId = CAST('50000000-0005-0000-0000-000000000002' AS UNIQUEIDENTIFIER)
      AND x.VariantId = CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER)
      AND x.MinQuantity = 10
);

-- Ecommerce (list 3): all variants, ~1/5 carry a storefront discount
INSERT INTO PriceListItems (PriceListId, VariantId, BasePrice, DiscountedPrice, MinQuantity)
SELECT
    CAST('50000000-0005-0000-0000-000000000003' AS UNIQUEIDENTIFIER),
    CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER),
    vf.VariantPrice,
    CASE WHEN vf.GlobalN % 5 = 0 THEN CAST(vf.VariantPrice * 0.85 AS DECIMAL(18,2)) ELSE NULL END,
    1
FROM #VariantsFinal vf
WHERE NOT EXISTS (
    SELECT 1 FROM PriceListItems x
    WHERE x.PriceListId = CAST('50000000-0005-0000-0000-000000000003' AS UNIQUEIDENTIFIER)
      AND x.VariantId = CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER)
      AND x.MinQuantity = 1
);

-- VIP Wholesale (list 4): ~1/3 of variants at 70% of retail, min qty 50
INSERT INTO PriceListItems (PriceListId, VariantId, BasePrice, DiscountedPrice, MinQuantity)
SELECT
    CAST('50000000-0005-0000-0000-000000000004' AS UNIQUEIDENTIFIER),
    CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER),
    CAST(vf.VariantPrice * 0.7 AS DECIMAL(18,2)),
    NULL,
    50
FROM #VariantsFinal vf
WHERE vf.GlobalN % 3 = 0
  AND NOT EXISTS (
    SELECT 1 FROM PriceListItems x
    WHERE x.PriceListId = CAST('50000000-0005-0000-0000-000000000004' AS UNIQUEIDENTIFIER)
      AND x.VariantId = CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER)
      AND x.MinQuantity = 50
);

-- Flash Sale (list 5): ~1/9 of variants, aggressive discount, min qty 1
INSERT INTO PriceListItems (PriceListId, VariantId, BasePrice, DiscountedPrice, MinQuantity)
SELECT
    CAST('50000000-0005-0000-0000-000000000005' AS UNIQUEIDENTIFIER),
    CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER),
    vf.VariantPrice,
    CAST(vf.VariantPrice * 0.6 AS DECIMAL(18,2)),
    1
FROM #VariantsFinal vf
WHERE vf.GlobalN % 9 = 0
  AND NOT EXISTS (
    SELECT 1 FROM PriceListItems x
    WHERE x.PriceListId = CAST('50000000-0005-0000-0000-000000000005' AS UNIQUEIDENTIFIER)
      AND x.VariantId = CAST('50000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), vf.GlobalN), 12) AS UNIQUEIDENTIFIER)
      AND x.MinQuantity = 1
);

-- ============================================================
-- Cleanup + summary
-- ============================================================
DROP TABLE IF EXISTS #Numbers, #CatStaging, #CategoryMeta, #Categories, #Brands,
    #ModelWords, #Descriptors, #ProductsStaging, #VariantsRaw, #VariantsFinal, #PriceLists;

DECLARE @CatCnt INT, @BrandCnt INT, @ProdCnt INT, @VarCnt INT, @PlCnt INT, @PliCnt INT;
SELECT @CatCnt = COUNT(*) FROM Categories;
SELECT @BrandCnt = COUNT(*) FROM Brands;
SELECT @ProdCnt = COUNT(*) FROM Products;
SELECT @VarCnt = COUNT(*) FROM ProductVariants;
SELECT @PlCnt = COUNT(*) FROM PriceLists;
SELECT @PliCnt = COUNT(*) FROM PriceListItems;
PRINT 'Seed complete — Categories: ' + CAST(@CatCnt AS VARCHAR(10))
    + ', Brands: ' + CAST(@BrandCnt AS VARCHAR(10))
    + ', Products: ' + CAST(@ProdCnt AS VARCHAR(10))
    + ', ProductVariants: ' + CAST(@VarCnt AS VARCHAR(10))
    + ', PriceLists: ' + CAST(@PlCnt AS VARCHAR(10))
    + ', PriceListItems: ' + CAST(@PliCnt AS VARCHAR(10));
GO
