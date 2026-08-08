-- ============================================================
-- PaymentDB Migration 005 — Demo Seed Data
-- Seeds 1000+ PaymentTransaction (Payments) rows + a modest set of
-- PaymentRefunds, for the cross-service demo-data effort described in
-- src/Database/SEED_DATA_CONVENTIONS.md.
--
-- GUID formula (mandatory, see conventions doc):
--   Payments.Id = A0000000-0001-0000-0000-{N}            (this service's own registry)
--   Payments.OrderId = 70000000-0001-0000-0000-{N}        (Sales SalesOrder, N in [1,1000])
--
-- Coverage design (deterministic via OrderN % 100, so the script is easy to
-- reason about and re-run):
--   residues  0..15 (160 orders) -> 2 payment attempts each: 1st Failed,
--                                   2nd Captured (or Refunded) -> 320 rows
--   residues 16..23 ( 80 orders) -> 1 attempt, Failed (abandoned, no retry)   ->  80 rows
--   residues 24..33 (100 orders) -> 1 attempt, Pending                       -> 100 rows
--   residues 34..43 (100 orders) -> 1 attempt, Cancelled                     -> 100 rows
--   residues 44..84 (410 orders) -> 1 attempt, Captured (~1/17 flagged Refunded) -> 410 rows
--   residues 85..99 (150 orders) -> NO payment at all (order never paid/attempted)
-- Totals: 850/1000 orders (85%) have at least one payment attempt, 150 (15%)
-- have none — "most, not all" per the task brief. Row count =
-- 320 + 80 + 100 + 100 + 410 = 1010 rows (>= 1000 target).
-- Idempotent: every INSERT is guarded by a NOT EXISTS check on the
-- deterministic Id, safe to re-run.
-- ============================================================

USE PaymentDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

;WITH Numbers AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Numbers WHERE N < 1000
),
OrderBuckets AS (
    -- One deterministic "order profile" per SalesOrder N: a base date spread
    -- over the last 12 months, and a plausible order amount, both reused by
    -- every payment attempt that belongs to this order (a retry keeps the
    -- same order amount; only the attempt number shifts the date/status).
    SELECT
        N,
        N % 100 AS Residue,
        DATEADD(MINUTE, -(ABS(CHECKSUM(NEWID())) % 1440),
            DATEADD(DAY, -(ABS(CHECKSUM(NEWID())) % 365), GETUTCDATE())) AS OrderBaseDate,
        CAST(15 + (ABS(CHECKSUM(NEWID())) % 148500) / 100.0 AS DECIMAL(18,2)) AS OrderAmount
    FROM Numbers
),
Attempts AS (
    -- Retry orders: residues 0..15 -> Failed then Captured/Refunded (2 rows)
    SELECT N AS OrderN, 1 AS AttemptSeq, 3 AS Status, OrderBaseDate, OrderAmount
    FROM OrderBuckets WHERE Residue BETWEEN 0 AND 15
    UNION ALL
    SELECT N, 2, CASE WHEN N % 17 = 0 THEN 5 ELSE 2 END, OrderBaseDate, OrderAmount
    FROM OrderBuckets WHERE Residue BETWEEN 0 AND 15
    UNION ALL
    -- Failed only, no retry (abandoned checkout)
    SELECT N, 1, 3, OrderBaseDate, OrderAmount
    FROM OrderBuckets WHERE Residue BETWEEN 16 AND 23
    UNION ALL
    -- Pending (payment initiated, never completed)
    SELECT N, 1, 0, OrderBaseDate, OrderAmount
    FROM OrderBuckets WHERE Residue BETWEEN 24 AND 33
    UNION ALL
    -- Cancelled
    SELECT N, 1, 4, OrderBaseDate, OrderAmount
    FROM OrderBuckets WHERE Residue BETWEEN 34 AND 43
    UNION ALL
    -- Captured (successful, single attempt) — ~1/17 flagged Refunded afterwards
    SELECT N, 1, CASE WHEN N % 17 = 0 THEN 5 ELSE 2 END, OrderBaseDate, OrderAmount
    FROM OrderBuckets WHERE Residue BETWEEN 44 AND 84
    -- residues 85..99 (150 orders) intentionally produce NO rows -> unpaid orders
),
Base AS (
    SELECT
        ROW_NUMBER() OVER (ORDER BY OrderN, AttemptSeq) AS PaymentN,
        OrderN,
        AttemptSeq,
        Status,
        OrderAmount,
        CASE WHEN AttemptSeq = 2 THEN DATEADD(HOUR, 6, OrderBaseDate) ELSE OrderBaseDate END AS CreatedAt,
        ABS(CHECKSUM(NEWID())) % 5 AS MethodRnd,          -- 0=CreditCard,1=DebitCard,2=BankTransfer,3=EWallet,4=Cash(COD)
        ABS(CHECKSUM(NEWID())) % 3 AS ReasonRnd,
        ABS(CHECKSUM(NEWID())) % 100000 AS GwRnd
    FROM Attempts
)
INSERT INTO Payments (
    Id, TenantId, OrderId, Amount, Currency, Method, Status,
    GatewayProvider, GatewayTransactionId, TransactionRef, FailureReason,
    AuthorizedAt, CapturedAt, CancelledAt, FailedAt, CreatedAt, UpdatedAt
)
SELECT
    NewId,
    '00000000-0000-0000-0000-000000000001',
    NewOrderId,
    b.OrderAmount,
    'USD',
    b.MethodRnd,
    b.Status,
    CASE b.MethodRnd
        WHEN 0 THEN N'Stripe' WHEN 1 THEN N'Stripe' WHEN 2 THEN N'ManualBankTransfer'
        WHEN 3 THEN N'VnPay' ELSE N'COD' END,
    CASE WHEN b.MethodRnd = 4 THEN NULL ELSE 'gw_' + CONVERT(VARCHAR(20), b.PaymentN) + '_' + CONVERT(VARCHAR(20), b.GwRnd) END,
    'TXN' + RIGHT('0000000' + CONVERT(VARCHAR(10), b.PaymentN), 7),
    CASE
        WHEN b.Status = 3 THEN
            CASE b.ReasonRnd WHEN 0 THEN N'Card declined by issuer' WHEN 1 THEN N'Insufficient funds' ELSE N'Gateway timeout' END
        WHEN b.Status = 4 THEN N'Cancelled by customer before capture'
        ELSE NULL
    END,
    CASE WHEN b.Status IN (1,2,5) THEN DATEADD(MINUTE, 2, b.CreatedAt) ELSE NULL END,
    CASE WHEN b.Status IN (2,5) THEN DATEADD(MINUTE, 5, b.CreatedAt) ELSE NULL END,
    CASE WHEN b.Status = 4 THEN DATEADD(MINUTE, 10, b.CreatedAt) ELSE NULL END,
    CASE WHEN b.Status = 3 THEN DATEADD(MINUTE, 3, b.CreatedAt) ELSE NULL END,
    b.CreatedAt,
    CASE WHEN b.Status IN (2,3,4,5) THEN DATEADD(MINUTE, 10, b.CreatedAt) ELSE b.CreatedAt END
FROM Base b
CROSS APPLY (SELECT CAST('A0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.PaymentN), 12) AS UNIQUEIDENTIFIER) AS NewId) idc
CROSS APPLY (SELECT CAST('70000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.OrderN), 12) AS UNIQUEIDENTIFIER) AS NewOrderId) oidc
WHERE NOT EXISTS (SELECT 1 FROM Payments p WHERE p.Id = idc.NewId)
OPTION (MAXRECURSION 1000);
GO

-- ============================================================
-- Refunds for the modest slice of payments flagged Status = 5 (Refunded)
-- ============================================================
INSERT INTO PaymentRefunds (
    Id, TenantId, PaymentId, Amount, Reason, Status, GatewayRefundId,
    CompletedAt, CreatedAt, UpdatedAt
)
SELECT
    NEWID(),
    p.TenantId,
    p.Id,
    p.Amount,
    N'Customer requested refund',
    1, -- Completed
    'RF-' + CONVERT(VARCHAR(20), ABS(CHECKSUM(NEWID())) % 1000000),
    DATEADD(DAY, 2, p.CapturedAt),
    DATEADD(DAY, 2, p.CapturedAt),
    DATEADD(DAY, 2, p.CapturedAt)
FROM Payments p
WHERE p.Status = 5
  AND NOT EXISTS (SELECT 1 FROM PaymentRefunds pr WHERE pr.PaymentId = p.Id);
GO
