-- ============================================================
-- PurchasingDB Demo Seed Data — PurchaseOrderService (Port 5003, Code: PO)
-- Migration 007 (2026-08-03)
--
-- Seeds realistic demo data so Admin/Storefront/mobile frontends have real
-- data to render for this service:
--   - ~50  Suppliers            (Vietnamese + international trading partners)
--   - 500+ PurchaseOrders       (varied status, spread over the last ~12 months)
--   - 2-6  PurchaseOrderItems per PurchaseOrder (line totals roll up into the
--          parent PurchaseOrder.TotalAmount_Amount after insertion)
--   - ~100 PurchaseRequests     (reorder suggestions against random products)
--
-- Deterministic cross-service GUID formula per
-- src/Database/SEED_DATA_CONVENTIONS.md — NEVER NEWID()/DEFAULT for these:
--   Supplier (this DB)            90000000-0001-0000-0000-{N}   N in [1,50]
--   PurchaseOrder (this DB)       90000000-0002-0000-0000-{N}   N in [1,550]
--   Product (ProductService, external, referenced only — no local FK)
--                                 50000000-0003-0000-0000-{N}   N in [1,1000]
-- PurchaseOrderItems.Id / PurchaseRequests.Id are local-only rows (never
-- cross-referenced) and use NEWID()/DEFAULT per the conventions doc.
--
-- Schema surprises adapted to (verified against the actual migration files,
-- not any earlier summary):
--   - PurchaseOrders.Status comment in 001 says "0=Draft,1=Submitted,2=Approved,
--     3=Received,4=Cancelled" but the real C# enum
--     (ERP.PO.Domain.Enums.PurchaseOrderStatus) is
--     Draft=0, Submitted=1, Approved=2, Ordered=3, PartiallyReceived=4,
--     Received=5, Cancelled=6, Rejected=7 — this script uses the REAL enum
--     values so seeded rows match what the application actually understands.
--   - PurchaseOrderItems.Status uses ERP.PO.Domain.Enums.PurchaseOrderItemStatus:
--     Pending=0, PartiallyReceived=1, Received=2, Cancelled=3.
--   - PurchaseOrders.RowVersion is a ROWVERSION (timestamp) column — omitted
--     from the insert column list; SQL Server populates it automatically.
--   - PurchaseRequests (006) intentionally has NO TenantId / no composite key
--     (per that migration's own header note) — seeded rows follow suit.
--   - GoodsReceipts / SupplierRatings / SupplierItems (003/005) are out of
--     scope for this pass (not requested) and are left unseeded.
-- ============================================================

USE PurchasingDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
GO

DECLARE @TenantId             UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @SupplierCount        INT = 50;
DECLARE @PurchaseOrderCount   INT = 550;
DECLARE @PurchaseRequestCount INT = 100;

-- ------------------------------------------------------------
-- Reusable numbers table (1..1000) via recursive CTE, per
-- SEED_DATA_CONVENTIONS.md guidance — used to drive every set-based
-- generator below instead of hand-written literal INSERT rows.
-- ------------------------------------------------------------
IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
CREATE TABLE #Numbers (N INT PRIMARY KEY);

;WITH Nums AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Nums WHERE N < 1000
)
INSERT INTO #Numbers (N)
SELECT N FROM Nums
OPTION (MAXRECURSION 0);

-- ============================================================
-- Suppliers (~50) — realistic Vietnamese + international trading partners
-- ============================================================
IF (SELECT COUNT(*) FROM Suppliers WHERE TenantId = @TenantId) < @SupplierCount
BEGIN
    DECLARE @SupplierSeed TABLE (
        Idx     INT IDENTITY(1,1) PRIMARY KEY,
        Name    NVARCHAR(255),
        Slug    NVARCHAR(50),
        City    NVARCHAR(100),
        Country NVARCHAR(100),
        IsVN    BIT
    );

    INSERT INTO @SupplierSeed (Name, Slug, City, Country, IsVN) VALUES
    ('Hoang Long Trading Co., Ltd',           'hoanglongtrading',    'Ho Chi Minh City', 'Vietnam',     1),
    ('Sao Mai Import-Export JSC',             'saomaiimportexport',  'Hanoi',             'Vietnam',     1),
    ('Minh Phat Materials Corp',              'minhphatmaterials',   'Da Nang',           'Vietnam',     1),
    ('Thien Long Office Supplies JSC',        'thienlongoffice',     'Ho Chi Minh City', 'Vietnam',     1),
    ('Dai Dong Packaging Co., Ltd',           'daidongpackaging',    'Binh Duong',        'Vietnam',     1),
    ('Phu Thai Distribution JSC',             'phuthaidistribution', 'Hanoi',             'Vietnam',     1),
    ('An Phat Plastics Corp',                 'anphatplastics',      'Hai Phong',         'Vietnam',     1),
    ('Viet Tien Garment Materials Co., Ltd',  'viettiengarment',     'Ho Chi Minh City', 'Vietnam',     1),
    ('Kim Long Metal Works Co., Ltd',         'kimlongmetal',        'Dong Nai',          'Vietnam',     1),
    ('Hoa Sen Steel Trading JSC',             'hoasensteel',         'Ho Chi Minh City', 'Vietnam',     1),
    ('Song Hong Electronics Co., Ltd',        'songhongelectronics', 'Hanoi',             'Vietnam',     1),
    ('Nam Viet Food Supply JSC',              'namvietfood',         'Can Tho',           'Vietnam',     1),
    ('Tan Hiep Phat Ingredients Co., Ltd',    'tanhiepphating',      'Binh Duong',        'Vietnam',     1),
    ('Bao Minh Chemicals Corp',               'baominhchemicals',    'Ho Chi Minh City', 'Vietnam',     1),
    ('Duc Giang Hardware Co., Ltd',           'ducgianghardware',    'Hanoi',             'Vietnam',     1),
    ('Vinh Phat Timber & Furniture JSC',      'vinhphattimber',      'Da Nang',           'Vietnam',     1),
    ('Thanh Cong Textile Co., Ltd',           'thanhcongtextile',    'Ho Chi Minh City', 'Vietnam',     1),
    ('Long Thanh Electrical Supplies Corp',   'longthanhelectrical', 'Long An',           'Vietnam',     1),
    ('Gia Dinh Paper Products JSC',           'giadinhpaper',        'Ho Chi Minh City', 'Vietnam',     1),
    ('Hong Ha Stationery Co., Ltd',           'honghastationery',    'Hanoi',             'Vietnam',     1),
    ('Sunrise Global Trading Pte. Ltd.',      'sunriseglobaltrading','Singapore',         'Singapore',   0),
    ('Guangzhou Fortune Manufacturing Co., Ltd','guangzhoufortune', 'Guangzhou',         'China',       0),
    ('Shenzhen Bright Electronics Co., Ltd',  'shenzhenbright',      'Shenzhen',          'China',       0),
    ('Yiwu Merchandise Import-Export Co.',    'yiwumerchandise',     'Yiwu',              'China',       0),
    ('Tokyo Precision Parts K.K.',            'tokyoprecision',      'Tokyo',             'Japan',       0),
    ('Osaka Industrial Supply Corp',          'osakaindustrial',     'Osaka',             'Japan',       0),
    ('Seoul Han River Trading Co., Ltd',      'seoulhanriver',       'Seoul',             'South Korea', 0),
    ('Busan Textile Materials Co.',           'busantextile',        'Busan',             'South Korea', 0),
    ('Bangkok Agro Products Co., Ltd',        'bangkokagro',         'Bangkok',           'Thailand',    0),
    ('Jakarta Nusantara Supplies PT',         'jakartanusantara',    'Jakarta',           'Indonesia',   0),
    ('Kuala Lumpur Trading Sdn. Bhd.',        'klmtrading',          'Kuala Lumpur',      'Malaysia',    0),
    ('Mumbai Textile Exports Pvt. Ltd.',      'mumbaitextile',       'Mumbai',            'India',       0),
    ('Chennai Auto Components Ltd.',          'chennaiauto',         'Chennai',           'India',       0),
    ('Hamburg Industrial Components GmbH',    'hamburgindustrial',   'Hamburg',           'Germany',     0),
    ('Munich Precision Tools GmbH',           'munichprecision',     'Munich',            'Germany',     0),
    ('Lyon Packaging Solutions S.A.R.L.',     'lyonpackaging',       'Lyon',              'France',      0),
    ('Milan Fashion Fabrics S.p.A.',          'milanfashion',        'Milan',             'Italy',       0),
    ('Rotterdam Global Logistics Supply B.V.','rotterdamglobal',    'Rotterdam',         'Netherlands', 0),
    ('Chicago Industrial Parts Inc.',         'chicagoindustrial',  'Chicago',           'USA',         0),
    ('Los Angeles Consumer Goods LLC',        'laconsumergoods',    'Los Angeles',       'USA',         0),
    ('Houston Petro Materials Inc.',          'houstonpetro',       'Houston',           'USA',         0),
    ('Toronto Northern Supplies Ltd.',        'torontonorthern',    'Toronto',           'Canada',      0),
    ('Sydney Pacific Trading Pty Ltd',        'sydneypacific',      'Sydney',            'Australia',   0),
    ('Can Tho Agri Produce Co., Ltd',         'canthoagri',         'Can Tho',           'Vietnam',     1),
    ('Hai Phong Marine Equipment JSC',        'haiphongmarine',     'Hai Phong',         'Vietnam',     1),
    ('Vung Tau Offshore Supplies Co., Ltd',   'vungtauoffshore',    'Vung Tau',          'Vietnam',     1),
    ('Nha Trang Seafood Packaging JSC',       'nhatrangseafood',    'Nha Trang',         'Vietnam',     1),
    ('Hue Traditional Crafts Co., Ltd',       'huetraditionalcrafts','Hue',              'Vietnam',     1),
    ('Bien Hoa Industrial Materials Corp',    'bienhoaindustrial',  'Dong Nai',          'Vietnam',     1),
    ('Thu Duc Tech Components JSC',           'thuductech',         'Ho Chi Minh City',  'Vietnam',     1);

    INSERT INTO Suppliers (
        Id, TenantId, Code, Name, Email, Phone, ContactPerson, TaxCode,
        Address_Street, Address_City, Address_State, Address_PostalCode, Address_Country,
        PaymentTermDays, CurrencyCode, IsActive, Rating, Notes,
        CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
    )
    SELECT
        CAST('90000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.Idx), 12) AS UNIQUEIDENTIFIER),
        @TenantId,
        'SUP-' + RIGHT('000' + CAST(s.Idx AS VARCHAR(3)), 3),
        s.Name,
        LOWER(s.Slug) + '@supplier-demo.com',
        CASE WHEN s.IsVN = 1 THEN '+84-28-' ELSE '+1-555-' END + RIGHT('0000000' + CAST(1000000 + s.Idx * 137 AS VARCHAR(10)), 7),
        CASE s.Idx % 10
            WHEN 0 THEN 'Nguyen Van An'
            WHEN 1 THEN 'Tran Thi Bich'
            WHEN 2 THEN 'Le Hoang Nam'
            WHEN 3 THEN 'Pham Thi Lan'
            WHEN 4 THEN 'Vo Minh Tuan'
            WHEN 5 THEN 'John Smith'
            WHEN 6 THEN 'Maria Garcia'
            WHEN 7 THEN 'Wei Zhang'
            WHEN 8 THEN 'Hiroshi Tanaka'
            ELSE 'Anna Mueller'
        END,
        CASE WHEN s.IsVN = 1
             THEN '03' + RIGHT('00000000' + CAST(1000000 + s.Idx * 911 AS VARCHAR(10)), 8)
             ELSE 'TAX' + RIGHT('0000000' + CAST(s.Idx AS VARCHAR(7)), 7)
        END,
        'No. ' + CAST(100 + s.Idx * 7 AS VARCHAR(10)) + ' Industrial Avenue',
        s.City,
        NULL,
        RIGHT('00000' + CAST(10000 + s.Idx * 3 AS VARCHAR(5)), 5),
        s.Country,
        CASE s.Idx % 4 WHEN 0 THEN 15 WHEN 1 THEN 30 WHEN 2 THEN 45 ELSE 60 END,
        CASE WHEN s.IsVN = 1 THEN 'VND' ELSE 'USD' END,
        CASE WHEN s.Idx % 17 = 0 THEN 0 ELSE 1 END,
        CAST(3.0 + (ABS(CHECKSUM(NEWID())) % 21) / 10.0 AS DECIMAL(3, 2)),
        NULL,
        'seed-script',
        DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 400), GETUTCDATE()),
        'seed-script',
        DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 60), GETUTCDATE()),
        0
    FROM @SupplierSeed s
    WHERE NOT EXISTS (
        SELECT 1 FROM Suppliers x
        WHERE x.Id = CAST('90000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.Idx), 12) AS UNIQUEIDENTIFIER)
          AND x.TenantId = @TenantId
    );
END

-- ============================================================
-- PurchaseOrders (550) — spread over the last ~12 months, status age-weighted
-- so older orders skew Received/Cancelled and recent ones skew Draft/Submitted.
-- ============================================================
IF (SELECT COUNT(*) FROM PurchaseOrders WHERE TenantId = @TenantId AND PONumber LIKE 'PO-%') < @PurchaseOrderCount
BEGIN
    INSERT INTO PurchaseOrders (
        Id, TenantId, PONumber, SupplierId, Status, OrderDate, ExpectedDeliveryDate, ActualDeliveryDate,
        TotalAmount_Amount, TotalAmount_Currency, Notes,
        ApprovedBy, ApprovedAt, CancelledBy, CancelledAt, CancellationReason, RejectionReason,
        ScheduledReceiptDate, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
    )
    SELECT
        CAST('90000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
        @TenantId,
        'PO-' + RIGHT('000000' + CAST(n.N AS VARCHAR(6)), 6),
        CAST('90000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), r.SupplierN), 12) AS UNIQUEIDENTIFIER),
        s.Status,
        r.OrderDate,
        d.ExpectedDeliveryDate,
        a.ActualDeliveryDate,
        0, -- TotalAmount_Amount: rolled up from PurchaseOrderItems.LineTotal below
        'USD',
        NULL,
        CASE WHEN s.Status IN (2, 3, 4, 5, 6) THEN 'seed-script' ELSE NULL END,
        CASE WHEN s.Status IN (2, 3, 4, 5, 6) THEN DATEADD(DAY, 1 + (r.LeadDays % 3), r.OrderDate) ELSE NULL END,
        CASE WHEN s.Status = 6 THEN 'seed-script' ELSE NULL END,
        CASE WHEN s.Status = 6 THEN DATEADD(DAY, 3 + (r.LeadDays % 5), r.OrderDate) ELSE NULL END,
        CASE WHEN s.Status = 6 THEN 'Cancelled per buyer request during demo data generation' ELSE NULL END,
        CASE WHEN s.Status = 7 THEN 'Rejected by approver — pricing or quality did not meet requirements' ELSE NULL END,
        CASE WHEN s.Status IN (3, 4, 5) THEN d.ExpectedDeliveryDate ELSE NULL END,
        'seed-script',
        r.OrderDate,
        'seed-script',
        CASE WHEN s.Status IN (5, 6, 7) THEN d.ExpectedDeliveryDate ELSE r.OrderDate END,
        0
    FROM #Numbers n
    CROSS APPLY (
        SELECT
            CAST(DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 365), GETUTCDATE()) AS DATETIME2) AS OrderDate,
            ABS(CHECKSUM(NEWID())) % 100 AS StatusRoll,
            1 + ABS(CHECKSUM(NEWID())) % 50 AS SupplierN,
            ABS(CHECKSUM(NEWID())) % 24 AS LeadDays,
            ABS(CHECKSUM(NEWID())) % 100 AS ActiveRoll
    ) r
    CROSS APPLY (
        SELECT
            CASE
                WHEN DATEDIFF(DAY, r.OrderDate, GETUTCDATE()) <= 5 THEN
                    CASE WHEN r.StatusRoll < 60 THEN 0 ELSE 1 END
                WHEN DATEDIFF(DAY, r.OrderDate, GETUTCDATE()) <= 15 THEN
                    CASE WHEN r.StatusRoll < 30 THEN 1 WHEN r.StatusRoll < 70 THEN 2 ELSE 3 END
                WHEN DATEDIFF(DAY, r.OrderDate, GETUTCDATE()) <= 30 THEN
                    CASE WHEN r.StatusRoll < 20 THEN 3 WHEN r.StatusRoll < 60 THEN 4 WHEN r.StatusRoll < 90 THEN 5 ELSE 6 END
                ELSE
                    CASE WHEN r.StatusRoll < 75 THEN 5 WHEN r.StatusRoll < 85 THEN 4 WHEN r.StatusRoll < 93 THEN 6 ELSE 7 END
            END AS Status
    ) s
    CROSS APPLY (
        SELECT DATEADD(DAY, 7 + r.LeadDays, r.OrderDate) AS ExpectedDeliveryDate
    ) d
    CROSS APPLY (
        SELECT
            CASE WHEN s.Status IN (4, 5) THEN DATEADD(DAY, (r.ActiveRoll % 5) - 2, d.ExpectedDeliveryDate) ELSE NULL END AS ActualDeliveryDate
    ) a
    WHERE n.N <= @PurchaseOrderCount
      AND NOT EXISTS (
        SELECT 1 FROM PurchaseOrders x
        WHERE x.Id = CAST('90000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
          AND x.TenantId = @TenantId
      );
END

-- ============================================================
-- PurchaseOrderItems (2-6 per PurchaseOrder) — ProductId references
-- ProductService's Product by the deterministic formula (50000000-0003-...).
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM PurchaseOrderItems
    WHERE PurchaseOrderId = CAST('90000000-0002-0000-0000-000000000001' AS UNIQUEIDENTIFIER)
)
BEGIN
    INSERT INTO PurchaseOrderItems (
        Id, TenantId, PurchaseOrderId, ProductId, ProductCode, ProductName,
        Quantity, ReceivedQuantity, Unit, UnitPrice_Amount, UnitPrice_Currency,
        TaxRate, TaxAmount, DiscountPercent, LineTotal, Status, RejectedQuantity,
        CreatedAt, UpdatedAt, IsDeleted
    )
    SELECT
        NEWID(),
        @TenantId,
        po.Id,
        CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), p.ProductN), 12) AS UNIQUEIDENTIFIER),
        'PRD-' + RIGHT('0000' + CAST(p.ProductN AS VARCHAR(4)), 4),
        CASE p.ProductN % 10
            WHEN 0 THEN 'Premium' WHEN 1 THEN 'Classic' WHEN 2 THEN 'Deluxe' WHEN 3 THEN 'Standard'
            WHEN 4 THEN 'Compact' WHEN 5 THEN 'Industrial' WHEN 6 THEN 'Eco' WHEN 7 THEN 'Heavy-Duty'
            WHEN 8 THEN 'Portable' ELSE 'Professional'
        END + ' ' +
        CASE p.ProductN % 12
            WHEN 0 THEN 'Widget' WHEN 1 THEN 'Component' WHEN 2 THEN 'Fastener' WHEN 3 THEN 'Panel'
            WHEN 4 THEN 'Module' WHEN 5 THEN 'Bracket' WHEN 6 THEN 'Cable' WHEN 7 THEN 'Sensor'
            WHEN 8 THEN 'Valve' WHEN 9 THEN 'Bearing' WHEN 10 THEN 'Container' ELSE 'Adapter'
        END + ' #' + CAST(p.ProductN AS VARCHAR(10)),
        calc.Qty,
        CASE po.Status WHEN 5 THEN calc.Qty WHEN 4 THEN CAST(calc.Qty * 0.5 AS DECIMAL(18, 2)) ELSE 0 END,
        CASE p.ProductN % 5 WHEN 0 THEN 'pcs' WHEN 1 THEN 'box' WHEN 2 THEN 'kg' WHEN 3 THEN 'carton' ELSE 'set' END,
        calc.UnitPrice,
        'USD',
        calc.TaxRate,
        tax.TaxAmt,
        calc.DiscountPercent,
        lt.LineTotal,
        CASE po.Status WHEN 5 THEN 2 WHEN 4 THEN 1 WHEN 6 THEN 3 WHEN 7 THEN 3 ELSE 0 END,
        CASE WHEN po.Status = 7 THEN calc.Qty ELSE 0 END,
        po.OrderDate,
        po.OrderDate,
        0
    FROM #Numbers n
    INNER JOIN PurchaseOrders po
        ON po.Id = CAST('90000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
        AND po.TenantId = @TenantId
    CROSS JOIN #Numbers m
    CROSS APPLY (
        SELECT 2 + (ABS(CHECKSUM(CAST(po.Id AS VARBINARY(16)))) % 5) AS ItemCount
    ) ic
    CROSS APPLY (
        SELECT 1 + ABS(CHECKSUM(HASHBYTES('MD5', CAST(po.Id AS VARBINARY(16)) + CAST(m.N AS VARBINARY(4))))) % 1000 AS ProductN
    ) p
    CROSS APPLY (
        SELECT
            1 + ABS(CHECKSUM(NEWID())) % 48 AS Qty,
            CAST(5 + (ABS(CHECKSUM(NEWID())) % 49600) / 100.0 AS DECIMAL(18, 2)) AS UnitPrice,
            CASE p.ProductN % 4 WHEN 0 THEN 0 WHEN 1 THEN 5 WHEN 2 THEN 8 ELSE 10 END AS TaxRate,
            CASE WHEN p.ProductN % 7 = 0 THEN 5 WHEN p.ProductN % 5 = 0 THEN 10 ELSE 0 END AS DiscountPercent
    ) calc
    CROSS APPLY (
        SELECT CAST(calc.Qty * calc.UnitPrice AS DECIMAL(18, 2)) AS Subtotal
    ) sub
    CROSS APPLY (
        SELECT CAST(sub.Subtotal * calc.DiscountPercent / 100.0 AS DECIMAL(18, 2)) AS DiscountAmount
    ) disc
    CROSS APPLY (
        SELECT CAST(sub.Subtotal - disc.DiscountAmount AS DECIMAL(18, 2)) AS Taxable
    ) tbl
    CROSS APPLY (
        SELECT CAST(tbl.Taxable * calc.TaxRate / 100.0 AS DECIMAL(18, 2)) AS TaxAmt
    ) tax
    CROSS APPLY (
        SELECT CAST(tbl.Taxable + tax.TaxAmt AS DECIMAL(18, 2)) AS LineTotal
    ) lt
    WHERE n.N <= @PurchaseOrderCount
      AND m.N <= 6
      AND m.N <= ic.ItemCount;

    -- Roll line totals back up into the parent PurchaseOrder so
    -- TotalAmount_Amount is consistent with its line items.
    UPDATE po
    SET po.TotalAmount_Amount = agg.Total,
        po.UpdatedAt = GETUTCDATE()
    FROM PurchaseOrders po
    INNER JOIN (
        SELECT PurchaseOrderId, SUM(LineTotal) AS Total
        FROM PurchaseOrderItems
        GROUP BY PurchaseOrderId
    ) agg ON agg.PurchaseOrderId = po.Id
    WHERE po.TenantId = @TenantId
      AND po.PONumber LIKE 'PO-%';
END

-- ============================================================
-- PurchaseRequests (~100) — reorder suggestions against random products.
-- No TenantId / no deterministic Id on this table (see header note), so
-- idempotency is a simple COUNT guard rather than per-row NOT EXISTS.
-- ============================================================
IF (SELECT COUNT(*) FROM PurchaseRequests) < @PurchaseRequestCount
BEGIN
    INSERT INTO PurchaseRequests (
        ProductId, ProductName, CurrentQuantity, ReorderPoint, SuggestedQuantity,
        Status, CreatedAt, ResolvedAt
    )
    SELECT
        CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), p.ProductN), 12) AS UNIQUEIDENTIFIER),
        CASE p.ProductN % 10
            WHEN 0 THEN 'Premium' WHEN 1 THEN 'Classic' WHEN 2 THEN 'Deluxe' WHEN 3 THEN 'Standard'
            WHEN 4 THEN 'Compact' WHEN 5 THEN 'Industrial' WHEN 6 THEN 'Eco' WHEN 7 THEN 'Heavy-Duty'
            WHEN 8 THEN 'Portable' ELSE 'Professional'
        END + ' ' +
        CASE p.ProductN % 12
            WHEN 0 THEN 'Widget' WHEN 1 THEN 'Component' WHEN 2 THEN 'Fastener' WHEN 3 THEN 'Panel'
            WHEN 4 THEN 'Module' WHEN 5 THEN 'Bracket' WHEN 6 THEN 'Cable' WHEN 7 THEN 'Sensor'
            WHEN 8 THEN 'Valve' WHEN 9 THEN 'Bearing' WHEN 10 THEN 'Container' ELSE 'Adapter'
        END + ' #' + CAST(p.ProductN AS VARCHAR(10)),
        rq.CurrentQuantity,
        rq.ReorderPoint,
        rq.ReorderPoint * 2 - rq.CurrentQuantity,
        rq.StatusVal,
        rq.CreatedAt,
        CASE WHEN rq.StatusVal <> 0 THEN DATEADD(DAY, 1 + (rq.StatusRoll % 5), rq.CreatedAt) ELSE NULL END
    FROM #Numbers n
    CROSS APPLY (
        SELECT 1 + ABS(CHECKSUM(NEWID())) % 1000 AS ProductN
    ) p
    CROSS APPLY (
        SELECT
            ABS(CHECKSUM(NEWID())) % 40 AS CurrentQuantity,
            20 + ABS(CHECKSUM(NEWID())) % 80 AS ReorderPoint,
            ABS(CHECKSUM(NEWID())) % 100 AS StatusRoll,
            CAST(DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 90), GETUTCDATE()) AS DATETIME2) AS CreatedAt
    ) base
    CROSS APPLY (
        SELECT
            base.CurrentQuantity, base.ReorderPoint, base.StatusRoll, base.CreatedAt,
            CASE WHEN base.StatusRoll < 70 THEN 0 WHEN base.StatusRoll < 90 THEN 2 ELSE 1 END AS StatusVal
    ) rq
    WHERE n.N <= @PurchaseRequestCount;
END
GO
