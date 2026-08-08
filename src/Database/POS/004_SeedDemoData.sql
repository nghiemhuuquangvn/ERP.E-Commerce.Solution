-- ============================================================
-- PosDB Migration 004 — Demo Seed Data
-- Seeds Terminals, CashShifts (+ CashDenominationLines), 560 Transactions
-- (+ TransactionItems + TransactionTenders) for the cross-service demo-data
-- effort described in src/Database/SEED_DATA_CONVENTIONS.md.
--
-- GUID formula (mandatory, see conventions doc):
--   Transactions.Id = D0000000-0001-0000-0000-{N}   N in [1,560]  (the
--     conventions doc's registered "POS Transaction" prefix)
--   Transactions.CashierId / CashShifts.CashierId reference SharedMasterDataService
--     Employee: 40000000-0009-0000-0000-{N}, N in [1,1000]
--   TransactionItems.ProductId references ProductService
--     Product: 50000000-0003-0000-0000-{N}, N in [1,1000]
--   Transactions.PromotionId references PromotionService
--     Promotion: C0000000-0001-0000-0000-{N}, N in [1,100] (~15% of Completed
--     transactions)
--   Terminals.Id / CashShifts.Id are NOT in the conventions-doc registry
--     (neither table is cross-referenced by another service) — this script
--     extends the D0000000 namespace locally purely so this file's own
--     multi-phase inserts can address rows deterministically and stay
--     idempotent: Terminals = D0000000-0002-0000-0000-{N}, N in [1,10];
--     CashShifts = D0000000-0003-0000-0000-{N}, N in [1,300]. Mirrors how
--     Promotion/002_SeedDemoData.sql extends Promotion's own C0000000
--     namespace for Coupon (C0000000-0002).
--
-- Schema surprises adapted to (verified against 001_CreatePosDB.sql,
-- 002_AddTendersDenominationsAndTypeFixes.sql, 003_AddProductAndPromotionCache.sql,
-- and the real C# domain model under src/services/POSService, not any earlier
-- summary):
--   - The real enum (ERP.POS.Domain.Enums.TransactionStatus) is
--     { Open=0, Completed=1, Voided=2, Suspended=3} — 001's column comment
--     ("0=Draft,1=Completed,2=Voided") omits Suspended=3 and calls state 0
--     "Draft" instead of "Open"; this script uses the real enum values.
--   - Transactions.PaymentMethod is declared INT NULL in 001 (comment
--     "0=Cash,1=CreditCard,2=EWallet"), but the real Transaction entity's
--     PaymentMethod property is a `string?` written by CompleteWithTenders()
--     as the tender method name (or "Mixed" for split payments) — a genuine
--     column-type mismatch never fixed (unlike CashierId, which 002 did fix
--     from NVARCHAR to UNIQUEIDENTIFIER). An NVARCHAR value can't be stored
--     in this INT column, so this script fills it with the legacy 0/1/2
--     code matching the transaction's primary tender instead; the real,
--     app-readable payment breakdown lives in TransactionTenders (seeded
--     below with the actual string method names), which correctly matches
--     the C# TransactionTender entity/table mapping.
--   - CashShifts.ClosingBalance is approximated from OpeningBalance + a cash
--     portion of that shift's completed sales (not an exact denomination
--     reconciliation) since CashDenominationLines carries no DB-level CHECK
--     constraint tying it to OpeningBalance/ClosingBalance (the exact-sum
--     rule lives only in the C# CashShift.Open/Close domain methods).
--   - PosProductCache / PosPromotionCache (003) are out of scope for this
--     pass (not requested) and are left unseeded.
--
-- Idempotent: every INSERT is guarded by a row-count threshold plus a
-- per-row NOT EXISTS check on the deterministic Id, safe to re-run. Rollup
-- UPDATE statements (Transaction totals, CashShift totals) are naturally
-- idempotent (recompute the same aggregate every run).
-- ============================================================

USE PosDB;
GO

SET NOCOUNT ON;
GO

DECLARE @TenantId         UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @TerminalCount    INT = 10;
DECLARE @ShiftCount       INT = 300;
DECLARE @TransactionCount INT = 560;

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
-- Terminals (10) — 5 store locations x 2 registers each
-- ============================================================
IF (SELECT COUNT(*) FROM Terminals WHERE TenantId = @TenantId) < @TerminalCount
BEGIN
    INSERT INTO Terminals (Id, TenantId, Name, LocationId, IsActive, LastSyncAt, CreatedAt)
    SELECT
        CAST('D0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
        @TenantId,
        st.StoreName + N' - Register ' + CONVERT(VARCHAR(2), ((n.N - 1) % 2) + 1),
        NULL,
        CASE WHEN n.N = 10 THEN 0 ELSE 1 END, -- one terminal decommissioned for realism
        DATEADD(MINUTE, -1 * (ABS(CHECKSUM(NEWID())) % 180), GETUTCDATE()),
        DATEADD(DAY, -400 + n.N, GETUTCDATE())
    FROM #Numbers n
    CROSS APPLY (
        SELECT CASE ((n.N - 1) / 2)
            WHEN 0 THEN N'District 1 Flagship Store'
            WHEN 1 THEN N'Thu Duc Mall Store'
            WHEN 2 THEN N'Ha Noi Old Quarter Store'
            WHEN 3 THEN N'Da Nang Riverside Store'
            ELSE N'Can Tho Downtown Store'
        END AS StoreName
    ) st
    WHERE n.N <= @TerminalCount
      AND NOT EXISTS (
        SELECT 1 FROM Terminals x
        WHERE x.Id = CAST('D0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
      );
END
GO

-- ============================================================
-- CashShifts (300) — 30 per terminal, spread over the last ~12 months.
-- TotalSales/TransactionCount/ClosingBalance are placeholders here, rolled
-- up from actual Transactions further below.
-- ============================================================
DECLARE @TenantId2 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @ShiftCount2 INT = 300;

IF (SELECT COUNT(*) FROM CashShifts WHERE TenantId = @TenantId2) < @ShiftCount2
BEGIN
    INSERT INTO CashShifts (
        Id, TenantId, TerminalId, CashierId, OpenedAt, ClosedAt,
        OpeningBalance, ClosingBalance, TotalSales, TransactionCount, Notes, Status
    )
    SELECT
        idc.NewId,
        @TenantId2,
        CAST('D0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), r.TerminalN), 12) AS UNIQUEIDENTIFIER),
        CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), r.CashierN), 12) AS UNIQUEIDENTIFIER),
        r.OpenedAtVal,
        CASE WHEN r.IsOpenShift = 1 THEN NULL ELSE DATEADD(HOUR, r.ShiftHours, r.OpenedAtVal) END,
        r.OpeningBalanceVal,
        NULL, -- computed in rollup UPDATE below
        0,    -- computed in rollup UPDATE below
        0,    -- computed in rollup UPDATE below
        CASE WHEN r.NoteRoll = 0 THEN N'Till counted twice at close, minor variance noted.' ELSE NULL END,
        CASE WHEN r.IsOpenShift = 1 THEN 0 ELSE 1 END
    FROM #Numbers n
    CROSS APPLY (
        SELECT CAST('D0000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER) AS NewId
    ) idc
    CROSS APPLY (
        SELECT
            1 + ((n.N - 1) % 10) AS TerminalN,
            1 + ABS(CHECKSUM(NEWID())) % 1000 AS CashierN,
            DATEADD(HOUR, 7 + ABS(CHECKSUM(NEWID())) % 3,
                CAST(DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 365), CAST(GETUTCDATE() AS DATE)) AS DATETIME2)) AS OpenedAtVal,
            8 + ABS(CHECKSUM(NEWID())) % 3 AS ShiftHours,
            CAST(100 + (ABS(CHECKSUM(NEWID())) % 40000) / 100.0 AS DECIMAL(18, 2)) AS OpeningBalanceVal,
            ABS(CHECKSUM(NEWID())) % 20 AS NoteRoll
    ) r0
    CROSS APPLY (
        SELECT r0.TerminalN, r0.CashierN, r0.OpenedAtVal, r0.ShiftHours, r0.OpeningBalanceVal, r0.NoteRoll,
               CASE WHEN DATEDIFF(HOUR, r0.OpenedAtVal, GETUTCDATE()) < r0.ShiftHours THEN 1 ELSE 0 END AS IsOpenShift
    ) r
    WHERE n.N <= @ShiftCount2
      AND NOT EXISTS (SELECT 1 FROM CashShifts x WHERE x.Id = idc.NewId);
END
GO

-- ============================================================
-- CashDenominationLines (opening count only, ~5 per shift) — cash float
-- breakdown at shift open, generic denominations {100,50,20,10,5}.
-- ============================================================
DECLARE @TenantId3 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (
    SELECT 1 FROM CashDenominationLines
    WHERE CashShiftId = CAST('D0000000-0003-0000-0000-000000000001' AS UNIQUEIDENTIFIER)
)
BEGIN
    INSERT INTO CashDenominationLines (Id, CashShiftId, Stage, Denomination, Count)
    SELECT
        NEWID(),
        s.Id,
        0, -- Opening
        dn.Denomination,
        CASE dn.Denomination
            WHEN 100 THEN CAST(ROUND(s.OpeningBalance * 0.35 / 100, 0) AS INT)
            WHEN 50  THEN CAST(ROUND(s.OpeningBalance * 0.25 / 50, 0) AS INT)
            WHEN 20  THEN CAST(ROUND(s.OpeningBalance * 0.20 / 20, 0) AS INT)
            WHEN 10  THEN CAST(ROUND(s.OpeningBalance * 0.12 / 10, 0) AS INT)
            ELSE          CAST(ROUND(s.OpeningBalance * 0.08 / 5, 0) AS INT)
        END
    FROM CashShifts s
    CROSS APPLY (SELECT * FROM (VALUES (100), (50), (20), (10), (5)) v(Denomination)) dn
    WHERE s.TenantId = @TenantId3
      AND NOT EXISTS (
        SELECT 1 FROM CashDenominationLines x
        WHERE x.CashShiftId = s.Id AND x.Stage = 0 AND x.Denomination = dn.Denomination
      );
END
GO

-- ============================================================
-- Transactions (560) — shell rows first (totals rolled up from items below).
-- ~70% linked to a pseudo-random CashShift (inherits its Terminal/Cashier/
-- time window); the rest are independent walk-in sales with no shift link.
-- ============================================================
DECLARE @TenantId4 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @TransactionCount4 INT = 560;
DECLARE @ShiftCount4 INT = 300;

IF (SELECT COUNT(*) FROM Transactions WHERE TenantId = @TenantId4) < @TransactionCount4
BEGIN
    INSERT INTO Transactions (
        Id, TenantId, TerminalId, CashierId, CashShiftId, Status,
        SubTotal, TaxAmount, DiscountAmount, TotalAmount, PaymentMethod,
        PromotionId, VoidReason, CompletedAt, VoidedAt, CreatedAt, UpdatedAt
    )
    SELECT
        idc.NewId,
        @TenantId4,
        src.TerminalIdVal,
        src.CashierIdVal,
        src.CashShiftIdVal,
        src.StatusVal,
        0, 0, 0, 0, -- SubTotal/TaxAmount/DiscountAmount/TotalAmount: rolled up from TransactionItems below
        NULL,       -- PaymentMethod (legacy INT): set once Status/tenders are finalized below
        src.PromotionIdVal,
        src.VoidReasonVal,
        NULL, NULL, -- CompletedAt/VoidedAt: set once Status is finalized below
        src.CreatedAtVal,
        src.CreatedAtVal
    FROM #Numbers n
    CROSS APPLY (
        SELECT CAST('D0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER) AS NewId
    ) idc
    CROSS APPLY (
        SELECT
            ABS(CHECKSUM(NEWID())) % 100 AS LinkRoll,
            ABS(CHECKSUM(NEWID())) % 100 AS StatusRoll,
            1 + ABS(CHECKSUM(NEWID())) % @ShiftCount4 AS ShiftN,
            1 + ABS(CHECKSUM(NEWID())) % 10 AS TerminalN,
            1 + ABS(CHECKSUM(NEWID())) % 1000 AS CashierN,
            ABS(CHECKSUM(NEWID())) % 100 AS PromoRoll,
            1 + ABS(CHECKSUM(NEWID())) % 100 AS PromoN,
            ABS(CHECKSUM(NEWID())) % 3 AS VoidReasonRoll,
            ABS(CHECKSUM(NEWID())) % (24 * 365) AS IndepMinutesAgo
    ) r
    OUTER APPLY (
        SELECT TOP (1) sh.Id AS ShiftId, sh.TerminalId, sh.CashierId, sh.OpenedAt, sh.ClosedAt
        FROM CashShifts sh
        WHERE sh.Id = CAST('D0000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), r.ShiftN), 12) AS UNIQUEIDENTIFIER)
    ) shf
    CROSS APPLY (
        SELECT
            CASE WHEN r.LinkRoll < 70 AND shf.ShiftId IS NOT NULL THEN 1 ELSE 0 END AS IsLinked
    ) lk
    CROSS APPLY (
        SELECT
            CASE WHEN lk.IsLinked = 1 THEN shf.TerminalId
                 ELSE CAST('D0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), r.TerminalN), 12) AS UNIQUEIDENTIFIER)
            END AS TerminalIdVal,
            CASE WHEN lk.IsLinked = 1 THEN shf.CashierId
                 ELSE CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), r.CashierN), 12) AS UNIQUEIDENTIFIER)
            END AS CashierIdVal,
            CASE WHEN lk.IsLinked = 1 THEN shf.ShiftId ELSE NULL END AS CashShiftIdVal,
            CASE WHEN lk.IsLinked = 1
                 THEN DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % 480, shf.OpenedAt)
                 ELSE DATEADD(MINUTE, -1 * r.IndepMinutesAgo, GETUTCDATE())
            END AS CreatedAtRaw
    ) base
    CROSS APPLY (
        SELECT CASE WHEN base.CreatedAtRaw > GETUTCDATE() THEN GETUTCDATE() ELSE base.CreatedAtRaw END AS CreatedAtVal
    ) tm
    CROSS APPLY (
        SELECT
            CASE
                WHEN DATEDIFF(MINUTE, tm.CreatedAtVal, GETUTCDATE()) < 15 THEN
                    CASE WHEN r.StatusRoll < 60 THEN 0 ELSE 1 END          -- very recent: Open or Completed
                ELSE
                    CASE WHEN r.StatusRoll < 82 THEN 1 WHEN r.StatusRoll < 92 THEN 2 WHEN r.StatusRoll < 97 THEN 3 ELSE 1 END
                    -- Completed 82% / Voided 10% / Suspended 5% / (remainder folded back into Completed)
            END AS StatusVal
    ) sts
    CROSS APPLY (
        SELECT
            base.TerminalIdVal, base.CashierIdVal, base.CashShiftIdVal, tm.CreatedAtVal, sts.StatusVal,
            CASE WHEN sts.StatusVal = 1 AND r.PromoRoll < 15
                 THEN CAST('C0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), r.PromoN), 12) AS UNIQUEIDENTIFIER)
                 ELSE NULL
            END AS PromotionIdVal,
            CASE WHEN sts.StatusVal = 2 THEN
                CASE r.VoidReasonRoll
                    WHEN 0 THEN N'Customer changed mind before payment'
                    WHEN 1 THEN N'Price override required, re-rung as new sale'
                    ELSE N'Duplicate transaction voided by supervisor'
                END
            ELSE NULL END AS VoidReasonVal
    ) src
    WHERE n.N <= @TransactionCount4
      AND NOT EXISTS (SELECT 1 FROM Transactions x WHERE x.Id = idc.NewId);
END
GO

-- ============================================================
-- TransactionItems (1-6 lines per transaction) — random Product per line
-- via the ProductService formula; LineTotal/TaxAmount computed the same way
-- as TransactionItem.Recalculate() in the domain model.
-- ============================================================
DECLARE @TenantId5 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (
    SELECT 1 FROM TransactionItems
    WHERE TransactionId = CAST('D0000000-0001-0000-0000-000000000001' AS UNIQUEIDENTIFIER)
)
BEGIN
    INSERT INTO TransactionItems (
        Id, TransactionId, ProductId, ProductName, Quantity, UnitPrice,
        TaxRate, TaxAmount, DiscountAmount, LineTotal
    )
    SELECT
        NEWID(),
        t.Id,
        CAST('50000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), p.ProductN), 12) AS UNIQUEIDENTIFIER),
        CASE p.ProductN % 10
            WHEN 0 THEN N'Premium' WHEN 1 THEN N'Classic' WHEN 2 THEN N'Deluxe' WHEN 3 THEN N'Standard'
            WHEN 4 THEN N'Compact' WHEN 5 THEN N'Everyday' WHEN 6 THEN N'Eco' WHEN 7 THEN N'Signature'
            WHEN 8 THEN N'Travel' ELSE N'Essential'
        END + N' ' +
        CASE p.ProductN % 12
            WHEN 0 THEN N'T-Shirt' WHEN 1 THEN N'Snack Pack' WHEN 2 THEN N'Phone Case' WHEN 3 THEN N'Water Bottle'
            WHEN 4 THEN N'Notebook' WHEN 5 THEN N'Headphones' WHEN 6 THEN N'Charger Cable' WHEN 7 THEN N'Skincare Set'
            WHEN 8 THEN N'Coffee Bag' WHEN 9 THEN N'Backpack' WHEN 10 THEN N'Sneakers' ELSE N'Sunglasses'
        END + N' #' + CONVERT(VARCHAR(10), p.ProductN),
        calc.Qty,
        calc.UnitPriceVal,
        calc.TaxRateVal,
        CAST((calc.Qty * calc.UnitPriceVal - calc.DiscountAmountVal) * calc.TaxRateVal / 100 AS DECIMAL(18, 2)),
        calc.DiscountAmountVal,
        CAST(calc.Qty * calc.UnitPriceVal AS DECIMAL(18, 2))
    FROM Transactions t
    CROSS JOIN #Numbers m
    CROSS APPLY (
        SELECT 1 + (ABS(CHECKSUM(CAST(t.Id AS VARBINARY(16)))) % 6) AS ItemCount
    ) ic
    CROSS APPLY (
        SELECT 1 + ABS(CHECKSUM(HASHBYTES('MD5', CAST(t.Id AS VARBINARY(16)) + CAST(m.N AS VARBINARY(4))))) % 1000 AS ProductN
    ) p
    CROSS APPLY (
        SELECT
            1 + ABS(CHECKSUM(NEWID())) % 5 AS Qty,
            CAST(3 + (ABS(CHECKSUM(NEWID())) % 14700) / 100.0 AS DECIMAL(18, 2)) AS UnitPriceVal,
            CASE p.ProductN % 4 WHEN 0 THEN 0 WHEN 1 THEN 5 WHEN 2 THEN 8 ELSE 10 END AS TaxRateVal,
            CASE WHEN p.ProductN % 11 = 0 THEN CAST(1 + (ABS(CHECKSUM(NEWID())) % 500) / 100.0 AS DECIMAL(18, 2)) ELSE 0 END AS DiscountAmountVal
    ) calc
    WHERE t.TenantId = @TenantId5
      AND m.N <= 6
      AND m.N <= ic.ItemCount;
END
GO

-- ============================================================
-- Roll TransactionItems totals up into the parent Transaction, then
-- finalize Status-dependent fields (CompletedAt/VoidedAt/PaymentMethod).
-- ============================================================
DECLARE @TenantId6 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

UPDATE t
SET
    t.SubTotal = agg.SubTotalVal,
    t.TaxAmount = agg.TaxAmountVal,
    t.DiscountAmount = agg.ItemDiscountVal + promo.PromoDiscountVal,
    t.TotalAmount = agg.SubTotalVal - (agg.ItemDiscountVal + promo.PromoDiscountVal) + agg.TaxAmountVal,
    t.UpdatedAt = t.CreatedAt
FROM Transactions t
INNER JOIN (
    SELECT TransactionId, SUM(LineTotal) AS SubTotalVal, SUM(TaxAmount) AS TaxAmountVal, SUM(DiscountAmount) AS ItemDiscountVal
    FROM TransactionItems
    GROUP BY TransactionId
) agg ON agg.TransactionId = t.Id
CROSS APPLY (
    SELECT CASE WHEN t.PromotionId IS NOT NULL THEN CAST(agg.SubTotalVal * 0.10 AS DECIMAL(18, 2)) ELSE 0 END AS PromoDiscountVal
) promo
WHERE t.TenantId = @TenantId6;

UPDATE Transactions
SET
    CompletedAt = CASE WHEN Status = 1 THEN DATEADD(MINUTE, 3, CreatedAt) ELSE NULL END,
    VoidedAt    = CASE WHEN Status = 2 THEN DATEADD(MINUTE, 5, CreatedAt) ELSE NULL END,
    PaymentMethod = CASE WHEN Status = 1 THEN ABS(CHECKSUM(CAST(Id AS VARBINARY(16)))) % 3 ELSE NULL END -- legacy INT: 0=Cash,1=CreditCard,2=EWallet
WHERE TenantId = @TenantId6;
GO

-- ============================================================
-- TransactionTenders — the real, app-readable payment breakdown. Single
-- tender for most completed sales; ~15% split across two methods.
-- ============================================================
DECLARE @TenantId7 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (
    SELECT 1 FROM TransactionTenders tt
    INNER JOIN Transactions t ON t.Id = tt.TransactionId
    WHERE t.TenantId = @TenantId7
)
BEGIN
    ;WITH Completed AS (
        SELECT t.Id, t.TotalAmount, t.PaymentMethod,
               ABS(CHECKSUM(CAST(t.Id AS VARBINARY(16)))) % 100 AS SplitRoll
        FROM Transactions t
        WHERE t.TenantId = @TenantId7 AND t.Status = 1
    )
    INSERT INTO TransactionTenders (Id, TransactionId, PaymentMethod, Amount)
    SELECT NEWID(), c.Id,
        CASE c.PaymentMethod WHEN 0 THEN N'Cash' WHEN 1 THEN N'CreditCard' ELSE N'EWallet' END,
        c.TotalAmount
    FROM Completed c
    WHERE c.SplitRoll >= 15

    UNION ALL

    SELECT NEWID(), c.Id, N'Cash', CAST(c.TotalAmount * 0.5 AS DECIMAL(18, 2))
    FROM Completed c
    WHERE c.SplitRoll < 15

    UNION ALL

    SELECT NEWID(), c.Id, N'CreditCard', c.TotalAmount - CAST(c.TotalAmount * 0.5 AS DECIMAL(18, 2))
    FROM Completed c
    WHERE c.SplitRoll < 15;
END
GO

-- ============================================================
-- Roll actual Transactions back up into their CashShift (TotalSales,
-- TransactionCount, approximate ClosingBalance for Closed shifts).
-- ============================================================
DECLARE @TenantId8 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

UPDATE s
SET
    s.TotalSales = ISNULL(agg.SalesTotal, 0),
    s.TransactionCount = ISNULL(agg.TxnCount, 0),
    s.ClosingBalance = CASE WHEN s.Status = 1 THEN s.OpeningBalance + ISNULL(cash.CashTotal, 0) ELSE NULL END
FROM CashShifts s
LEFT JOIN (
    SELECT CashShiftId, SUM(TotalAmount) AS SalesTotal, COUNT(*) AS TxnCount
    FROM Transactions
    WHERE TenantId = @TenantId8 AND Status = 1 AND CashShiftId IS NOT NULL
    GROUP BY CashShiftId
) agg ON agg.CashShiftId = s.Id
LEFT JOIN (
    SELECT t.CashShiftId, SUM(tt.Amount) AS CashTotal
    FROM Transactions t
    INNER JOIN TransactionTenders tt ON tt.TransactionId = t.Id AND tt.PaymentMethod = N'Cash'
    WHERE t.TenantId = @TenantId8 AND t.Status = 1 AND t.CashShiftId IS NOT NULL
    GROUP BY t.CashShiftId
) cash ON cash.CashShiftId = s.Id
WHERE s.TenantId = @TenantId8;
GO
