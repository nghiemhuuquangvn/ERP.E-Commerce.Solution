-- ============================================================
-- SalesDB Migration 010 — Demo data seed (SalesOrders, SalesOrderItems,
-- CustomerSalesProfiles).
--
-- Follows src/Database/SEED_DATA_CONVENTIONS.md deterministic GUID formula:
--   {PrefixHigh}-{PrefixLow}-0000-0000-{N padded to 12 hex digits}
-- Cross-service references used here (rows do not need to exist yet in the
-- owning service's DB at migration time):
--   CustomerId    -> Identity  Customer User : 20000000-0002-0000-0000-{N}, N in [1,1000]
--   SalesRepId    -> MasterData Employee     : 40000000-0009-0000-0000-{N}, N in [1,1000]
--   DepartmentId  -> MasterData Department   : 40000000-0007-0000-0000-{N}, N in [1,15]
--   ProductId     -> Product   Product       : 50000000-0003-0000-0000-{N}, N in [1,1000]
--   PromotionId   -> Promotion Promotion      : C0000000-0001-0000-0000-{N}, N in [1,100]  (~20% of orders)
-- SalesOrder itself is registered as 70000000-0001-0000-0000-{N}.
-- SalesOrderItem.Id stays NEWID() — local-only, never cross-referenced.
--
-- Idempotent: the whole block is skipped once SalesOrders already holds
-- >= 1000 rows, so re-running this script (e.g. on every docker-compose
-- db-migrate run) is a no-op after the first successful seed.
-- ============================================================

USE SalesDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;

IF (SELECT COUNT(*) FROM SalesOrders) < 1000
BEGIN
    DECLARE @TenantId   UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
    DECLARE @OrderCount INT = 1200;

    IF OBJECT_ID('tempdb..#Orders') IS NOT NULL DROP TABLE #Orders;
    IF OBJECT_ID('tempdb..#ItemsBase') IS NOT NULL DROP TABLE #ItemsBase;

    -- ------------------------------------------------------------
    -- 1) Generate #Orders (one row per SalesOrder to be seeded)
    -- ------------------------------------------------------------
    ;WITH Numbers AS (
        SELECT 1 AS n
        UNION ALL
        SELECT n + 1 FROM Numbers WHERE n < @OrderCount
    ),
    Base AS (
        SELECT
            n,
            CustomerN  = 1 + ABS(CHECKSUM(NEWID())) % 1000,
            SalesRepN  = 1 + ABS(CHECKSUM(NEWID())) % 1000,
            DeptN      = 1 + ABS(CHECKSUM(NEWID())) % 15,
            DaysAgo    = ABS(CHECKSUM(NEWID())) % 365,
            SecondsAgo = ABS(CHECKSUM(NEWID())) % 86400,
            StatusRoll = ABS(CHECKSUM(NEWID())) % 100,
            SourceRoll = ABS(CHECKSUM(NEWID())) % 100,
            PromoRoll  = ABS(CHECKSUM(NEWID())) % 100,
            PromoN     = 1 + ABS(CHECKSUM(NEWID())) % 100,
            ItemCount  = 1 + ABS(CHECKSUM(NEWID())) % 4,
            CityRoll   = ABS(CHECKSUM(NEWID())) % 6,
            StreetNo   = 1 + ABS(CHECKSUM(NEWID())) % 998,
            PostalRoll = 100000 + ABS(CHECKSUM(NEWID())) % 899999
        FROM Numbers
    ),
    WithDate AS (
        SELECT *,
            OrderDate = DATEADD(SECOND, -SecondsAgo, DATEADD(DAY, -DaysAgo, SYSUTCDATETIME())),
            -- OrderStatus enum: 0=Draft,1=Submitted,2=Approved,3=Picked,4=Shipped,5=Delivered,6=Cancelled,7=Rejected
            Status = CASE
                        WHEN StatusRoll < 5  THEN 0
                        WHEN StatusRoll < 15 THEN 1
                        WHEN StatusRoll < 25 THEN 2
                        WHEN StatusRoll < 30 THEN 3
                        WHEN StatusRoll < 45 THEN 4
                        WHEN StatusRoll < 90 THEN 5
                        WHEN StatusRoll < 98 THEN 6
                        ELSE 7
                     END,
            -- OrderSource: 0=Manual, 1=Ecommerce, 2=POS
            OrderSource = CASE WHEN SourceRoll < 30 THEN 0 WHEN SourceRoll < 90 THEN 1 ELSE 2 END
        FROM Base
    ),
    WithApproved AS (
        SELECT *,
            ApprovedAt = CASE WHEN Status IN (2,3,4,5)
                              THEN DATEADD(HOUR, 1 + ABS(CHECKSUM(NEWID())) % 48, OrderDate)
                              ELSE NULL END
        FROM WithDate
    ),
    WithShipped AS (
        SELECT *,
            ShippedAt = CASE WHEN Status IN (4,5)
                             THEN DATEADD(HOUR, 1 + ABS(CHECKSUM(NEWID())) % 72, ApprovedAt)
                             ELSE NULL END
        FROM WithApproved
    ),
    WithDelivered AS (
        SELECT *,
            DeliveredAt = CASE WHEN Status = 5
                               THEN DATEADD(HOUR, 4 + ABS(CHECKSUM(NEWID())) % 96, ShippedAt)
                               ELSE NULL END
        FROM WithShipped
    ),
    WithCancelled AS (
        SELECT *,
            CancelledAt = CASE WHEN Status = 6
                               THEN DATEADD(HOUR, 1 + ABS(CHECKSUM(NEWID())) % 48, OrderDate)
                               ELSE NULL END
        FROM WithDelivered
    )
    SELECT
        OrderId      = CAST('70000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n), 12) AS UNIQUEIDENTIFIER),
        n,
        CustomerId   = CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), CustomerN), 12) AS UNIQUEIDENTIFIER),
        CustomerName = 'Customer ' + CONVERT(VARCHAR(12), CustomerN),
        CustomerEmail= 'customer' + CONVERT(VARCHAR(12), CustomerN) + '@example.com',
        SalesRepId   = CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), SalesRepN), 12) AS UNIQUEIDENTIFIER),
        DepartmentId = CAST('40000000-0007-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), DeptN), 12) AS UNIQUEIDENTIFIER),
        PromotionId  = CASE WHEN PromoRoll < 20
                            THEN CAST('C0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), PromoN), 12) AS UNIQUEIDENTIFIER)
                            ELSE NULL END,
        OrderDate, Status, OrderSource, ApprovedAt, ShippedAt, DeliveredAt, CancelledAt,
        ItemCount,
        OrderNo = 'SO-' + RIGHT('0000000' + CONVERT(VARCHAR(7), n), 7),
        City = CASE CityRoll
                    WHEN 0 THEN 'Hanoi'
                    WHEN 1 THEN 'Ho Chi Minh City'
                    WHEN 2 THEN 'Da Nang'
                    WHEN 3 THEN 'Hai Phong'
                    WHEN 4 THEN 'Can Tho'
                    ELSE 'Nha Trang'
               END,
        StreetNo, PostalRoll
    INTO #Orders
    FROM WithCancelled
    OPTION (MAXRECURSION 0);

    ALTER TABLE #Orders ADD
        CancellationReason NVARCHAR(500) NULL,
        TrackingNumber     NVARCHAR(200) NULL,
        Carrier            NVARCHAR(200) NULL,
        ShippingStreet     NVARCHAR(255) NULL;

    UPDATE #Orders SET
        CancellationReason = CASE WHEN Status IN (6,7) THEN
                (CASE ABS(CHECKSUM(NEWID())) % 4
                    WHEN 0 THEN 'Customer requested cancellation'
                    WHEN 1 THEN 'Item out of stock'
                    WHEN 2 THEN 'Payment failed'
                    ELSE 'Duplicate order'
                 END)
            ELSE NULL END,
        TrackingNumber = CASE WHEN Status IN (4,5)
                              THEN 'TRK' + RIGHT('00000000' + CONVERT(VARCHAR(8), ABS(CHECKSUM(NEWID())) % 100000000), 8)
                              ELSE NULL END,
        Carrier = CASE WHEN Status IN (4,5) THEN
                (CASE ABS(CHECKSUM(NEWID())) % 3
                    WHEN 0 THEN 'GHN'
                    WHEN 1 THEN 'GHTK'
                    ELSE 'ViettelPost'
                 END)
            ELSE NULL END,
        ShippingStreet = CONVERT(VARCHAR(6), StreetNo) + ' Main Street';

    INSERT INTO SalesOrders (
        Id, TenantId, CustomerId, CustomerName, CustomerEmail, OrderDate, Status, TotalAmount,
        ShippingAddress_Street, ShippingAddress_City, ShippingAddress_State, ShippingAddress_PostalCode, ShippingAddress_Country,
        Notes, PromotionId, DiscountAmount, ApprovedAt, ApprovedBy, ShippedAt, DeliveredAt, CancelledAt, CancellationReason,
        CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted,
        TrackingNumber, Carrier, HasPriceChangeWarning,
        OrderNo, OrderSource, DepartmentId, SalesRepId, QuotationId
    )
    SELECT
        o.OrderId, @TenantId, o.CustomerId, o.CustomerName, o.CustomerEmail, o.OrderDate, o.Status, 0,
        o.ShippingStreet, o.City, o.City, CONVERT(VARCHAR(10), o.PostalRoll), 'Vietnam',
        NULL, o.PromotionId, 0, o.ApprovedAt, CASE WHEN o.ApprovedAt IS NOT NULL THEN 'sales.manager@example.com' ELSE NULL END,
        o.ShippedAt, o.DeliveredAt, o.CancelledAt, o.CancellationReason,
        'seed-script', o.OrderDate, NULL, ISNULL(o.DeliveredAt, ISNULL(o.CancelledAt, ISNULL(o.ShippedAt, ISNULL(o.ApprovedAt, o.OrderDate)))), 0,
        o.TrackingNumber, o.Carrier, 0,
        o.OrderNo, o.OrderSource, o.DepartmentId, o.SalesRepId, NULL
    FROM #Orders o
    WHERE NOT EXISTS (SELECT 1 FROM SalesOrders so WHERE so.Id = o.OrderId);

    -- ------------------------------------------------------------
    -- 2) Generate #ItemsBase (1-4 rows per order)
    -- ------------------------------------------------------------
    ;WITH Slots AS (
        SELECT 1 AS s UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
    )
    SELECT
        Id          = NEWID(),
        OrderId     = o.OrderId,
        OrderStatus = o.Status,
        ProductN    = 1 + ABS(CHECKSUM(NEWID())) % 1000,
        Quantity    = 1 + ABS(CHECKSUM(NEWID())) % 5,
        PriceDollars= 5 + ABS(CHECKSUM(NEWID())) % 496,
        PriceCents  = ABS(CHECKSUM(NEWID())) % 100
    INTO #ItemsBase
    FROM #Orders o
    JOIN Slots sl ON sl.s <= o.ItemCount;

    ALTER TABLE #ItemsBase ADD
        ProductId   UNIQUEIDENTIFIER NULL,
        UnitPrice   DECIMAL(18,2)    NULL,
        TaxAmount   DECIMAL(18,2)    NULL,
        ProductName NVARCHAR(500)    NULL,
        ProductSku  NVARCHAR(100)    NULL;

    UPDATE #ItemsBase SET
        ProductId  = CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ProductN), 12) AS UNIQUEIDENTIFIER),
        UnitPrice  = CAST(PriceDollars + PriceCents / 100.0 AS DECIMAL(18,2)),
        ProductSku = 'PRD-' + RIGHT('000000' + CONVERT(VARCHAR(6), ProductN), 6);

    UPDATE #ItemsBase SET
        TaxAmount = CAST(Quantity * UnitPrice * 0.10 AS DECIMAL(18,2));

    ;WITH Adj(i, w) AS (
        SELECT * FROM (VALUES
            (0,'Premium'),(1,'Deluxe'),(2,'Compact'),(3,'Wireless'),(4,'Classic'),(5,'Eco'),
            (6,'Smart'),(7,'Portable'),(8,'Heavy-Duty'),(9,'Professional'),(10,'Ultra'),(11,'Essential')
        ) v(i, w)
    ),
    Noun(i, w) AS (
        SELECT * FROM (VALUES
            (0,'Mouse'),(1,'Keyboard'),(2,'Office Chair'),(3,'Desk Lamp'),(4,'Backpack'),(5,'Monitor'),
            (6,'Speaker'),(7,'Water Bottle'),(8,'Notebook'),(9,'Headphones'),(10,'Charger'),(11,'Webcam'),
            (12,'Desk Organizer'),(13,'Travel Mug'),(14,'Phone Case'),(15,'Laptop Sleeve'),(16,'Laptop Stand'),
            (17,'Mouse Pad'),(18,'Bluetooth Speaker'),(19,'USB Hub')
        ) v(i, w)
    )
    UPDATE ib SET ProductName = a.w + ' ' + nn.w
    FROM #ItemsBase ib
    JOIN Adj a  ON a.i = ib.ProductN % 12
    JOIN Noun nn ON nn.i = (ib.ProductN / 12) % 20;

    INSERT INTO SalesOrderItems (
        Id, OrderId, TenantId, ProductId, ProductName, ProductSku, Quantity, UnitPrice,
        DiscountAmount, TaxRate, TaxAmount, ShippedQuantity
    )
    SELECT
        ib.Id, ib.OrderId, @TenantId, ib.ProductId, ib.ProductName, ib.ProductSku, ib.Quantity, ib.UnitPrice,
        0, 10.00, ib.TaxAmount, CASE WHEN ib.OrderStatus IN (4,5) THEN ib.Quantity ELSE 0 END
    FROM #ItemsBase ib
    WHERE NOT EXISTS (SELECT 1 FROM SalesOrderItems soi WHERE soi.OrderId = ib.OrderId);

    -- ------------------------------------------------------------
    -- 3) Roll up SalesOrders.TotalAmount = SUM(item LineTotal)
    -- ------------------------------------------------------------
    UPDATE so
        SET TotalAmount = agg.Total
    FROM SalesOrders so
    JOIN #Orders o ON o.OrderId = so.Id
    JOIN (
        SELECT OrderId, Total = SUM(LineTotal) FROM SalesOrderItems GROUP BY OrderId
    ) agg ON agg.OrderId = so.Id;

    -- ------------------------------------------------------------
    -- 4) CustomerSalesProfiles for every distinct customer used above
    -- ------------------------------------------------------------
    INSERT INTO CustomerSalesProfiles (Id, CustomerId, CreditLimit, CurrentBalance, UpdatedAt)
    SELECT
        NEWID(), c.CustomerId, cl.CreditLimit, cb.CurrentBalance, SYSUTCDATETIME()
    FROM (SELECT DISTINCT CustomerId FROM #Orders) c
    CROSS APPLY (SELECT CreditLimit = CAST(1000 + ABS(CHECKSUM(NEWID())) % 19000 AS DECIMAL(18,2))) cl
    CROSS APPLY (SELECT CurrentBalance = CAST((ABS(CHECKSUM(NEWID())) % 100) / 100.0 * cl.CreditLimit AS DECIMAL(18,2))) cb
    WHERE NOT EXISTS (SELECT 1 FROM CustomerSalesProfiles p WHERE p.CustomerId = c.CustomerId);

    DROP TABLE #Orders;
    DROP TABLE #ItemsBase;

    PRINT 'SalesDB demo data seeded: SalesOrders/SalesOrderItems/CustomerSalesProfiles.';
END
ELSE
BEGIN
    PRINT 'SalesDB demo data seed skipped: SalesOrders already has >= 1000 rows.';
END
GO
