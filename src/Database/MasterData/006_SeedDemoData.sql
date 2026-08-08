-- ============================================================
-- MasterDataDB Migration 006 — Demo Seed Data
-- Seeds Countries, Provinces, Districts, Wards, UnitsOfMeasure,
-- Currencies, Departments, Positions, Employees.
--
-- Follows the MANDATORY deterministic-GUID formula defined in
-- src/Database/SEED_DATA_CONVENTIONS.md:
--   {PrefixHigh}-{PrefixLow}-0000-0000-{N padded to 12 hex digits}
-- Registry prefixes used here:
--   Country          40000000-0001-...
--   Province         40000000-0002-...
--   District         40000000-0003-...
--   Ward             40000000-0004-...
--   UnitOfMeasure    40000000-0005-...
--   Currency         40000000-0006-...
--   Department       40000000-0007-...
--   Position         40000000-0008-...
--   Employee         40000000-0009-...
-- TenantId is always the fixed system tenant '00000000-0000-0000-0000-000000000001'.
--
-- Idempotent / safely re-runnable: each block is guarded by a
-- COUNT(*) < target check, and every INSERT additionally filters
-- out rows whose deterministic Id already exists.
-- ============================================================

USE MasterDataDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Numbers helper (#Numbers): 1..10000, persists across GO batches
-- for the remainder of this script (temp tables survive batch
-- separators within the same session).
-- ============================================================
IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
GO

;WITH L1 AS (
    SELECT TOP (100) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
    FROM master.dbo.spt_values
)
SELECT TOP (10000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
INTO #Numbers
FROM L1 a CROSS JOIN L1 b;

CREATE UNIQUE CLUSTERED INDEX IX_Numbers_N ON #Numbers(N);
GO

-- ============================================================
-- Countries (~10): Vietnam first, plus real trading partners
-- ============================================================
IF (SELECT COUNT(*) FROM Countries) < 10
BEGIN
    ;WITH Src AS (
        SELECT * FROM (VALUES
            (1,  N'VN', N'Vietnam',        N'+84'),
            (2,  N'US', N'United States',  N'+1'),
            (3,  N'CN', N'China',          N'+86'),
            (4,  N'JP', N'Japan',          N'+81'),
            (5,  N'KR', N'South Korea',    N'+82'),
            (6,  N'SG', N'Singapore',      N'+65'),
            (7,  N'TH', N'Thailand',       N'+66'),
            (8,  N'DE', N'Germany',        N'+49'),
            (9,  N'FR', N'France',         N'+33'),
            (10, N'AU', N'Australia',      N'+61')
        ) AS v(N, Code, Name, PhoneCode)
    )
    INSERT INTO Countries (Id, TenantId, Code, Name, PhoneCode, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        CAST('40000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        s.Code, s.Name, s.PhoneCode,
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM Src s
    WHERE NOT EXISTS (
        SELECT 1 FROM Countries c
        WHERE c.Id = CAST('40000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER)
    );
END
GO

-- ============================================================
-- Provinces (~30): children of Vietnam (Country N=1)
-- ============================================================
IF (SELECT COUNT(*) FROM Provinces) < 30
BEGIN
    ;WITH Src AS (
        SELECT * FROM (VALUES
            (1,  N'HN',  N'Hà Nội'),
            (2,  N'HCM', N'Hồ Chí Minh'),
            (3,  N'HP',  N'Hải Phòng'),
            (4,  N'DNA', N'Đà Nẵng'),
            (5,  N'CTH', N'Cần Thơ'),
            (6,  N'AGI', N'An Giang'),
            (7,  N'BRV', N'Bà Rịa - Vũng Tàu'),
            (8,  N'BGI', N'Bắc Giang'),
            (9,  N'BNI', N'Bắc Ninh'),
            (10, N'BLI', N'Bạc Liêu'),
            (11, N'BTR', N'Bến Tre'),
            (12, N'BDI', N'Bình Định'),
            (13, N'BDU', N'Bình Dương'),
            (14, N'BPH', N'Bình Phước'),
            (15, N'BTH', N'Bình Thuận'),
            (16, N'CMA', N'Cà Mau'),
            (17, N'CBA', N'Cao Bằng'),
            (18, N'DLA', N'Đắk Lắk'),
            (19, N'DNO', N'Đắk Nông'),
            (20, N'DBI', N'Điện Biên'),
            (21, N'DNI', N'Đồng Nai'),
            (22, N'DTH', N'Đồng Tháp'),
            (23, N'GLA', N'Gia Lai'),
            (24, N'HGI', N'Hà Giang'),
            (25, N'HNM', N'Hà Nam'),
            (26, N'HTI', N'Hà Tĩnh'),
            (27, N'HDU', N'Hải Dương'),
            (28, N'HGG', N'Hậu Giang'),
            (29, N'HBI', N'Hòa Bình'),
            (30, N'HYE', N'Hưng Yên')
        ) AS v(N, Code, Name)
    )
    INSERT INTO Provinces (Id, TenantId, CountryId, Code, Name, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        CAST('40000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        CAST('40000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), 1), 12) AS UNIQUEIDENTIFIER), -- Vietnam
        s.Code, s.Name,
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM Src s
    WHERE NOT EXISTS (
        SELECT 1 FROM Provinces p
        WHERE p.Id = CAST('40000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER)
    );
END
GO

-- ============================================================
-- Districts (~150): 5 per Province (30 provinces x 5 = 150)
-- District N = (ProvinceN - 1) * 5 + LevelN
-- ============================================================
IF (SELECT COUNT(*) FROM Districts) < 150
BEGIN
    ;WITH Provs AS (
        SELECT TOP (30) N AS PN FROM #Numbers ORDER BY N
    ),
    Levels AS (
        SELECT * FROM (VALUES
            (1, N'Q1',  N'Quận 1'),
            (2, N'Q2',  N'Quận 2'),
            (3, N'Q3',  N'Quận 3'),
            (4, N'H1',  N'Huyện Ngoại Thành'),
            (5, N'TX1', N'Thị Xã Trung Tâm')
        ) AS v(LN, Code, Name)
    )
    INSERT INTO Districts (Id, TenantId, ProvinceId, Code, Name, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        CAST('40000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), (p.PN - 1) * 5 + l.LN), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        CAST('40000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), p.PN), 12) AS UNIQUEIDENTIFIER),
        l.Code, l.Name,
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM Provs p CROSS JOIN Levels l
    WHERE NOT EXISTS (
        SELECT 1 FROM Districts d
        WHERE d.Id = CAST('40000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), (p.PN - 1) * 5 + l.LN), 12) AS UNIQUEIDENTIFIER)
    );
END
GO

-- ============================================================
-- Wards (~600): 4 per District (150 districts x 4 = 600)
-- Ward N = (DistrictN - 1) * 4 + LevelN
-- ============================================================
IF (SELECT COUNT(*) FROM Wards) < 600
BEGIN
    ;WITH Dists AS (
        SELECT TOP (150) N AS DN FROM #Numbers ORDER BY N
    ),
    Levels AS (
        SELECT * FROM (VALUES
            (1, N'P1', N'Phường 1'),
            (2, N'P2', N'Phường 2'),
            (3, N'P3', N'Phường 3'),
            (4, N'P4', N'Phường 4')
        ) AS v(LN, Code, Name)
    )
    INSERT INTO Wards (Id, TenantId, DistrictId, Code, Name, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        CAST('40000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), (d.DN - 1) * 4 + l.LN), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        CAST('40000000-0003-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), d.DN), 12) AS UNIQUEIDENTIFIER),
        l.Code, l.Name,
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM Dists d CROSS JOIN Levels l
    WHERE NOT EXISTS (
        SELECT 1 FROM Wards w
        WHERE w.Id = CAST('40000000-0004-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), (d.DN - 1) * 4 + l.LN), 12) AS UNIQUEIDENTIFIER)
    );
END
GO

-- ============================================================
-- UnitsOfMeasure (~20)
-- UnitType: 0=Weight, 1=Volume, 2=Length, 3=Count, 4=Area
-- ============================================================
IF (SELECT COUNT(*) FROM UnitsOfMeasure) < 20
BEGIN
    ;WITH Src AS (
        SELECT * FROM (VALUES
            (1,  N'PCS',   N'Piece',         N'pcs',  3, NULL, 1.0),
            (2,  N'BOX',   N'Box',           N'box',  3, NULL, 1.0),
            (3,  N'DOZEN', N'Dozen',         N'dz',   3, 1,    12.0),
            (4,  N'SET',   N'Set',           N'set',  3, NULL, 1.0),
            (5,  N'PAIR',  N'Pair',          N'pair', 3, NULL, 1.0),
            (6,  N'ROLL',  N'Roll',          N'roll', 3, NULL, 1.0),
            (7,  N'BAG',   N'Bag',           N'bag',  3, NULL, 1.0),
            (8,  N'KG',    N'Kilogram',      N'kg',   0, NULL, 1.0),
            (9,  N'G',     N'Gram',          N'g',    0, 8,    0.001),
            (10, N'TON',   N'Ton',           N't',    0, 8,    1000.0),
            (11, N'LB',    N'Pound',         N'lb',   0, NULL, 1.0),
            (12, N'L',     N'Liter',         N'L',    1, NULL, 1.0),
            (13, N'ML',    N'Milliliter',    N'mL',   1, 12,   0.001),
            (14, N'GAL',   N'Gallon',        N'gal',  1, NULL, 1.0),
            (15, N'M',     N'Meter',         N'm',    2, NULL, 1.0),
            (16, N'CM',    N'Centimeter',    N'cm',   2, 15,   0.01),
            (17, N'MM',    N'Millimeter',    N'mm',   2, 15,   0.001),
            (18, N'KM',    N'Kilometer',     N'km',   2, 15,   1000.0),
            (19, N'M2',    N'Square Meter',  N'm2',   4, NULL, 1.0),
            (20, N'SQFT',  N'Square Foot',   N'ft2',  4, NULL, 1.0)
        ) AS v(N, Code, Name, Symbol, UnitType, BaseN, Factor)
    )
    INSERT INTO UnitsOfMeasure (Id, TenantId, Code, Name, Symbol, UnitType, BaseUnitId, ConversionFactor, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        CAST('40000000-0005-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        s.Code, s.Name, s.Symbol, s.UnitType,
        CASE WHEN s.BaseN IS NULL THEN NULL
             ELSE CAST('40000000-0005-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.BaseN), 12) AS UNIQUEIDENTIFIER)
        END,
        s.Factor,
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM Src s
    WHERE NOT EXISTS (
        SELECT 1 FROM UnitsOfMeasure u
        WHERE u.Id = CAST('40000000-0005-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER)
    );
END
GO

-- ============================================================
-- Currencies (~5): VND (default), USD, EUR, JPY, CNY
-- ============================================================
IF (SELECT COUNT(*) FROM Currencies) < 5
BEGIN
    ;WITH Src AS (
        SELECT * FROM (VALUES
            (1, N'VND', N'Vietnamese Dong', N'₫', 0),
            (2, N'USD', N'US Dollar',       N'$', 2),
            (3, N'EUR', N'Euro',            N'€', 2),
            (4, N'JPY', N'Japanese Yen',    N'¥', 0),
            (5, N'CNY', N'Chinese Yuan',    N'¥', 2)
        ) AS v(N, Code, Name, Symbol, Decimals)
    )
    INSERT INTO Currencies (Id, TenantId, Code, Name, Symbol, DecimalPlaces, IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        CAST('40000000-0006-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        s.Code, s.Name, s.Symbol, s.Decimals, 1,
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM Src s
    WHERE NOT EXISTS (
        SELECT 1 FROM Currencies c
        WHERE c.Id = CAST('40000000-0006-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER)
    );
END
GO

-- Default currency setting for the system tenant (VND) — local-only
-- single row, not part of the cross-service GUID registry, so NEWID() is fine.
IF NOT EXISTS (SELECT 1 FROM TenantCurrencySettings WHERE TenantId = '00000000-0000-0000-0000-000000000001')
    INSERT INTO TenantCurrencySettings (Id, TenantId, DefaultCurrencyCode, ExchangeRateSource, DecimalDigits, CreatedAt, UpdatedAt)
    VALUES (NEWID(), '00000000-0000-0000-0000-000000000001', N'VND', N'Manual', 0, GETUTCDATE(), GETUTCDATE());
GO

IF NOT EXISTS (SELECT 1 FROM SystemCurrencySettings)
    INSERT INTO SystemCurrencySettings (Id, TenantId, MultiCurrencyAllowed, CreatedAt, UpdatedAt)
    VALUES (NEWID(), '00000000-0000-0000-0000-000000000000', 1, GETUTCDATE(), GETUTCDATE());
GO

-- ============================================================
-- Departments (~15)
-- ============================================================
IF (SELECT COUNT(*) FROM Departments) < 15
BEGIN
    ;WITH Src AS (
        SELECT * FROM (VALUES
            (1,  N'SAL',  N'Sales'),
            (2,  N'WH',   N'Warehouse'),
            (3,  N'ACC',  N'Accounting'),
            (4,  N'HR',   N'Human Resources'),
            (5,  N'IT',   N'Information Technology'),
            (6,  N'PUR',  N'Purchasing'),
            (7,  N'CRM',  N'Customer Relationship Management'),
            (8,  N'MKT',  N'Marketing'),
            (9,  N'CS',   N'Customer Service'),
            (10, N'OPS',  N'Operations'),
            (11, N'LEG',  N'Legal'),
            (12, N'FIN',  N'Finance'),
            (13, N'RND',  N'Research and Development'),
            (14, N'QA',   N'Quality Assurance'),
            (15, N'EXEC', N'Executive Office')
        ) AS v(N, Code, Name)
    )
    INSERT INTO Departments (Id, TenantId, DepartmentName, Code, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        CAST('40000000-0007-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        s.Name, s.Code,
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM Src s
    WHERE NOT EXISTS (
        SELECT 1 FROM Departments dep
        WHERE dep.Id = CAST('40000000-0007-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), s.N), 12) AS UNIQUEIDENTIFIER)
    );
END
GO

-- ============================================================
-- #DeptMeta: department metadata (Code/Name/PosCount/PosOffset)
-- used by both the Positions insert and the Employees insert
-- below to compute correct global Position sequence numbers.
-- Mirrors the same 15 departments seeded above.
-- ============================================================
IF OBJECT_ID('tempdb..#DeptMeta') IS NOT NULL DROP TABLE #DeptMeta;
GO

;WITH Src AS (
    SELECT * FROM (VALUES
        (1,  N'SAL',  N'Sales',                             4),
        (2,  N'WH',   N'Warehouse',                         3),
        (3,  N'ACC',  N'Accounting',                        3),
        (4,  N'HR',   N'Human Resources',                   3),
        (5,  N'IT',   N'Information Technology',             3),
        (6,  N'PUR',  N'Purchasing',                        2),
        (7,  N'CRM',  N'Customer Relationship Management',  3),
        (8,  N'MKT',  N'Marketing',                         3),
        (9,  N'CS',   N'Customer Service',                  2),
        (10, N'OPS',  N'Operations',                        3),
        (11, N'LEG',  N'Legal',                             2),
        (12, N'FIN',  N'Finance',                           3),
        (13, N'RND',  N'Research and Development',           3),
        (14, N'QA',   N'Quality Assurance',                 2),
        (15, N'EXEC', N'Executive Office',                  2)
    ) AS v(DeptN, Code, Name, PosCount)
)
SELECT
    DeptN, Code, Name, PosCount,
    CAST(ISNULL(SUM(PosCount) OVER (ORDER BY DeptN ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS INT) AS PosOffset
INTO #DeptMeta
FROM Src;

CREATE UNIQUE CLUSTERED INDEX IX_DeptMeta_DeptN ON #DeptMeta(DeptN);
GO

-- ============================================================
-- Positions (~40, 2-4 per Department = 41 total)
-- Position N = PosOffset(dept) + LevelN
-- ============================================================
IF (SELECT COUNT(*) FROM Positions) < 40
BEGIN
    ;WITH Levels AS (
        SELECT * FROM (VALUES
            (1, N'MGR', N'Manager'),
            (2, N'SSP', N'Senior Specialist'),
            (3, N'SPC', N'Specialist'),
            (4, N'AST', N'Assistant')
        ) AS v(LN, LCode, LTitle)
    )
    INSERT INTO Positions (Id, TenantId, Title, Code, DepartmentId, IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted)
    SELECT
        CAST('40000000-0008-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), dm.PosOffset + l.LN), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        dm.Name + N' ' + l.LTitle,
        dm.Code + N'-' + l.LCode,
        CAST('40000000-0007-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), dm.DeptN), 12) AS UNIQUEIDENTIFIER),
        1,
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM #DeptMeta dm
    CROSS JOIN Levels l
    WHERE l.LN <= dm.PosCount
      AND NOT EXISTS (
        SELECT 1 FROM Positions p
        WHERE p.Id = CAST('40000000-0008-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), dm.PosOffset + l.LN), 12) AS UNIQUEIDENTIFIER)
      );
END
GO

-- ============================================================
-- Employees (1000+, target 1050)
-- DepartmentId / PositionId computed pseudo-randomly (deterministic
-- hash of EmpN) from the Department/Position registries above.
-- ============================================================
IF (SELECT COUNT(*) FROM Employees) < 1000
BEGIN
    ;WITH LastNames AS (
        SELECT * FROM (VALUES
            (1,  N'Nguyễn', N'nguyen'),
            (2,  N'Trần',   N'tran'),
            (3,  N'Lê',     N'le'),
            (4,  N'Phạm',   N'pham'),
            (5,  N'Hoàng',  N'hoang'),
            (6,  N'Huỳnh',  N'huynh'),
            (7,  N'Phan',   N'phan'),
            (8,  N'Vũ',     N'vu'),
            (9,  N'Võ',     N'vo'),
            (10, N'Đặng',   N'dang'),
            (11, N'Bùi',    N'bui'),
            (12, N'Đỗ',     N'do'),
            (13, N'Hồ',     N'ho'),
            (14, N'Ngô',    N'ngo'),
            (15, N'Dương',  N'duong'),
            (16, N'Lý',     N'ly')
        ) AS v(LIdx, LastName, LastSlug)
    ),
    FirstNames AS (
        SELECT * FROM (VALUES
            (1,  N'Văn An',    N'van.an'),
            (2,  N'Thị Hương', N'thi.huong'),
            (3,  N'Minh Tuấn', N'minh.tuan'),
            (4,  N'Thu Trang', N'thu.trang'),
            (5,  N'Quốc Bảo',  N'quoc.bao'),
            (6,  N'Ngọc Anh',  N'ngoc.anh'),
            (7,  N'Hoài Nam',  N'hoai.nam'),
            (8,  N'Thị Lan',   N'thi.lan'),
            (9,  N'Văn Đức',   N'van.duc'),
            (10, N'Thị Mai',   N'thi.mai'),
            (11, N'Minh Khoa', N'minh.khoa'),
            (12, N'Thị Thảo',  N'thi.thao'),
            (13, N'Quang Huy', N'quang.huy'),
            (14, N'Thị Hoa',   N'thi.hoa'),
            (15, N'Văn Long',  N'van.long'),
            (16, N'Thị Linh',  N'thi.linh'),
            (17, N'Minh Châu', N'minh.chau'),
            (18, N'Thị Nga',   N'thi.nga'),
            (19, N'Quốc Anh',  N'quoc.anh'),
            (20, N'Thị Yến',   N'thi.yen'),
            (21, N'Văn Hùng',  N'van.hung'),
            (22, N'Thị Hà',    N'thi.ha'),
            (23, N'Minh Đức',  N'minh.duc'),
            (24, N'Thị Thu',   N'thi.thu'),
            (25, N'Quang Minh',N'quang.minh'),
            (26, N'Thị Kim',   N'thi.kim'),
            (27, N'Văn Phong', N'van.phong'),
            (28, N'Thị Ngọc',  N'thi.ngoc'),
            (29, N'Minh Hiếu', N'minh.hieu'),
            (30, N'Thị Xuân',  N'thi.xuan')
        ) AS v(FIdx, FirstName, FirstSlug)
    ),
    Emp AS (
        SELECT
            n.N AS EmpN,
            ((n.N * 7 + 3) % 15) + 1  AS DeptN,
            ((n.N * 3 + 1) % 16) + 1  AS LIdx,
            ((n.N * 5 + 2) % 30) + 1  AS FIdx,
            (n.N * 13 + 5)            AS PosRandSeed,
            (n.N % 20)                AS StatusRand,
            ((n.N * 47) % 20)         AS TypeRand
        FROM #Numbers n
        WHERE n.N <= 1050
    )
    INSERT INTO Employees (
        Id, TenantId, EmployeeNumber, FirstName, LastName, Email, Phone, DateOfHire,
        DepartmentId, PositionId, EmploymentStatus, EmploymentType,
        CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
    )
    SELECT
        CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), e.EmpN), 12) AS UNIQUEIDENTIFIER),
        '00000000-0000-0000-0000-000000000001',
        N'EMP' + RIGHT('000000' + CONVERT(VARCHAR(10), e.EmpN), 6),
        fn.FirstName,
        ln.LastName,
        LOWER(ln.LastSlug + N'.' + fn.FirstSlug) + CONVERT(VARCHAR(10), e.EmpN) + N'@erp-demo.local',
        N'09' + RIGHT('00000000' + CONVERT(VARCHAR(10), 10000000 + e.EmpN), 8),
        DATEADD(DAY, (e.EmpN * 37) % 2190, '2020-01-01'),
        CAST('40000000-0007-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), e.DeptN), 12) AS UNIQUEIDENTIFIER),
        CAST('40000000-0008-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), dm.PosOffset + ((e.PosRandSeed % dm.PosCount) + 1)), 12) AS UNIQUEIDENTIFIER),
        CASE WHEN e.StatusRand < 17 THEN 0 WHEN e.StatusRand < 19 THEN 1 ELSE 2 END, -- 85% Active, 10% OnLeave, 5% Terminated
        CASE WHEN e.TypeRand < 15 THEN 0 WHEN e.TypeRand < 18 THEN 1 ELSE 2 END,    -- 75% FullTime, 15% PartTime, 10% Contract
        N'seed-script', GETUTCDATE(), N'seed-script', GETUTCDATE(), 0
    FROM Emp e
    JOIN FirstNames fn ON fn.FIdx = e.FIdx
    JOIN LastNames ln ON ln.LIdx = e.LIdx
    JOIN #DeptMeta dm ON dm.DeptN = e.DeptN
    WHERE NOT EXISTS (
        SELECT 1 FROM Employees emp
        WHERE emp.Id = CAST('40000000-0009-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), e.EmpN), 12) AS UNIQUEIDENTIFIER)
    );
END
GO

-- ============================================================
-- Cleanup temp helpers
-- ============================================================
IF OBJECT_ID('tempdb..#DeptMeta') IS NOT NULL DROP TABLE #DeptMeta;
IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
GO

DECLARE @CountriesCnt INT, @ProvincesCnt INT, @DistrictsCnt INT, @WardsCnt INT,
        @UomCnt INT, @CurrenciesCnt INT, @DepartmentsCnt INT, @PositionsCnt INT, @EmployeesCnt INT;
SELECT @CountriesCnt = COUNT(*) FROM Countries;
SELECT @ProvincesCnt = COUNT(*) FROM Provinces;
SELECT @DistrictsCnt = COUNT(*) FROM Districts;
SELECT @WardsCnt = COUNT(*) FROM Wards;
SELECT @UomCnt = COUNT(*) FROM UnitsOfMeasure;
SELECT @CurrenciesCnt = COUNT(*) FROM Currencies;
SELECT @DepartmentsCnt = COUNT(*) FROM Departments;
SELECT @PositionsCnt = COUNT(*) FROM Positions;
SELECT @EmployeesCnt = COUNT(*) FROM Employees;
PRINT 'MasterDataDB seed 006 complete: Countries=' + CAST(@CountriesCnt AS VARCHAR(10))
    + ', Provinces=' + CAST(@ProvincesCnt AS VARCHAR(10))
    + ', Districts=' + CAST(@DistrictsCnt AS VARCHAR(10))
    + ', Wards=' + CAST(@WardsCnt AS VARCHAR(10))
    + ', UnitsOfMeasure=' + CAST(@UomCnt AS VARCHAR(10))
    + ', Currencies=' + CAST(@CurrenciesCnt AS VARCHAR(10))
    + ', Departments=' + CAST(@DepartmentsCnt AS VARCHAR(10))
    + ', Positions=' + CAST(@PositionsCnt AS VARCHAR(10))
    + ', Employees=' + CAST(@EmployeesCnt AS VARCHAR(10));
GO
