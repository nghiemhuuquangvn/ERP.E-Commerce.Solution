-- ============================================================
-- ReportingDB Migration 003 — One-time ETL backfill of the OLAP
-- fact/dimension tables from the already-seeded business databases.
--
-- WHY THIS SCRIPT INTENTIONALLY CROSS-DATABASE-QUERIES
-- (a deliberate, documented exception to the "no cross-database queries /
-- no direct table access" application-layer rule in root CLAUDE.md):
--
--   CLAUDE.md's Integration Rules forbid cross-database queries and direct
--   table access *at the application layer* (services must talk to each
--   other only via REST/RabbitMQ/Kafka/Shared Contracts). This script is
--   NOT application code — it is a one-time, offline ETL/backfill batch
--   run directly against SQL Server, the exact use case batch ETL exists
--   for. In production, ReportingService's fact/dim tables are meant to be
--   populated incrementally, event-by-event, by real Kafka consumers
--   (mirroring IdentityService's `IdentityEventConsumer` pattern, per
--   CLAUDE.md's Notification Service note) as SalesOrderCreated,
--   PaymentProcessed, StockMoved, etc. integration events arrive.
--
--   The problem this script solves: all 16 service databases were just
--   bulk-seeded with historical demo data (src/Database/SEED_DATA_CONVENTIONS.md)
--   directly via SQL, so no integration event was ever published for any of
--   these rows — there is nothing for a Kafka consumer to have consumed.
--   Rather than fabricate and replay 5000+ fake Kafka messages just to reuse
--   the live consumer code path, this script does the equivalent
--   transformation synchronously and in bulk: read the already-committed
--   source rows straight out of each service's database (three-part names,
--   same SQL Server instance, sqlcmd/sa access) and INSERT...SELECT them
--   into ReportingDB's star schema in the shape the real consumers would
--   have produced. It is idempotent (NOT EXISTS-guarded per row, like every
--   other NNN_SeedDemoData.sql in this repo) and safe to re-run.
--
--   This is a standing exception for this exact script, not a precedent for
--   any application code — no C# in ReportingService (or anywhere else)
--   should ever open a connection to another service's database.
--
-- Source -> Target mapping:
--   ProductDB.dbo.Products (+Categories, +ProductVariants, +PriceListItems) -> DimProducts
--   SalesDB.dbo.SalesOrders (+SalesOrderItems)                              -> FactSalesOrders
--   PaymentDB.dbo.Payments (+PaymentRefunds)                                -> FactPayments
--   WarehouseDB.dbo.StockMovements                                         -> FactInventoryMovements
--   PosDB.dbo.Transactions (+TransactionItems)                             -> FactPOSTransactions
--   HrmDB.dbo.PaySlips (+MasterDataDB.dbo.Employees for name)              -> FactHRPayslips
--
-- Enum/schema facts verified against the actual seed scripts and C# domain
-- enums (not assumed) — see the per-block comments below for anything that
-- differed from a first-pass assumption.
-- ============================================================

USE ReportingDB;
GO

-- This script reads other services' databases (documented one-time ETL exception). On a database being built
-- from scratch they do not exist yet (folders run alphabetically): skip instead of failing; run db-migrate again
-- once every service database has been provisioned and this backfill is applied.
IF OBJECT_ID('HrmDB.dbo.PaySlips') IS NULL OR OBJECT_ID('MasterDataDB.dbo.Employees') IS NULL OR OBJECT_ID('PaymentDB.dbo.PaymentRefunds') IS NULL OR OBJECT_ID('PaymentDB.dbo.Payments') IS NULL OR OBJECT_ID('PosDB.dbo.TransactionItems') IS NULL OR OBJECT_ID('PosDB.dbo.Transactions') IS NULL OR OBJECT_ID('ProductDB.dbo.Categories') IS NULL OR OBJECT_ID('ProductDB.dbo.PriceListItems') IS NULL OR OBJECT_ID('ProductDB.dbo.ProductVariants') IS NULL OR OBJECT_ID('ProductDB.dbo.Products') IS NULL OR OBJECT_ID('SalesDB.dbo.SalesOrderItems') IS NULL OR OBJECT_ID('SalesDB.dbo.SalesOrders') IS NULL OR OBJECT_ID('WarehouseDB.dbo.Inventories') IS NULL OR OBJECT_ID('WarehouseDB.dbo.StockMovements') IS NULL
BEGIN
    PRINT 'Reporting 003 skipped: a source database is not provisioned yet - re-run db-migrate later.';
    SET NOEXEC ON;
END
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- NOTE: @TenantId is NOT declared as a T-SQL variable here because each GO
-- separator starts a new batch and batch-local variables do not survive
-- across GO boundaries under sqlcmd. The tenant GUID is therefore inlined
-- as the literal '00000000-0000-0000-0000-000000000001' in every block
-- below (same fixed System Tenant GUID used by every other seed script
-- per SEED_DATA_CONVENTIONS.md).

-- ============================================================
-- 1) DimProducts <- ProductDB.dbo.Products
--    Status enum verified via ERP.PRD.Domain.Enums.ProductStatus.cs:
--      0=Draft, 1=Active, 2=Discontinued, 3=PendingReview
--    (011_SeedDemoData.sql only ever emits 0/1/2: ~5% Draft, ~3%
--    Discontinued, ~92% Active — NOT the "~87% Active" guessed up front.)
--    Price/BasePrice = MIN(PriceListItems.BasePrice) across all of a
--    product's variants across all price lists, default 0 if none priced.
--    Both BasePrice and Price columns get the same value (002 migration
--    added Price as an EF-mapped alias of the original BasePrice column).
-- ============================================================
INSERT INTO dbo.DimProducts
    (ProductId, TenantId, Sku, Name, Category, BasePrice, Status, UpdatedAt, Price, IsActive)
SELECT
    p.Id,
    '00000000-0000-0000-0000-000000000001',
    p.Sku,
    p.Name,
    c.Name,
    ISNULL(pv.MinPrice, 0),
    CASE p.Status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'Active'
        WHEN 2 THEN 'Discontinued'
        WHEN 3 THEN 'PendingReview'
        ELSE 'Unknown'
    END,
    GETUTCDATE(),
    ISNULL(pv.MinPrice, 0),
    CASE WHEN p.Status = 1 THEN 1 ELSE 0 END
FROM ProductDB.dbo.Products p
LEFT JOIN ProductDB.dbo.Categories c ON c.Id = p.CategoryId
OUTER APPLY (
    SELECT MIN(pli.BasePrice) AS MinPrice
    FROM ProductDB.dbo.ProductVariants v
    JOIN ProductDB.dbo.PriceListItems pli ON pli.VariantId = v.Id
    WHERE v.ProductId = p.Id
) pv
WHERE p.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.DimProducts d WHERE d.ProductId = p.Id);
GO

-- ============================================================
-- 2) FactSalesOrders <- SalesDB.dbo.SalesOrders (+SalesOrderItems for count)
--    Status enum verified via SalesDB/010_SeedDemoData.sql's own header
--    comment: 0=Draft,1=Submitted,2=Approved,3=Picked,4=Shipped,5=Delivered,
--    6=Cancelled,7=Rejected (matches the brief exactly).
--    Channel derived from the real OrderSource column (added by
--    006_AddOrderCoreFields.sql, not in the original prompt's column list
--    but present in the live schema): 0=Manual,1=Ecommerce->'Online',
--    2=POS->'POS'.
-- ============================================================
INSERT INTO dbo.FactSalesOrders
    (Id, TenantId, OrderId, OrderDateKey, CustomerId, TotalAmount, LineItemCount,
     Status, Channel, UpdatedAt, CustomerName, ItemCount, OrderDate, ApprovedAt,
     ShippedAt, DeliveredAt, DateKey)
SELECT
    o.Id,
    '00000000-0000-0000-0000-000000000001',
    o.Id,
    CONVERT(INT, CONVERT(VARCHAR(8), o.OrderDate, 112)),
    o.CustomerId,
    o.TotalAmount,
    ISNULL(ic.ItemCount, 0),
    CASE o.Status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'Submitted'
        WHEN 2 THEN 'Approved'
        WHEN 3 THEN 'Picked'
        WHEN 4 THEN 'Shipped'
        WHEN 5 THEN 'Delivered'
        WHEN 6 THEN 'Cancelled'
        WHEN 7 THEN 'Rejected'
        ELSE 'Unknown'
    END,
    CASE o.OrderSource WHEN 1 THEN 'Online' WHEN 2 THEN 'POS' ELSE 'Manual' END,
    GETUTCDATE(),
    o.CustomerName,
    ISNULL(ic.ItemCount, 0),
    o.OrderDate,
    o.ApprovedAt,
    o.ShippedAt,
    o.DeliveredAt,
    CONVERT(INT, CONVERT(VARCHAR(8), o.OrderDate, 112))
FROM SalesDB.dbo.SalesOrders o
OUTER APPLY (
    SELECT COUNT(*) AS ItemCount
    FROM SalesDB.dbo.SalesOrderItems i
    WHERE i.OrderId = o.Id
) ic
WHERE o.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.FactSalesOrders f WHERE f.Id = o.Id);
GO

-- ============================================================
-- 3) FactPayments <- PaymentDB.dbo.Payments (+PaymentRefunds)
--    Method/Status enums verified via ERP.PAY.Domain.Enums.PaymentStatus.cs:
--      Method: 0=CreditCard,1=DebitCard,2=BankTransfer,3=EWallet,4=Cash
--      Status: 0=Pending,1=Authorized,2=Captured,3=Failed,4=Cancelled,
--              5=Refunded,6=PartiallyRefunded
--    Status=2 (Captured) is mapped to the literal string 'Processed'
--    because ReportingRepository.GetPaymentSummaryAsync filters
--    `p.Status == "Processed"` verbatim (confirmed by re-reading that
--    method) — the brief's guess of this mapping was correct.
--    ProcessedAt = CapturedAt when present, else CreatedAt (matches brief).
-- ============================================================
INSERT INTO dbo.FactPayments
    (Id, TenantId, PaymentId, OrderId, PaymentDateKey, Amount, Currency, Method,
     Status, RefundedAmount, UpdatedAt, ProcessedAt, DateKey)
SELECT
    p.Id,
    '00000000-0000-0000-0000-000000000001',
    p.Id,
    p.OrderId,
    CONVERT(INT, CONVERT(VARCHAR(8), ISNULL(p.CapturedAt, p.CreatedAt), 112)),
    p.Amount,
    p.Currency,
    CASE p.Method
        WHEN 0 THEN 'CreditCard'
        WHEN 1 THEN 'DebitCard'
        WHEN 2 THEN 'BankTransfer'
        WHEN 3 THEN 'EWallet'
        WHEN 4 THEN 'Cash'
        ELSE 'Unknown'
    END,
    CASE p.Status
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Authorized'
        WHEN 2 THEN 'Processed'
        WHEN 3 THEN 'Failed'
        WHEN 4 THEN 'Cancelled'
        WHEN 5 THEN 'Refunded'
        WHEN 6 THEN 'PartiallyRefunded'
        ELSE 'Unknown'
    END,
    ISNULL(rf.RefundedAmount, 0),
    GETUTCDATE(),
    ISNULL(p.CapturedAt, p.CreatedAt),
    CONVERT(INT, CONVERT(VARCHAR(8), ISNULL(p.CapturedAt, p.CreatedAt), 112))
FROM PaymentDB.dbo.Payments p
OUTER APPLY (
    SELECT SUM(r.Amount) AS RefundedAmount
    FROM PaymentDB.dbo.PaymentRefunds r
    WHERE r.PaymentId = p.Id
) rf
WHERE NOT EXISTS (SELECT 1 FROM dbo.FactPayments f WHERE f.Id = p.Id);
GO

-- ============================================================
-- 4) FactInventoryMovements <- WarehouseDB.dbo.StockMovements
--    MovementType enum verified via 006_SeedDemoData.sql's own comment:
--      0=Receipt,1=Issue,2=Transfer,3=Adjustment,4=Return (schema comment
--      lists a 5th value, Return, unused by the seed script but mapped
--      here for completeness/future-proofing).
--    Mapped to 'Receipt'/'Issue' literally (not 'Receive'/'Shipment') so
--    ReportingRepository.GetInventoryReportAsync's
--    .Contains("receipt")/.Contains("issue") checks match.
-- ============================================================
INSERT INTO dbo.FactInventoryMovements
    (Id, TenantId, MovementId, ProductId, MovementDateKey, MovementType, Quantity,
     ReferenceId, UpdatedAt, OccurredAt, DateKey)
SELECT
    m.MovementId,
    '00000000-0000-0000-0000-000000000001',
    m.MovementId,
    m.ProductId,
    CONVERT(INT, CONVERT(VARCHAR(8), m.RecordedAt, 112)),
    CASE m.MovementType
        WHEN 0 THEN 'Receipt'
        WHEN 1 THEN 'Issue'
        WHEN 2 THEN 'Transfer'
        WHEN 3 THEN 'Adjustment'
        WHEN 4 THEN 'Return'
        ELSE 'Unknown'
    END,
    m.Quantity,
    m.ReferenceId,
    GETUTCDATE(),
    m.RecordedAt,
    CONVERT(INT, CONVERT(VARCHAR(8), m.RecordedAt, 112))
FROM WarehouseDB.dbo.StockMovements m
WHERE NOT EXISTS (SELECT 1 FROM dbo.FactInventoryMovements f WHERE f.MovementId = m.MovementId);
GO

-- ============================================================
-- 5) FactPOSTransactions <- PosDB.dbo.Transactions (+TransactionItems)
--    Status enum verified via POS/004_SeedDemoData.sql's own header note:
--    the REAL enum (ERP.POS.Domain.Enums.TransactionStatus) is
--    {Open=0, Completed=1, Voided=2, Suspended=3} — NOT the
--    "0=Draft,1=Completed,2=Voided" guessed up front (which also omits
--    Suspended=3). Only Status=1 (Completed) rows are loaded, per brief.
--    StoreId has no real source column on Transactions — reused from
--    TerminalId's value as instructed.
--    PaymentMethod on Transactions is stored as a legacy INT (0=Cash,
--    1=CreditCard,2=EWallet) despite the C# entity really using a string;
--    carried over as-is per brief since ReportingRepository never reads
--    this column.
-- ============================================================
INSERT INTO dbo.FactPOSTransactions
    (Id, TenantId, TransactionId, TxnDateKey, TerminalId, CashierId, TotalAmount,
     ItemCount, PaymentMethod, UpdatedAt, StoreId, TaxAmount, OccurredAt, DateKey)
SELECT
    t.Id,
    '00000000-0000-0000-0000-000000000001',
    t.Id,
    CONVERT(INT, CONVERT(VARCHAR(8), ISNULL(t.CompletedAt, t.CreatedAt), 112)),
    t.TerminalId,
    t.CashierId,
    t.TotalAmount,
    ISNULL(ic.ItemCount, 0),
    CONVERT(NVARCHAR(50), t.PaymentMethod),
    GETUTCDATE(),
    t.TerminalId,
    t.TaxAmount,
    ISNULL(t.CompletedAt, t.CreatedAt),
    CONVERT(INT, CONVERT(VARCHAR(8), ISNULL(t.CompletedAt, t.CreatedAt), 112))
FROM PosDB.dbo.Transactions t
OUTER APPLY (
    SELECT COUNT(*) AS ItemCount
    FROM PosDB.dbo.TransactionItems ti
    WHERE ti.TransactionId = t.Id
) ic
WHERE t.Status = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.FactPOSTransactions f WHERE f.TransactionId = t.Id);
GO

-- ============================================================
-- 6) FactHRPayslips <- HrmDB.dbo.PaySlips (+MasterDataDB.dbo.Employees
--    for EmployeeName)
--    Schema surprise vs. the original brief: PaySlips has NO Year/Month
--    int columns in its base 001 DDL and NO plain NetSalary column either
--    — but 003_FixHrmEntityColumns.sql DOES add real Year/Month/OvertimePay
--    columns (confirmed populated by 004_SeedDemoData.sql), and NetSalary
--    exists as a PERSISTED computed column
--    (BasicSalary+Allowances-Deductions-TaxAmount-SocialInsurance), so both
--    are safely selectable directly — the brief's assumption turned out
--    correct in the end, just not obvious from the base 001 DDL alone.
--    FactHRPayslips has no TenantId column (confirmed against 002's DDL) —
--    intentionally omitted below.
-- ============================================================
INSERT INTO dbo.FactHRPayslips
    (PayslipId, EmployeeId, EmployeeName, Period, BasicSalary, OvertimePay,
     TotalAmount, PaidAt, DateKey)
SELECT
    ps.Id,
    ps.EmployeeId,
    ISNULL(e.FirstName + ' ' + e.LastName, ''),
    CONVERT(VARCHAR(4), ps.[Year]) + '-' + RIGHT('0' + CONVERT(VARCHAR(2), ps.[Month]), 2),
    ps.BasicSalary,
    ps.OvertimePay,
    ps.NetSalary,
    ps.PaidAt,
    CONVERT(INT, CONVERT(VARCHAR(8), ps.PeriodStart, 112))
FROM HrmDB.dbo.PaySlips ps
LEFT JOIN MasterDataDB.dbo.Employees e ON e.Id = ps.EmployeeId
WHERE ps.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.FactHRPayslips f WHERE f.PayslipId = ps.Id);
GO

-- ============================================================
-- Verification — row counts per target table
-- ============================================================
SELECT 'DimProducts' AS TableName, COUNT(*) AS TotalRows FROM dbo.DimProducts
UNION ALL SELECT 'FactSalesOrders', COUNT(*) FROM dbo.FactSalesOrders
UNION ALL SELECT 'FactPayments', COUNT(*) FROM dbo.FactPayments
UNION ALL SELECT 'FactInventoryMovements', COUNT(*) FROM dbo.FactInventoryMovements
UNION ALL SELECT 'FactPOSTransactions', COUNT(*) FROM dbo.FactPOSTransactions
UNION ALL SELECT 'FactHRPayslips', COUNT(*) FROM dbo.FactHRPayslips;
GO

SET NOEXEC OFF;
GO
