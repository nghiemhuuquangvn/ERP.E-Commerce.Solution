-- ============================================================
-- AccountingDB Migration 005 — Demo data seed (FiscalPeriods,
-- JournalEntries/JournalEntryLines, Invoices/InvoiceLines, Bills/BillLines).
--
-- Follows src/Database/SEED_DATA_CONVENTIONS.md deterministic GUID formula:
--   {PrefixHigh}-{PrefixLow}-0000-0000-{N padded to 12 hex digits}
--   JournalEntry  E0000000-0001-0000-0000-{N}   N in [1,1200]  (target 1000+)
--   Invoice       E0000000-0002-0000-0000-{N}   N in [1,1200]  (target 1000+)
--   Bill          E0000000-0003-0000-0000-{N}   N in [1,600]   (target 500+)
-- Cross-service references used here (rows do not need to exist yet in the
-- owning service's DB at migration time — no FK across databases):
--   Invoices.CustomerId -> Identity  Customer User : 20000000-0002-0000-0000-{N}, N in [1,1000]
--   Invoices.OrderId    -> Sales     SalesOrder     : 70000000-0001-0000-0000-{N}, N in [1,1000]
--   Bills.SupplierId    -> Purchasing Supplier      : 90000000-0001-0000-0000-{N}, N in [1,50]
--   Bills.POId          -> Purchasing PurchaseOrder : 90000000-0002-0000-0000-{N}, N in [1,500]
--   JournalEntries.ReferenceId -> SalesOrder (N in [1,1000]) or PurchaseOrder
--   (N in [1,500]) depending on entry type, or NULL for Payroll/Payment/Tax/Manual.
--
-- Schema surprises adapted to (verified against 001/002/003/004, not assumed):
--   - 001 seeds a ~13-row Chart of Accounts (AccountNumber '1000'..'5200') but
--     ZERO FiscalPeriods — JournalEntries.FiscalPeriodId is NOT NULL with an FK
--     to FiscalPeriods, so this script seeds 14 FiscalPeriods (current month +
--     13 prior) before touching JournalEntries. The current-month period is
--     left Status=0 (Open) so "now" always falls inside an open period; the 13
--     prior months are Status=1 (Closed). This is additive, not a duplicate of
--     the existing Chart of Accounts seed.
--   - Accounts.Id values are NEWID()-random (not deterministic per the
--     conventions registry — Accounts/Chart-of-Accounts isn't a registered
--     cross-service entity), so JournalEntryLines/InvoiceLines/BillLines look
--     up the real AccountId by the stable AccountNumber ('1000','4000', etc.)
--     at insert time rather than computing a formula GUID.
--   - Invoices.AmountDue and Bills.AmountDue are PERSISTED COMPUTED columns
--     (TotalAmount - AmountPaid) — never included in an insert column list.
--   - Accounts/FiscalPeriods/JournalEntries/Invoices/Bills all carry a
--     RowVersion ROWVERSION column added in 004 — also never in an insert list
--     (SQL Server populates it automatically).
--   - JournalEntryLines.CHK_JELine_OneAmount requires exactly one of
--     DebitAmount/CreditAmount > 0 per line — this script always pairs one
--     Debit-only line with one Credit-only line of the same Amount per entry,
--     which is what the task calls for and satisfies the CHECK constraint.
--   - Bills.Status only has 5 states (0=Draft..4=Overdue, no Void), whereas
--     Invoices.Status has 6 (adds 5=Void) — handled with separate status logic
--     per table rather than sharing one CASE expression.
--
-- Idempotent: each section is guarded by a row-count threshold check plus a
-- per-row NOT EXISTS, so re-running this script (e.g. on every docker-compose
-- db-migrate run) is a no-op after the first successful seed.
--
-- Numbers are generated via master..spt_values (type = 'P', ~2048 rows) per
-- the documented preference over sys.all_objects CROSS JOIN sys.all_objects,
-- which is known to be unreliable for numbers generation in this repo.
-- ============================================================

USE AccountingDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;

DECLARE @TenantId          UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @JournalEntryCount INT = 1200;
DECLARE @InvoiceCount      INT = 1200;
DECLARE @BillCount         INT = 600;

-- ------------------------------------------------------------
-- Reusable numbers table (1..1200) via master..spt_values, reused by every
-- set-based generator below instead of hand-written literal INSERT rows.
-- ------------------------------------------------------------
IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
SELECT TOP (1200) number + 1 AS N
INTO #Numbers
FROM master..spt_values
WHERE type = 'P'
ORDER BY number;

-- ============================================================
-- 1) FiscalPeriods (14 rows: current month + 13 prior) — 001 never seeded
--    any, and JournalEntries.FiscalPeriodId is NOT NULL, so this must exist
--    first. Current month (k=0) is left Open so "now" is always covered.
-- ============================================================
IF (SELECT COUNT(*) FROM FiscalPeriods WHERE TenantId = @TenantId) < 14
BEGIN
    INSERT INTO FiscalPeriods (
        Id, TenantId, Name, StartDate, EndDate, FiscalYear, PeriodMonth, Status,
        ClosedAt, ClosedBy, Description, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
    )
    SELECT
        NEWID(),
        @TenantId,
        DATENAME(MONTH, d.MonthStart) + ' ' + CONVERT(VARCHAR(4), YEAR(d.MonthStart)),
        d.MonthStart,
        EOMONTH(d.MonthStart),
        YEAR(d.MonthStart),
        MONTH(d.MonthStart),
        CASE WHEN mb.k = 0 THEN 0 ELSE 1 END,                                   -- 0=Open (current), 1=Closed (prior)
        CASE WHEN mb.k = 0 THEN NULL ELSE CAST(DATEADD(DAY, 5, EOMONTH(d.MonthStart)) AS DATETIME2) END,
        CASE WHEN mb.k = 0 THEN NULL ELSE 'seed-script' END,
        N'Demo seed fiscal period',
        'seed-script',
        GETUTCDATE(),
        'seed-script',
        GETUTCDATE(),
        0
    FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13)) mb(k)
    CROSS APPLY (
        SELECT DATEFROMPARTS(
            YEAR(DATEADD(MONTH, -mb.k, GETUTCDATE())),
            MONTH(DATEADD(MONTH, -mb.k, GETUTCDATE())),
            1
        ) AS MonthStart
    ) d
    WHERE NOT EXISTS (
        SELECT 1 FROM FiscalPeriods fp
        WHERE fp.TenantId = @TenantId AND fp.FiscalYear = YEAR(d.MonthStart) AND fp.PeriodMonth = MONTH(d.MonthStart)
    );

    PRINT 'AccountingDB demo data: FiscalPeriods seeded (14 rows).';
END
ELSE
BEGIN
    PRINT 'AccountingDB demo data: FiscalPeriods seed skipped (already >= 14 rows for tenant).';
END

-- ============================================================
-- 2) JournalEntries (1200) + JournalEntryLines (2400, balanced pairs).
--    8 entry types rotate through the seeded Chart of Accounts, dated over
--    the last ~365 days. Status is mostly Posted, with a small Draft/Voided
--    mix for realism.
-- ============================================================
IF (SELECT COUNT(*) FROM JournalEntries) < 1000
BEGIN
    IF OBJECT_ID('tempdb..#JE') IS NOT NULL DROP TABLE #JE;

    SELECT
        n.N,
        t.TypeIdx, t.DebitNum, t.CreditNum, t.Descr, t.RefType,
        ed.EntryDate,
        am.Amount,
        rr.RefRoll,
        sr.StatusRoll,
        ReferenceId = CASE t.RefType
            WHEN 'SalesOrder' THEN
                CAST('70000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), 1 + (rr.RefRoll % 1000)), 12) AS UNIQUEIDENTIFIER)
            WHEN 'PurchaseOrder' THEN
                CAST('90000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), 1 + (rr.RefRoll % 500)), 12) AS UNIQUEIDENTIFIER)
            ELSE NULL
        END
    INTO #JE
    FROM #Numbers n
    JOIN (VALUES
        (0,'1100','4000',N'Sales revenue recognition',                'SalesOrder'),
        (1,'5000','1200',N'Cost of goods sold - inventory relief',    'SalesOrder'),
        (2,'5100','2000',N'Purchase of operating supplies',           'PurchaseOrder'),
        (3,'5200','1000',N'Payroll salaries and benefits payment',    'Payroll'),
        (4,'1000','1100',N'Customer payment received against invoice','Payment'),
        (5,'2000','1000',N'Supplier bill payment',                    'Payment'),
        (6,'2200','1000',N'Tax remittance to authorities',            'Tax'),
        (7,'1000','3000',N'Owner capital contribution',               'Manual')
    ) t(TypeIdx, DebitNum, CreditNum, Descr, RefType) ON t.TypeIdx = (n.N - 1) % 8
    CROSS APPLY (SELECT CAST(DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 365), GETUTCDATE()) AS DATE) AS EntryDate) ed
    CROSS APPLY (SELECT CAST(50 + (ABS(CHECKSUM(NEWID())) % 995000) / 100.0 AS DECIMAL(18,2)) AS Amount) am
    CROSS APPLY (SELECT ABS(CHECKSUM(NEWID())) % 1000 AS RefRoll) rr
    CROSS APPLY (SELECT ABS(CHECKSUM(NEWID())) % 100 AS StatusRoll) sr
    WHERE n.N <= @JournalEntryCount;

    INSERT INTO JournalEntries (
        Id, TenantId, EntryNumber, FiscalPeriodId, Description, EntryDate,
        TotalDebit, TotalCredit, Status, ReferenceType, ReferenceId,
        PostedAt, PostedBy, VoidedAt, VoidedBy, VoidReason,
        CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
    )
    SELECT
        CAST('E0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), j.N), 12) AS UNIQUEIDENTIFIER),
        @TenantId,
        'JE-' + RIGHT('000000' + CONVERT(VARCHAR(6), j.N), 6),
        fp.Id,
        j.Descr,
        j.EntryDate,
        j.Amount,
        j.Amount,
        CASE WHEN j.StatusRoll < 5 THEN 2 WHEN j.StatusRoll < 10 THEN 0 ELSE 1 END,   -- 2=Voided,0=Draft,1=Posted
        j.RefType,
        j.ReferenceId,
        CASE WHEN j.StatusRoll >= 10 THEN CAST(j.EntryDate AS DATETIME2) ELSE NULL END,
        CASE WHEN j.StatusRoll >= 10 THEN 'seed-script' ELSE NULL END,
        CASE WHEN j.StatusRoll < 5 THEN CAST(DATEADD(DAY, 1, j.EntryDate) AS DATETIME2) ELSE NULL END,
        CASE WHEN j.StatusRoll < 5 THEN 'seed-script' ELSE NULL END,
        CASE WHEN j.StatusRoll < 5 THEN N'Voided during demo data generation' ELSE NULL END,
        'seed-script',
        CAST(j.EntryDate AS DATETIME2),
        'seed-script',
        CAST(j.EntryDate AS DATETIME2),
        0
    FROM #JE j
    JOIN FiscalPeriods fp ON fp.TenantId = @TenantId AND fp.FiscalYear = YEAR(j.EntryDate) AND fp.PeriodMonth = MONTH(j.EntryDate)
    WHERE NOT EXISTS (
        SELECT 1 FROM JournalEntries je
        WHERE je.Id = CAST('E0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), j.N), 12) AS UNIQUEIDENTIFIER)
    );

    INSERT INTO JournalEntryLines (
        Id, TenantId, JournalEntryId, AccountId, Description, DebitAmount, CreditAmount, LineOrder,
        CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
    )
    SELECT
        NEWID(),
        @TenantId,
        CAST('E0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), j.N), 12) AS UNIQUEIDENTIFIER),
        CASE side.Side WHEN 0 THEN da.Id ELSE ca.Id END,
        CASE side.Side WHEN 0 THEN N'Debit - ' + j.Descr ELSE N'Credit - ' + j.Descr END,
        CASE side.Side WHEN 0 THEN j.Amount ELSE 0 END,
        CASE side.Side WHEN 1 THEN j.Amount ELSE 0 END,
        side.Side,
        'seed-script',
        CAST(j.EntryDate AS DATETIME2),
        'seed-script',
        CAST(j.EntryDate AS DATETIME2),
        0
    FROM #JE j
    JOIN (VALUES (0),(1)) side(Side) ON 1 = 1
    JOIN Accounts da ON da.AccountNumber = j.DebitNum  AND da.TenantId = @TenantId AND da.IsDeleted = 0
    JOIN Accounts ca ON ca.AccountNumber = j.CreditNum AND ca.TenantId = @TenantId AND ca.IsDeleted = 0
    WHERE NOT EXISTS (
        SELECT 1 FROM JournalEntryLines jel
        WHERE jel.JournalEntryId = CAST('E0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), j.N), 12) AS UNIQUEIDENTIFIER)
    );

    DROP TABLE #JE;

    PRINT 'AccountingDB demo data: JournalEntries/JournalEntryLines seeded.';
END
ELSE
BEGIN
    PRINT 'AccountingDB demo data seed skipped: JournalEntries already has >= 1000 rows.';
END

-- ============================================================
-- 3) Invoices (1200, AR) + InvoiceLines (1 per invoice) — reference a Sales
--    SalesOrder and an Identity Customer User by formula.
-- ============================================================
IF (SELECT COUNT(*) FROM Invoices) < 1000
BEGIN
    IF OBJECT_ID('tempdb..#InvoicesBase') IS NOT NULL DROP TABLE #InvoicesBase;
    IF OBJECT_ID('tempdb..#Invoices2')    IS NOT NULL DROP TABLE #Invoices2;

    SELECT
        n.N,
        CustomerN   = 1 + ABS(CHECKSUM(NEWID())) % 1000,
        OrderN      = 1 + ABS(CHECKSUM(NEWID())) % 1000,
        InvoiceDate = CAST(DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 365), GETUTCDATE()) AS DATE),
        TermDays    = CASE (n.N % 4) WHEN 0 THEN 15 WHEN 1 THEN 30 WHEN 2 THEN 45 ELSE 60 END,
        StatusRoll  = ABS(CHECKSUM(NEWID())) % 100,
        SubTotal    = CAST(50 + (ABS(CHECKSUM(NEWID())) % 990000) / 100.0 AS DECIMAL(18,2))
    INTO #InvoicesBase
    FROM #Numbers n
    WHERE n.N <= @InvoiceCount;

    SELECT
        b.*,
        DueDate   = DATEADD(DAY, b.TermDays, b.InvoiceDate),
        TaxAmount = CAST(b.SubTotal * 0.10 AS DECIMAL(18,2))
    INTO #Invoices2
    FROM #InvoicesBase b;

    INSERT INTO Invoices (
        Id, TenantId, InvoiceNumber, CustomerId, CustomerName, CustomerEmail, OrderId,
        InvoiceDate, DueDate, SubTotal, TaxAmount, TotalAmount, AmountPaid, CurrencyCode,
        Status, Notes, SentAt, PaidAt, VoidedAt, VoidReason,
        CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
    )
    SELECT
        CAST('E0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), i.N), 12) AS UNIQUEIDENTIFIER),
        @TenantId,
        'INV-' + RIGHT('000000' + CONVERT(VARCHAR(6), i.N), 6),
        CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), i.CustomerN), 12) AS UNIQUEIDENTIFIER),
        'Customer ' + CONVERT(VARCHAR(12), i.CustomerN),
        'customer' + CONVERT(VARCHAR(12), i.CustomerN) + '@example.com',
        CAST('70000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), i.OrderN), 12) AS UNIQUEIDENTIFIER),
        i.InvoiceDate,
        i.DueDate,
        i.SubTotal,
        i.TaxAmount,
        i.SubTotal + i.TaxAmount,
        st.AmountPaid,
        'USD',
        st.Status,
        NULL,
        CASE WHEN st.Status IN (1,2,3,4) THEN DATEADD(HOUR, 2, CAST(i.InvoiceDate AS DATETIME2)) ELSE NULL END,
        st.PaidAt,
        CASE WHEN st.Status = 5 THEN CAST(DATEADD(DAY, 1, i.InvoiceDate) AS DATETIME2) ELSE NULL END,
        CASE WHEN st.Status = 5 THEN N'Voided during demo data generation' ELSE NULL END,
        'seed-script',
        CAST(i.InvoiceDate AS DATETIME2),
        'seed-script',
        CAST(ISNULL(st.PaidAt, i.InvoiceDate) AS DATETIME2),
        0
    FROM #Invoices2 i
    CROSS APPLY (
        SELECT Status = CASE
            WHEN i.StatusRoll < 5  THEN 5   -- Void
            WHEN i.StatusRoll < 12 THEN 0   -- Draft
            WHEN i.StatusRoll < 55 THEN 3   -- Paid
            WHEN i.StatusRoll < 75 THEN 2   -- Partial
            WHEN i.DueDate < CAST(GETUTCDATE() AS DATE) THEN 4  -- Overdue
            ELSE 1                          -- Issued
        END
    ) stx
    CROSS APPLY (
        SELECT
            Status = stx.Status,
            AmountPaid = CASE stx.Status
                WHEN 3 THEN i.SubTotal + i.TaxAmount
                WHEN 2 THEN CAST((i.SubTotal + i.TaxAmount) * (0.2 + (i.N % 5) * 0.1) AS DECIMAL(18,2))
                ELSE 0
            END,
            PaidAt = CASE stx.Status
                WHEN 3 THEN CAST(DATEADD(DAY, 1 + (i.N % (i.TermDays + 1)), i.InvoiceDate) AS DATETIME2)
                ELSE NULL
            END
    ) st
    WHERE NOT EXISTS (
        SELECT 1 FROM Invoices inv
        WHERE inv.Id = CAST('E0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), i.N), 12) AS UNIQUEIDENTIFIER)
    );

    INSERT INTO InvoiceLines (
        Id, InvoiceId, TenantId, Description, Quantity, UnitPrice, TaxRate, TaxAmount, LineTotal, AccountId, LineOrder
    )
    SELECT
        NEWID(),
        CAST('E0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), i.N), 12) AS UNIQUEIDENTIFIER),
        @TenantId,
        N'Goods and services rendered',
        1,
        i.SubTotal,
        10.00,
        i.TaxAmount,
        i.SubTotal + i.TaxAmount,
        rev.Id,
        0
    FROM #Invoices2 i
    JOIN Accounts rev ON rev.AccountNumber = '4000' AND rev.TenantId = @TenantId AND rev.IsDeleted = 0
    WHERE NOT EXISTS (
        SELECT 1 FROM InvoiceLines il
        WHERE il.InvoiceId = CAST('E0000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), i.N), 12) AS UNIQUEIDENTIFIER)
    );

    DROP TABLE #InvoicesBase;
    DROP TABLE #Invoices2;

    PRINT 'AccountingDB demo data: Invoices/InvoiceLines seeded.';
END
ELSE
BEGIN
    PRINT 'AccountingDB demo data seed skipped: Invoices already has >= 1000 rows.';
END

-- ============================================================
-- 4) Bills (600, AP) + BillLines (1 per bill) — reference a Purchasing
--    Supplier and PurchaseOrder by formula.
-- ============================================================
IF (SELECT COUNT(*) FROM Bills) < 500
BEGIN
    IF OBJECT_ID('tempdb..#BillsBase') IS NOT NULL DROP TABLE #BillsBase;
    IF OBJECT_ID('tempdb..#Bills2')    IS NOT NULL DROP TABLE #Bills2;

    SELECT
        n.N,
        SupplierN  = 1 + ABS(CHECKSUM(NEWID())) % 50,
        PON        = 1 + ABS(CHECKSUM(NEWID())) % 500,
        BillDate   = CAST(DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 365), GETUTCDATE()) AS DATE),
        TermDays   = CASE (n.N % 4) WHEN 0 THEN 15 WHEN 1 THEN 30 WHEN 2 THEN 45 ELSE 60 END,
        StatusRoll = ABS(CHECKSUM(NEWID())) % 100,
        SubTotal   = CAST(100 + (ABS(CHECKSUM(NEWID())) % 1490000) / 100.0 AS DECIMAL(18,2))
    INTO #BillsBase
    FROM #Numbers n
    WHERE n.N <= @BillCount;

    SELECT
        b.*,
        DueDate   = DATEADD(DAY, b.TermDays, b.BillDate),
        TaxAmount = CAST(b.SubTotal * 0.08 AS DECIMAL(18,2))
    INTO #Bills2
    FROM #BillsBase b;

    INSERT INTO Bills (
        Id, TenantId, BillNumber, SupplierId, SupplierName, POId, SupplierInvoiceRef,
        BillDate, DueDate, SubTotal, TaxAmount, TotalAmount, AmountPaid, CurrencyCode,
        Status, Notes, ApprovedAt, ApprovedBy, PaidAt,
        CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
    )
    SELECT
        CAST('E0000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.N), 12) AS UNIQUEIDENTIFIER),
        @TenantId,
        'BILL-' + RIGHT('000000' + CONVERT(VARCHAR(6), b.N), 6),
        CAST('90000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.SupplierN), 12) AS UNIQUEIDENTIFIER),
        'Supplier ' + CONVERT(VARCHAR(12), b.SupplierN),
        CAST('90000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.PON), 12) AS UNIQUEIDENTIFIER),
        'SINV-' + RIGHT('000000' + CONVERT(VARCHAR(6), b.N), 6),
        b.BillDate,
        b.DueDate,
        b.SubTotal,
        b.TaxAmount,
        b.SubTotal + b.TaxAmount,
        st.AmountPaid,
        'USD',
        st.Status,
        NULL,
        CASE WHEN st.Status IN (1,2,3,4) THEN DATEADD(HOUR, 3, CAST(b.BillDate AS DATETIME2)) ELSE NULL END,
        CASE WHEN st.Status IN (1,2,3,4) THEN 'seed-script' ELSE NULL END,
        st.PaidAt,
        'seed-script',
        CAST(b.BillDate AS DATETIME2),
        'seed-script',
        CAST(ISNULL(st.PaidAt, b.BillDate) AS DATETIME2),
        0
    FROM #Bills2 b
    CROSS APPLY (
        SELECT Status = CASE
            WHEN b.StatusRoll < 8  THEN 0   -- Draft
            WHEN b.StatusRoll < 55 THEN 3   -- Paid
            WHEN b.StatusRoll < 75 THEN 2   -- Partial
            WHEN b.DueDate < CAST(GETUTCDATE() AS DATE) THEN 4  -- Overdue
            ELSE 1                          -- Approved
        END
    ) stx
    CROSS APPLY (
        SELECT
            Status = stx.Status,
            AmountPaid = CASE stx.Status
                WHEN 3 THEN b.SubTotal + b.TaxAmount
                WHEN 2 THEN CAST((b.SubTotal + b.TaxAmount) * (0.2 + (b.N % 5) * 0.1) AS DECIMAL(18,2))
                ELSE 0
            END,
            PaidAt = CASE stx.Status
                WHEN 3 THEN CAST(DATEADD(DAY, 1 + (b.N % (b.TermDays + 1)), b.BillDate) AS DATETIME2)
                ELSE NULL
            END
    ) st
    WHERE NOT EXISTS (
        SELECT 1 FROM Bills x
        WHERE x.Id = CAST('E0000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.N), 12) AS UNIQUEIDENTIFIER)
    );

    INSERT INTO BillLines (
        Id, BillId, TenantId, Description, Quantity, UnitPrice, TaxRate, TaxAmount, LineTotal, AccountId, LineOrder
    )
    SELECT
        NEWID(),
        CAST('E0000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.N), 12) AS UNIQUEIDENTIFIER),
        @TenantId,
        N'Goods and services received',
        1,
        b.SubTotal,
        8.00,
        b.TaxAmount,
        b.SubTotal + b.TaxAmount,
        exp.Id,
        0
    FROM #Bills2 b
    JOIN Accounts exp ON exp.AccountNumber = '5100' AND exp.TenantId = @TenantId AND exp.IsDeleted = 0
    WHERE NOT EXISTS (
        SELECT 1 FROM BillLines bl
        WHERE bl.BillId = CAST('E0000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), b.N), 12) AS UNIQUEIDENTIFIER)
    );

    DROP TABLE #BillsBase;
    DROP TABLE #Bills2;

    PRINT 'AccountingDB demo data: Bills/BillLines seeded.';
END
ELSE
BEGIN
    PRINT 'AccountingDB demo data seed skipped: Bills already has >= 500 rows.';
END

DROP TABLE #Numbers;
GO
