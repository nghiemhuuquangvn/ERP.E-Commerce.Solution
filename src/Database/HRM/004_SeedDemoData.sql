-- ============================================================
-- HrmDB Seed Demo Data — HRMService (Port 5007, Code: HRM)
-- Migration 004.
--
-- Seeds:
--   1) EmployeeSnapshots — mirrors MasterDataService's 1000 seeded
--      Employees 1:1 via the deterministic GUID formula (see
--      src/Database/SEED_DATA_CONVENTIONS.md). EmployeeSnapshots is a
--      read-only Kafka projection per CLAUDE.md/002_RemoveEmployee-
--      DepartmentOwnership.sql — HRM owns no Employee aggregate, but for
--      demo purposes we populate the projection directly instead of
--      round-tripping through Kafka.
--   2) AttendanceRecords (~700 rows) — a sample of 100 employees with a
--      recent 4-week attendance history each (7 records/employee).
--   3) LeaveRequests (~200 rows) — varied leave type/status, spread over
--      the last 12 months.
--   4) PaySlips (~150 rows) — one recent monthly pay period, 150 distinct
--      employees.
--
-- Total combined AttendanceRecords + LeaveRequests + PaySlips = ~1050 rows.
--
-- Idempotent: every block is guarded by a row-count check, safe to re-run.
-- Generated set-based via numbers CTEs — no hand-written literal INSERTs.
-- ============================================================

USE HrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

-- ============================================================
-- 1) EmployeeSnapshots — one row per MasterData Employee N in [1, 1000]
--    EmployeeId formula: 40000000-0009-0000-0000-{N}  (MasterData Employee)
--    DepartmentId formula: 40000000-0007-0000-0000-{1..15} (MasterData Department, ~15 seeded)
-- ============================================================
IF (SELECT COUNT(*) FROM EmployeeSnapshots) < 1000
BEGIN
    ;WITH Numbers AS (
        SELECT 1 AS N
        UNION ALL
        SELECT N + 1 FROM Numbers WHERE N < 1000
    ),
    FirstNames AS (
        SELECT FirstName, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
        FROM (VALUES
            ('James'),('Mary'),('John'),('Patricia'),('Robert'),('Jennifer'),('Michael'),('Linda'),('William'),('Elizabeth'),
            ('David'),('Barbara'),('Richard'),('Susan'),('Joseph'),('Jessica'),('Thomas'),('Sarah'),('Charles'),('Karen'),
            ('Minh'),('Lan'),('Hoa'),('Nam'),('Trang'),('Huy'),('Linh'),('Duc'),('Mai'),('Tuan')
        ) AS F(FirstName)
    ),
    LastNames AS (
        SELECT LastName, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
        FROM (VALUES
            ('Smith'),('Johnson'),('Williams'),('Brown'),('Jones'),('Garcia'),('Miller'),('Davis'),('Nguyen'),('Tran'),
            ('Le'),('Pham'),('Hoang'),('Phan'),('Vu'),('Dang'),('Bui'),('Do'),('Ho'),('Ngo')
        ) AS L(LastName)
    )
    INSERT INTO EmployeeSnapshots
        (Id, EmployeeId, TenantId, EmployeeNumber, FullName, Email, EmploymentStatus, DepartmentId, CreatedAt, UpdatedAt, CreatedBy, IsDeleted)
    SELECT
        EmpId,
        EmpId,
        @TenantId,
        'EMP' + RIGHT('00000' + CONVERT(VARCHAR(5), n.N), 5),
        fn.FirstName + ' ' + ln.LastName,
        LOWER(fn.FirstName + '.' + ln.LastName + CONVERT(VARCHAR(5), n.N)) + '@erp-demo.com',
        CASE WHEN n.N % 50 = 0 THEN 3   -- Terminated
             WHEN n.N % 17 = 0 THEN 2   -- Suspended
             WHEN n.N % 9  = 0 THEN 1   -- OnLeave
             ELSE 0 END,                -- Active
        CAST('40000000-0007-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N - 1) % 15) + 1), 12) AS UNIQUEIDENTIFIER),
        DATEADD(DAY, -1 * ((n.N * 37) % 1000), SYSUTCDATETIME()),
        SYSUTCDATETIME(),
        'seed-script',
        0
    FROM Numbers n
    CROSS APPLY (SELECT CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER) AS EmpId) e
    JOIN FirstNames fn ON fn.rn = ((n.N - 1) % 30) + 1
    JOIN LastNames  ln ON ln.rn = ((n.N * 3 - 1) % 20) + 1
    OPTION (MAXRECURSION 0);
END
GO

-- ============================================================
-- 2) AttendanceRecords — 100 sampled employees x 7 recent workdays = 700 rows
--    Employee sample: N = ((i-1)*10)+1 for i in [1,100] -> N = 1,11,21,...,991
-- ============================================================
DECLARE @TenantId2 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF (SELECT COUNT(*) FROM AttendanceRecords) < 700
BEGIN
    ;WITH EmpIdx AS (
        SELECT 1 AS i
        UNION ALL
        SELECT i + 1 FROM EmpIdx WHERE i < 100
    ),
    DayIdx AS (
        SELECT 0 AS j
        UNION ALL
        SELECT j + 1 FROM DayIdx WHERE j < 6
    ),
    Base AS (
        SELECT
            e.i,
            d.j,
            CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((e.i - 1) * 10) + 1), 12) AS UNIQUEIDENTIFIER) AS EmployeeId,
            CAST(DATEADD(DAY, -1 * (d.j * 4), SYSUTCDATETIME()) AS DATE) AS WorkDate,
            8 + (e.i % 3) AS ShiftLen,          -- 8, 9 or 10 hr shift length variety
            (e.i % 45) AS StartMinuteOffset
        FROM EmpIdx e CROSS JOIN DayIdx d
    )
    INSERT INTO AttendanceRecords
        (Id, TenantId, EmployeeId, CheckInTime, CheckOutTime, TotalHours, OvertimeHours, Source, CreatedAt, [Date], [Status], CreatedBy, UpdatedAt, IsDeleted)
    SELECT
        NEWID(),
        @TenantId2,
        EmployeeId,
        DATEADD(MINUTE, StartMinuteOffset, DATEADD(HOUR, 8, CAST(WorkDate AS DATETIME2))) AS CheckInTime,
        DATEADD(MINUTE, StartMinuteOffset, DATEADD(HOUR, 8 + ShiftLen, CAST(WorkDate AS DATETIME2))) AS CheckOutTime,
        CAST(ShiftLen AS DECIMAL(6,2)) AS TotalHours,
        CASE WHEN ShiftLen > 8 THEN CAST(ShiftLen - 8 AS DECIMAL(5,2)) ELSE 0 END AS OvertimeHours,
        i % 4 AS Source,
        SYSUTCDATETIME(),
        WorkDate,
        CASE WHEN (i + j) % 11 = 0 THEN 2 WHEN (i + j) % 13 = 0 THEN 3 ELSE 0 END,  -- occasional Late (2) / HalfDay (3), else Present (0)
        'seed-script',
        SYSUTCDATETIME(),
        0
    FROM Base
    OPTION (MAXRECURSION 0);
END
GO

-- ============================================================
-- 3) LeaveRequests — 200 rows, employee picked pseudo-randomly per row
-- ============================================================
DECLARE @TenantId3 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF (SELECT COUNT(*) FROM LeaveRequests) < 200
BEGIN
    ;WITH Numbers AS (
        SELECT 1 AS K
        UNION ALL
        SELECT K + 1 FROM Numbers WHERE K < 200
    ),
    Reasons AS (
        SELECT Reason, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn
        FROM (VALUES
            ('Annual family vacation'),('Feeling unwell'),('Medical appointment'),('Personal matters'),
            ('Family emergency'),('Wedding attendance'),('Relocation'),('Childcare'),('Rest and recovery'),('Other')
        ) AS R(Reason)
    ),
    Picked AS (
        SELECT
            n.K,
            CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), 1 + (ABS(CHECKSUM(NEWID())) % 1000)), 12) AS UNIQUEIDENTIFIER) AS EmployeeId,
            n.K % 6 AS LeaveTypeVal,
            DATEADD(DAY, -1 * (ABS(CHECKSUM(NEWID())) % 365), SYSUTCDATETIME()) AS StartDate,
            1 + (ABS(CHECKSUM(NEWID())) % 5) AS DaysVal,
            CASE (n.K % 10)
                WHEN 0 THEN 2   -- Rejected
                WHEN 1 THEN 3   -- Cancelled
                WHEN 2 THEN 0   -- Pending
                ELSE 1 END AS StatusVal, -- Approved (majority)
            r.Reason
        FROM Numbers n
        JOIN Reasons r ON r.rn = ((n.K - 1) % 10) + 1
    )
    INSERT INTO LeaveRequests
        (Id, TenantId, EmployeeId, LeaveType, StartDate, EndDate, Days, HalfDay, Reason, Status,
         ApprovedByUserId, ApprovedAt, RejectionReason,
         LeaveRequestNumber, LeaveTypeId, DaysRequested, LeaveStatus,
         CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        NEWID(),
        @TenantId3,
        EmployeeId,
        LeaveTypeVal,
        CAST(StartDate AS DATE),
        CAST(DATEADD(DAY, DaysVal - 1, StartDate) AS DATE),
        CAST(DaysVal AS DECIMAL(5,1)),
        CASE WHEN K % 13 = 0 THEN 1 ELSE 0 END,
        Reason,
        StatusVal,
        CASE WHEN StatusVal = 1 THEN CONVERT(NVARCHAR(100), CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), 1 + (K % 50)), 12) AS UNIQUEIDENTIFIER)) ELSE NULL END,
        CASE WHEN StatusVal = 1 THEN DATEADD(DAY, 1, StartDate) ELSE NULL END,
        CASE WHEN StatusVal = 2 THEN 'Insufficient leave balance / scheduling conflict' ELSE NULL END,
        'LR' + RIGHT('000000' + CONVERT(VARCHAR(6), K), 6),
        LeaveTypeVal,
        DaysVal,
        StatusVal,
        'seed-script',
        SYSUTCDATETIME(),
        NULL,
        SYSUTCDATETIME(),
        0
    FROM Picked
    OPTION (MAXRECURSION 0);
END
GO

-- ============================================================
-- 4) PaySlips — 150 rows for the most recently completed calendar month,
--    one per employee (distinct N via a step-7 modulo walk so the
--    (EmployeeId, PeriodStart, PeriodEnd) unique index is never violated)
-- ============================================================
DECLARE @TenantId4 UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @PeriodStart DATE = DATEFROMPARTS(YEAR(DATEADD(MONTH, -1, SYSUTCDATETIME())), MONTH(DATEADD(MONTH, -1, SYSUTCDATETIME())), 1);
DECLARE @PeriodEnd   DATE = EOMONTH(@PeriodStart);

IF (SELECT COUNT(*) FROM PaySlips) < 150
BEGIN
    ;WITH Numbers AS (
        SELECT 1 AS K
        UNION ALL
        SELECT K + 1 FROM Numbers WHERE K < 150
    ),
    Base AS (
        SELECT
            K,
            CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), ((K * 7 - 1) % 1000) + 1), 12) AS UNIQUEIDENTIFIER) AS EmployeeId,
            CAST(800 + (K % 25) * 120 AS DECIMAL(18,2)) AS BasicSalaryVal,
            CAST(50 + (K % 10) * 20 AS DECIMAL(18,2)) AS AllowancesVal,
            CAST(K % 8 AS DECIMAL(6,2)) AS OvertimeHoursVal,
            CAST(20 + (K % 5) * 10 AS DECIMAL(18,2)) AS DeductionsVal,
            CASE (K % 20) WHEN 0 THEN 0 WHEN 1 THEN 1 ELSE 2 END AS StatusVal -- mostly PaidOut(2), some Issued(1)/Draft(0)
        FROM Numbers
    )
    INSERT INTO PaySlips
        (Id, TenantId, EmployeeId, PeriodStart, PeriodEnd, BasicSalary, Allowances, Deductions, TaxAmount, SocialInsurance,
         CurrencyCode, Status, IssuedAt, IssuedBy, PaidAt, PaymentRef,
         PaySlipNumber, [Year], [Month], GrossSalary, OvertimeHours, OvertimePay, PaymentDate, PaymentStatus,
         CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        NEWID(),
        @TenantId4,
        EmployeeId,
        @PeriodStart,
        @PeriodEnd,
        BasicSalaryVal,
        AllowancesVal,
        DeductionsVal,
        CAST((BasicSalaryVal + AllowancesVal) * 0.1 AS DECIMAL(18,2)),
        CAST(BasicSalaryVal * 0.08 AS DECIMAL(18,2)),
        'USD',
        StatusVal,
        CASE WHEN StatusVal >= 1 THEN DATEADD(DAY, 2, @PeriodEnd) ELSE NULL END,
        'seed-script',
        CASE WHEN StatusVal = 2 THEN DATEADD(DAY, 10, @PeriodEnd) ELSE NULL END,
        CASE WHEN StatusVal = 2 THEN 'PAY-REF-' + CONVERT(VARCHAR(6), K) ELSE NULL END,
        'PS' + CONVERT(VARCHAR(4), YEAR(@PeriodStart)) + RIGHT('00' + CONVERT(VARCHAR(2), MONTH(@PeriodStart)), 2) + RIGHT('0000' + CONVERT(VARCHAR(4), K), 4),
        YEAR(@PeriodStart),
        MONTH(@PeriodStart),
        BasicSalaryVal + AllowancesVal + CAST(OvertimeHoursVal * 15 AS DECIMAL(18,2)),
        OvertimeHoursVal,
        CAST(OvertimeHoursVal * 15 AS DECIMAL(18,2)),
        CASE WHEN StatusVal = 2 THEN DATEADD(DAY, 10, @PeriodEnd) ELSE NULL END,
        StatusVal,
        'seed-script',
        SYSUTCDATETIME(),
        NULL,
        SYSUTCDATETIME(),
        0
    FROM Base
    OPTION (MAXRECURSION 0);
END
GO
