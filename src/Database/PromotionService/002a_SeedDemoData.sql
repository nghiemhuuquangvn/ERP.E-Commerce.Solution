-- ============================================================
-- PromotionDB Migration 002 — Demo Seed Data
-- Seeds ~100 Promotions + ~200 Coupons for the cross-service demo-data
-- effort described in src/Database/SEED_DATA_CONVENTIONS.md.
--
-- GUID formula (mandatory, see conventions doc):
--   Promotions.Id       = C0000000-0001-0000-0000-{N}   N in [1,100]  (this service's own registry)
--   Coupons.Id          = C0000000-0002-0000-0000-{N}   N in [1,200]  (this service's own registry)
--   Coupons.PromotionId references Promotions.Id above (pseudo-random per row)
--   Promotions.ApplicableProductIds (JSON List<Guid>) references ProductService
--     Product: 50000000-0003-0000-0000-{N}, N in [1,1000]
--   Coupons.UsedByUserId (free-text NVARCHAR, not FK) references IdentityService
--     Customer User: 20000000-0002-0000-0000-{N}, N in [1,1000], for used coupons only
--
-- Schema surprises adapted to (verified against 001_CreatePromotionDB.sql,
-- PromotionService/002_FixPromotionEntityColumns.sql, and the real C# domain
-- model under src/services/PromotionService, not any earlier summary):
--   - Promotions.Type (INT NOT NULL, 001 comment "0=OrderDiscount,1=ProductDiscount,
--     2=Coupon,3=FlashSale,4=Loyalty") has NO corresponding property anywhere on
--     the Promotion entity or PromotionDbContext — it's an orphan column EF never
--     reads/writes. Still NOT NULL with no DEFAULT, so this script must supply a
--     value; derived from Scope (0 when Scope=Order, else 1) purely to keep the
--     column non-null and roughly sensible — the running app never looks at it.
--   - The real enums (ERP.PRM.Domain.Enums, PromotionEnums.cs) differ from the
--     001 column comments: DiscountType { Percentage=0, FixedAmount=1,
--     BuyXGetY=2, FreeShipping=3 } (001 comment says "0=Percentage,1=Fixed"
--     only), PromotionStatus { Draft=0, Active=1, Paused=2, Expired=3,
--     Cancelled=4 } (001 comment omits Cancelled=4). This script uses the
--     real enum values throughout.
--   - Promotions.Scope (added in PromotionService/002_FixPromotionEntityColumns.sql)
--     is PromotionScope { Order=0, Product=1, Category=2 } — drives which
--     ~40% of promotions (30% Product + 10% Category below) get
--     ApplicableProductIds / ApplicableCategoryIds populated.
--   - ApplicableProductIds/ApplicableCategoryIds are EF JSON-converted columns:
--     List<Guid> and List<string> respectively (NOT NULL, default '[]').
--     ApplicableCategoryIds holds plain category NAME strings (e.g.
--     "Electronics"), not GUIDs — there is no GUID formula for it in the
--     conventions doc, so this script reuses the same 8 top-level category
--     names as Product/011_SeedDemoData.sql for cross-service naming
--     consistency, not an actual GUID reference.
--   - Coupons is mapped as an EF OwnsMany table off Promotion (ToTable
--     "Coupons") but the Coupon entity itself has NO TenantId property even
--     though the DB column is NOT NULL — this script still populates it
--     (parent Promotion's TenantId) per the conventions doc's "always set
--     TenantId where the column exists" rule; EF itself would currently fail
--     to insert a Coupon for the same reason — an existing app bug, out of
--     scope for this seed script.
--
-- Idempotent: every INSERT is guarded by a row-count threshold plus a
-- per-row NOT EXISTS check on the deterministic Id, safe to re-run.
-- ============================================================

USE PromotionDB;
GO

SET NOCOUNT ON;
GO

DECLARE @TenantId       UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @PromotionCount INT = 100;
DECLARE @CouponCount    INT = 200;

-- ------------------------------------------------------------
-- Reusable numbers table (1..1000) via recursive CTE, per
-- SEED_DATA_CONVENTIONS.md guidance — drives every set-based generator
-- below instead of hand-written literal INSERT rows.
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
-- Promotions (100) — varied DiscountType (Percentage/FixedAmount/BuyXGetY/
-- FreeShipping), ~40% Product/Category scoped referencing real Products by
-- formula, dates spread from ~11 months ago to ~2 months ahead so the mix
-- covers Draft/Active/Paused/Expired/Cancelled realistically.
-- ============================================================
IF (SELECT COUNT(*) FROM Promotions WHERE TenantId = @TenantId) < @PromotionCount
BEGIN
    INSERT INTO Promotions (
        Id, TenantId, Name, Description, Type, DiscountType, Scope, DiscountValue,
        MinOrderAmount, MaxDiscountAmount, MaxUsage, MaxUsageTotal, MaxUsagePerUser,
        CurrentUsage, BuyQuantity, GetQuantity, StartDate, EndDate, Status,
        ApplicableProductIds, ApplicableCategoryIds, CreatedAt, UpdatedAt
    )
    SELECT
        idc.NewId,
        @TenantId,
        nm.PromoName,
        nm.PromoDesc,
        lgc.TypeVal,
        dtp.DiscountTypeVal,
        sco.ScopeVal,
        clc.DiscountValueVal,
        clc.MinOrderAmountVal,
        clc.MaxDiscountAmountVal,
        usg.MaxUsageTotalVal,
        usg.MaxUsageTotalVal,
        usg.MaxUsagePerUserVal,
        cur.CurrentUsageVal,
        bxg.BuyQty,
        bxg.GetQty,
        sd.StartDateVal,
        ed.EndDateVal,
        sts.StatusVal,
        pj.ProductJson,
        ctg.CatJson,
        tmc.CreatedAtVal,
        tm.UpdatedAtVal
    FROM #Numbers n
    CROSS APPLY (
        SELECT CAST('C0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER) AS NewId
    ) idc
    CROSS APPLY (
        SELECT
            n.N % 10 AS DiscRoll,
            n.N % 7  AS ScopeRoll,
            ABS(CHECKSUM(NEWID())) % 100 AS StatusRoll,
            ABS(CHECKSUM(NEWID())) % 390 - 330 AS StartOffsetDays, -- -330..+59 days from today
            7 + ABS(CHECKSUM(NEWID())) % 84 AS DurationRoll,       -- 7..90 day duration
            n.N % 5 AS MinOrderRoll,
            ABS(CHECKSUM(NEWID())) % 100 AS MaxUsageRoll,
            ABS(CHECKSUM(NEWID())) % 100 AS PerUserRoll,
            ABS(CHECKSUM(NEWID())) % 1000 AS DiscValRoll,
            ABS(CHECKSUM(NEWID())) AS ProdSeed1,
            ABS(CHECKSUM(NEWID())) AS ProdSeed2,
            n.N % 8 AS CatRoll
    ) r
    CROSS APPLY (
        SELECT CASE WHEN r.DiscRoll <= 3 THEN 0 WHEN r.DiscRoll <= 6 THEN 1 WHEN r.DiscRoll <= 8 THEN 2 ELSE 3 END AS DiscountTypeVal
    ) dtp
    CROSS APPLY (
        SELECT CASE WHEN r.ScopeRoll IN (0, 1) THEN 1 WHEN r.ScopeRoll = 2 THEN 2 ELSE 0 END AS ScopeVal
        -- ScopeRoll 0,1 (2/7 ~28.6%) -> Product; ScopeRoll=2 (1/7 ~14.3%) -> Category; rest (4/7 ~57%) -> Order
    ) sco
    CROSS APPLY (
        SELECT CASE WHEN sco.ScopeVal = 0 THEN 0 ELSE 1 END AS TypeVal
    ) lgc
    CROSS APPLY (
        SELECT CAST(DATEADD(DAY, r.StartOffsetDays, GETUTCDATE()) AS DATETIME2) AS StartDateVal
    ) sd
    CROSS APPLY (
        SELECT DATEADD(DAY, r.DurationRoll, sd.StartDateVal) AS EndDateVal
    ) ed
    CROSS APPLY (
        SELECT
            CASE
                WHEN sd.StartDateVal > GETUTCDATE() THEN CASE WHEN r.StatusRoll < 85 THEN 0 ELSE 2 END -- Draft / Paused
                WHEN ed.EndDateVal   < GETUTCDATE() THEN CASE WHEN r.StatusRoll < 80 THEN 3 ELSE 4 END -- Expired / Cancelled
                ELSE CASE WHEN r.StatusRoll < 80 THEN 1 WHEN r.StatusRoll < 93 THEN 2 ELSE 4 END        -- Active / Paused / Cancelled
            END AS StatusVal
    ) sts
    CROSS APPLY (
        SELECT
            CASE WHEN r.MaxUsageRoll < 20 THEN NULL ELSE 50 + r.MaxUsageRoll * 19 END AS MaxUsageTotalVal,
            CASE WHEN r.PerUserRoll  < 50 THEN NULL ELSE 1 + r.PerUserRoll % 5 END AS MaxUsagePerUserVal
    ) usg
    CROSS APPLY (
        SELECT
            CASE
                WHEN sts.StatusVal IN (1, 3, 4) AND usg.MaxUsageTotalVal IS NOT NULL THEN ABS(CHECKSUM(NEWID())) % (usg.MaxUsageTotalVal + 1)
                WHEN sts.StatusVal IN (1, 3, 4) THEN ABS(CHECKSUM(NEWID())) % 200
                ELSE 0
            END AS CurrentUsageVal
    ) cur
    CROSS APPLY (
        SELECT
            CASE r.MinOrderRoll WHEN 0 THEN 0 WHEN 1 THEN 20 WHEN 2 THEN 50 WHEN 3 THEN 100 ELSE 200 END AS MinOrderAmountVal,
            CAST(
                CASE dtp.DiscountTypeVal
                    WHEN 0 THEN 5 + r.DiscValRoll % 46                       -- Percentage: 5-50%
                    WHEN 1 THEN 5 + (r.DiscValRoll % 9600) / 100.0           -- FixedAmount: $5-$100
                    WHEN 2 THEN 0                                            -- BuyXGetY: DiscountValue unused by domain calc
                    ELSE 10 + (r.DiscValRoll % 3000) / 100.0                 -- FreeShipping: $10-$40 waived
                END AS DECIMAL(18, 2)
            ) AS DiscountValueVal,
            CASE
                WHEN dtp.DiscountTypeVal = 0 AND r.DiscValRoll % 3 <> 0
                    THEN CAST(20 + (r.DiscValRoll % 28000) / 100.0 AS DECIMAL(18, 2))
                ELSE NULL
            END AS MaxDiscountAmountVal
    ) clc
    CROSS APPLY (
        SELECT
            CASE WHEN dtp.DiscountTypeVal = 2 THEN 2 + r.DiscValRoll % 4 ELSE NULL END AS BuyQty,
            CASE WHEN dtp.DiscountTypeVal = 2 THEN 1 + r.DiscValRoll % 2 ELSE NULL END AS GetQty
    ) bxg
    CROSS APPLY (
        SELECT 1 + r.ProdSeed1 % 1000 AS ProdN1, 1 + r.ProdSeed2 % 1000 AS ProdN2
    ) pid
    CROSS APPLY (
        SELECT
            CASE WHEN sco.ScopeVal = 1 THEN
                '[' + '"' + CONVERT(VARCHAR(36), CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), pid.ProdN1), 12) AS UNIQUEIDENTIFIER)) + '","' +
                      CONVERT(VARCHAR(36), CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), pid.ProdN2), 12) AS UNIQUEIDENTIFIER)) + '"]'
            ELSE '[]' END AS ProductJson
    ) pj
    CROSS APPLY (
        SELECT
            CASE WHEN sco.ScopeVal = 2 THEN
                '["' + (CASE r.CatRoll
                    WHEN 0 THEN 'Electronics' WHEN 1 THEN 'Fashion' WHEN 2 THEN 'Home & Living'
                    WHEN 3 THEN 'Sports & Outdoors' WHEN 4 THEN 'Beauty & Health' WHEN 5 THEN 'Groceries'
                    WHEN 6 THEN 'Toys & Games' ELSE 'Automotive' END) + '"]'
            ELSE '[]' END AS CatJson
    ) ctg
    CROSS APPLY (
        SELECT
            CASE (n.N - 1) % 20
                WHEN 0 THEN N'Tet Holiday Sale' WHEN 1 THEN N'Summer Splash Sale' WHEN 2 THEN N'Back to School Bonanza'
                WHEN 3 THEN N'Black Friday Mega Deal' WHEN 4 THEN N'Cyber Monday Special' WHEN 5 THEN N'Christmas Countdown'
                WHEN 6 THEN N'New Year Kickoff' WHEN 7 THEN N'Valentine''s Day Specials' WHEN 8 THEN N'Women''s Day Treat'
                WHEN 9 THEN N'Spring Refresh Sale' WHEN 10 THEN N'Mid-Year Clearance' WHEN 11 THEN N'Anniversary Celebration'
                WHEN 12 THEN N'Flash Weekend Deal' WHEN 13 THEN N'Payday Deals' WHEN 14 THEN N'Independence Day Sale'
                WHEN 15 THEN N'Halloween Frights Sale' WHEN 16 THEN N'Mother''s Day Gifts' WHEN 17 THEN N'Father''s Day Picks'
                WHEN 18 THEN N'9.9 Mega Sale' ELSE N'12.12 Grand Finale'
            END AS EventName,
            CASE (n.N - 1) / 20
                WHEN 0 THEN N'Percentage Off' WHEN 1 THEN N'Cash Discount' WHEN 2 THEN N'Buy More Save More'
                WHEN 3 THEN N'Free Shipping Bonus' ELSE N'Member Exclusive'
            END AS SuffixName
    ) nmr
    CROSS APPLY (
        SELECT
            nmr.EventName + N' - ' + nmr.SuffixName AS PromoName,
            N'Limited-time ' + nmr.EventName + N' promotion (' + nmr.SuffixName + N'). Terms apply while supplies last.' AS PromoDesc
    ) nm
    CROSS APPLY (
        SELECT DATEADD(DAY, -3, sd.StartDateVal) AS CreatedAtVal
    ) tmc
    CROSS APPLY (
        SELECT CASE WHEN sts.StatusVal <> 0 THEN DATEADD(HOUR, 6, tmc.CreatedAtVal) ELSE tmc.CreatedAtVal END AS UpdatedAtVal
    ) tm
    WHERE n.N <= @PromotionCount
      AND NOT EXISTS (SELECT 1 FROM Promotions x WHERE x.Id = idc.NewId);
END
GO

-- ============================================================
-- Coupons (200) — pseudo-random Promotion per coupon (so popular promotions
-- naturally get more coupons than others), realistic codes, ~1/3 of coupons
-- against Active/Expired promotions marked used with a redeeming customer.
-- ============================================================
DECLARE @TenantId2 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @CouponCount2 INT = 200;
DECLARE @PromotionCount2 INT = 100;

IF (SELECT COUNT(*) FROM Coupons WHERE TenantId = @TenantId2) < @CouponCount2
BEGIN
    -- Materialized into a real temp table (not a CTE) so PromotionN's
    -- NEWID()-based value is computed exactly once per row — a CTE column
    -- built from a non-deterministic function can otherwise be re-evaluated
    -- by the optimizer each time it's referenced downstream, which was
    -- producing duplicate NewId values (and PK violations) here.
    IF OBJECT_ID('tempdb..#CouponSrc') IS NOT NULL DROP TABLE #CouponSrc;
    SELECT
        n.N,
        CAST('C0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER) AS NewId,
        1 + ABS(CHECKSUM(NEWID())) % @PromotionCount2 AS PromotionN
    INTO #CouponSrc
    FROM #Numbers n
    WHERE n.N <= @CouponCount2;

    INSERT INTO Coupons (Id, TenantId, PromotionId, Code, IsUsed, UsedByUserId, UsedAt, CreatedAt)
    SELECT
        cs.NewId,
        p.TenantId,
        p.Id,
        cd.Code,
        base.IsUsedFlag,
        CASE WHEN base.IsUsedFlag = 1 THEN base.UsedByUserId ELSE NULL END,
        CASE WHEN base.IsUsedFlag = 1 THEN ua.UsedAtClamped ELSE NULL END,
        base.CreatedAtVal
    FROM #CouponSrc cs
    INNER JOIN Promotions p
        ON p.Id = CAST('C0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), cs.PromotionN), 12) AS UNIQUEIDENTIFIER)
       AND p.TenantId = @TenantId2
    CROSS APPLY (
        SELECT CASE cs.N % 15
            WHEN 0 THEN 'SAVE10' WHEN 1 THEN 'WELCOME' WHEN 2 THEN 'FLASH' WHEN 3 THEN 'VIP'
            WHEN 4 THEN 'LOYAL' WHEN 5 THEN 'HOLIDAY' WHEN 6 THEN 'SPRING' WHEN 7 THEN 'SUMMER'
            WHEN 8 THEN 'FALLSALE' WHEN 9 THEN 'WINTER' WHEN 10 THEN 'NEWYEAR' WHEN 11 THEN 'TET'
            WHEN 12 THEN 'BLACKFRIDAY' WHEN 13 THEN 'CYBERMON' ELSE 'FREESHIP'
        END + '-' + RIGHT('00000' + CONVERT(VARCHAR(5), cs.N), 5) AS Code
    ) cd
    CROSS APPLY (
        SELECT
            CASE WHEN p.Status IN (1, 3) AND cs.N % 3 = 0 THEN 1 ELSE 0 END AS IsUsedFlag,
            CONVERT(VARCHAR(100), CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), 1 + ABS(CHECKSUM(NEWID())) % 1000), 12) AS UNIQUEIDENTIFIER)) AS UsedByUserId,
            DATEADD(DAY, ABS(CHECKSUM(NEWID())) % (CASE WHEN DATEDIFF(DAY, p.StartDate, p.EndDate) < 1 THEN 1 ELSE DATEDIFF(DAY, p.StartDate, p.EndDate) END), p.StartDate) AS UsedAtRaw,
            DATEADD(HOUR, -1 * (ABS(CHECKSUM(NEWID())) % 48), p.StartDate) AS CreatedAtVal
    ) base
    CROSS APPLY (
        SELECT CASE WHEN base.UsedAtRaw > GETUTCDATE() THEN GETUTCDATE() ELSE base.UsedAtRaw END AS UsedAtClamped
    ) ua
    WHERE NOT EXISTS (SELECT 1 FROM Coupons c WHERE c.Id = cs.NewId);
END
GO
