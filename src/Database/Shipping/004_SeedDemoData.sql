-- ============================================================
-- ShippingDB Migration 004 — Demo Seed Data
-- Adds a few more Carriers (5 total including the existing default) and
-- seeds 1000+ Shipment rows, for the cross-service demo-data effort
-- described in src/Database/SEED_DATA_CONVENTIONS.md.
--
-- GUID formula (mandatory, see conventions doc):
--   Shipments.Id = B0000000-0001-0000-0000-{N}            (this service's own registry)
--   Shipments.OrderId = 70000000-0001-0000-0000-{N}        (Sales SalesOrder, N in [1,1000])
--
-- Coverage design (deterministic via OrderN % 100):
--   residues  0..15 (160 orders) -> 2 shipments each: 1st Failed/Returned,
--                                   2nd re-shipped & Delivered        -> 320 rows
--   residues 16..35 (200 orders) -> 1 shipment, Delivered             -> 200 rows
--   residues 36..55 (200 orders) -> 1 shipment, InTransit             -> 200 rows
--   residues 56..65 (100 orders) -> 1 shipment, OutForDelivery        -> 100 rows
--   residues 66..75 (100 orders) -> 1 shipment, Pending               -> 100 rows
--   residues 76..80 ( 50 orders) -> 1 shipment, PickedUp              ->  50 rows
--   residues 81..85 ( 50 orders) -> 1 shipment, Cancelled             ->  50 rows
--   residues 86..99 (140 orders) -> NO shipment at all (never shipped)
-- Totals: 860/1000 orders (86%) have at least one shipment, 140 (14%) have
-- none — "most, not all" per the task brief. Row count =
-- 320 + 200 + 200 + 100 + 100 + 50 + 50 = 1020 rows (>= 1000 target).
-- Idempotent: every INSERT is guarded by a NOT EXISTS check on the
-- deterministic Id, safe to re-run.
-- ============================================================

USE ShippingDB;
GO

-- ============================================================
-- A few more carriers (001 already seeded Carrier #1 'Standard Shipping'/STD
-- with TenantId = all-zero — left untouched; new rows use the correct system
-- TenantId per SEED_DATA_CONVENTIONS.md).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Carriers WHERE Id = '00000000-0000-0000-0000-000000000002')
INSERT INTO Carriers (Id, TenantId, Name, Code, IsActive, BaseRatePerKg, CreatedAt, InterProvinceSurcharge, OriginProvince)
VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', N'Giao Hang Nhanh', 'GHN', 1, 3.00, GETUTCDATE(), 1.20, N'Ha Noi');
GO

IF NOT EXISTS (SELECT 1 FROM Carriers WHERE Id = '00000000-0000-0000-0000-000000000003')
INSERT INTO Carriers (Id, TenantId, Name, Code, IsActive, BaseRatePerKg, CreatedAt, InterProvinceSurcharge, OriginProvince)
VALUES ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', N'Giao Hang Tiet Kiem', 'GHTK', 1, 2.20, GETUTCDATE(), 0.80, N'Ho Chi Minh');
GO

IF NOT EXISTS (SELECT 1 FROM Carriers WHERE Id = '00000000-0000-0000-0000-000000000004')
INSERT INTO Carriers (Id, TenantId, Name, Code, IsActive, BaseRatePerKg, CreatedAt, InterProvinceSurcharge, OriginProvince)
VALUES ('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', N'Viettel Post', 'VTP', 1, 2.80, GETUTCDATE(), 1.00, N'Da Nang');
GO

IF NOT EXISTS (SELECT 1 FROM Carriers WHERE Id = '00000000-0000-0000-0000-000000000005')
INSERT INTO Carriers (Id, TenantId, Name, Code, IsActive, BaseRatePerKg, CreatedAt, InterProvinceSurcharge, OriginProvince)
VALUES ('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', N'J&T Express', 'JNT', 1, 3.20, GETUTCDATE(), 1.10, N'Ho Chi Minh');
GO

-- ============================================================
-- Shipments
-- ============================================================
;WITH Numbers AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Numbers WHERE N < 1000
),
OrderBuckets AS (
    -- One deterministic "order shipping profile" per SalesOrder N: a base
    -- ship date spread over the last 12 months, a carrier pick, recipient
    -- name/address and parcel weight — all reused by every shipment that
    -- belongs to this order (a re-ship keeps the same carrier/recipient).
    SELECT
        N,
        N % 100 AS Residue,
        DATEADD(MINUTE, -(ABS(CHECKSUM(NEWID())) % 1440),
            DATEADD(DAY, -(ABS(CHECKSUM(NEWID())) % 365), GETUTCDATE())) AS ShipBaseDate,
        1 + (ABS(CHECKSUM(NEWID())) % 5) AS CarrierPick,
        CAST(0.2 + (ABS(CHECKSUM(NEWID())) % 2000) / 100.0 AS DECIMAL(10,3)) AS Weight,
        (ABS(CHECKSUM(NEWID())) % 10) AS LastNameIdx,
        (ABS(CHECKSUM(NEWID())) % 10) AS FirstNameIdx,
        (ABS(CHECKSUM(NEWID())) % 10) AS StreetIdx,
        (ABS(CHECKSUM(NEWID())) % 8)  AS ProvinceIdx,
        (ABS(CHECKSUM(NEWID())) % 12) AS DistrictNo,
        (ABS(CHECKSUM(NEWID())) % 20) AS WardNo,
        (ABS(CHECKSUM(NEWID())) % 900000000 + 100000000) AS PhoneSuffix
    FROM Numbers
),
ShipAttempts AS (
    -- Retry orders: residues 0..15 -> 1st Failed/Returned, 2nd Delivered (re-ship)
    SELECT N AS OrderN, 1 AS AttemptSeq, CASE WHEN N % 2 = 0 THEN 5 ELSE 6 END AS Status, ShipBaseDate
    FROM OrderBuckets WHERE Residue BETWEEN 0 AND 15
    UNION ALL
    SELECT N, 2, 4, ShipBaseDate
    FROM OrderBuckets WHERE Residue BETWEEN 0 AND 15
    UNION ALL
    -- Delivered (single shipment)
    SELECT N, 1, 4, ShipBaseDate
    FROM OrderBuckets WHERE Residue BETWEEN 16 AND 35
    UNION ALL
    -- InTransit
    SELECT N, 1, 2, ShipBaseDate
    FROM OrderBuckets WHERE Residue BETWEEN 36 AND 55
    UNION ALL
    -- OutForDelivery
    SELECT N, 1, 3, ShipBaseDate
    FROM OrderBuckets WHERE Residue BETWEEN 56 AND 65
    UNION ALL
    -- Pending
    SELECT N, 1, 0, ShipBaseDate
    FROM OrderBuckets WHERE Residue BETWEEN 66 AND 75
    UNION ALL
    -- PickedUp
    SELECT N, 1, 1, ShipBaseDate
    FROM OrderBuckets WHERE Residue BETWEEN 76 AND 80
    UNION ALL
    -- Cancelled
    SELECT N, 1, 7, ShipBaseDate
    FROM OrderBuckets WHERE Residue BETWEEN 81 AND 85
    -- residues 86..99 (140 orders) intentionally produce NO rows -> never shipped
),
Base AS (
    SELECT
        ROW_NUMBER() OVER (ORDER BY a.OrderN, a.AttemptSeq) AS ShipN,
        a.OrderN,
        a.AttemptSeq,
        a.Status,
        CASE WHEN a.AttemptSeq = 2 THEN DATEADD(DAY, 5, a.ShipBaseDate) ELSE a.ShipBaseDate END AS CreatedAt,
        ob.CarrierPick,
        ob.Weight,
        ob.LastNameIdx, ob.FirstNameIdx, ob.StreetIdx, ob.ProvinceIdx, ob.DistrictNo, ob.WardNo, ob.PhoneSuffix
    FROM ShipAttempts a
    JOIN OrderBuckets ob ON ob.N = a.OrderN
),
Lookup AS (
    SELECT
        b.*,
        CASE b.CarrierPick
            WHEN 1 THEN CAST('00000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER)
            WHEN 2 THEN CAST('00000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER)
            WHEN 3 THEN CAST('00000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER)
            WHEN 4 THEN CAST('00000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER)
            ELSE CAST('00000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER)
        END AS CarrierId,
        CASE b.CarrierPick
            WHEN 1 THEN N'Standard Shipping' WHEN 2 THEN N'Giao Hang Nhanh' WHEN 3 THEN N'Giao Hang Tiet Kiem'
            WHEN 4 THEN N'Viettel Post' ELSE N'J&T Express' END AS CarrierName,
        CASE b.CarrierPick
            WHEN 1 THEN 'STD' WHEN 2 THEN 'GHN' WHEN 3 THEN 'GHTK' WHEN 4 THEN 'VTP' ELSE 'JNT' END AS CarrierCode,
        CASE b.LastNameIdx
            WHEN 0 THEN N'Nguyen' WHEN 1 THEN N'Tran' WHEN 2 THEN N'Le' WHEN 3 THEN N'Pham' WHEN 4 THEN N'Hoang'
            WHEN 5 THEN N'Vu' WHEN 6 THEN N'Vo' WHEN 7 THEN N'Dang' WHEN 8 THEN N'Bui' ELSE N'Do' END AS LastName,
        CASE b.FirstNameIdx
            WHEN 0 THEN N'Van An' WHEN 1 THEN N'Thi Bao' WHEN 2 THEN N'Minh Chau' WHEN 3 THEN N'Quoc Dat'
            WHEN 4 THEN N'Thi Em' WHEN 5 THEN N'Van Phuc' WHEN 6 THEN N'Thi Giang' WHEN 7 THEN N'Minh Hoang'
            WHEN 8 THEN N'Thi Kim' ELSE N'Van Long' END AS FirstName,
        CASE b.StreetIdx
            WHEN 0 THEN N'12 Le Loi Street' WHEN 1 THEN N'45 Tran Hung Dao Street' WHEN 2 THEN N'78 Nguyen Hue Boulevard'
            WHEN 3 THEN N'23 Hai Ba Trung Street' WHEN 4 THEN N'56 Vo Van Kiet Avenue' WHEN 5 THEN N'89 Pham Ngu Lao Street'
            WHEN 6 THEN N'34 Ly Thuong Kiet Street' WHEN 7 THEN N'67 Dien Bien Phu Street' WHEN 8 THEN N'90 Cach Mang Thang Tam Street'
            ELSE N'15 Nguyen Trai Street' END AS AddressStreet,
        CASE b.ProvinceIdx
            WHEN 0 THEN N'Ha Noi' WHEN 1 THEN N'Ho Chi Minh City' WHEN 2 THEN N'Da Nang' WHEN 3 THEN N'Hai Phong'
            WHEN 4 THEN N'Can Tho' WHEN 5 THEN N'Bien Hoa' WHEN 6 THEN N'Nha Trang' ELSE N'Hue' END AS AddressProvince,
        N'District ' + CONVERT(VARCHAR(4), b.DistrictNo + 1) AS AddressDistrict,
        N'Ward ' + CONVERT(VARCHAR(4), b.WardNo + 1) AS AddressWard
    FROM Base b
)
INSERT INTO Shipments (
    Id, TenantId, OrderId, CarrierId, CarrierName, TrackingNumber, Status,
    RecipientName, RecipientPhone, AddressStreet, AddressDistrict, AddressProvince,
    AddressWard, AddressCountry, TotalWeightKg, EstimatedDelivery, ActualDelivery,
    CancelledAt, CancelReason, DeliveryAttempts, CreatedAt, UpdatedAt
)
SELECT
    idc.NewId,
    '00000000-0000-0000-0000-000000000001',
    oidc.NewOrderId,
    l.CarrierId,
    l.CarrierName,
    l.CarrierCode + RIGHT('000000000' + CONVERT(VARCHAR(10), l.ShipN), 9),
    l.Status,
    l.LastName + N' ' + l.FirstName,
    '09' + RIGHT('00000000' + CONVERT(VARCHAR(10), l.PhoneSuffix), 8),
    l.AddressStreet,
    l.AddressDistrict,
    l.AddressProvince,
    l.AddressWard,
    N'Vietnam',
    l.Weight,
    CAST(DATEADD(DAY, 4, l.CreatedAt) AS DATE),
    CASE WHEN l.Status = 4 THEN DATEADD(DAY, 1 + (l.ShipN % 6), l.CreatedAt) ELSE NULL END,
    CASE WHEN l.Status = 7 THEN DATEADD(HOUR, 4, l.CreatedAt) ELSE NULL END,
    CASE
        WHEN l.Status = 5 THEN N'Delivery attempt failed — recipient unreachable'
        WHEN l.Status = 6 THEN N'Returned to sender — recipient refused parcel'
        WHEN l.Status = 7 THEN N'Cancelled before dispatch'
        ELSE NULL
    END,
    CASE WHEN l.Status IN (5,6) THEN 1 WHEN l.AttemptSeq = 2 THEN 1 ELSE 0 END,
    l.CreatedAt,
    CASE WHEN l.Status IN (4,5,6,7) THEN DATEADD(DAY, 1, l.CreatedAt) ELSE l.CreatedAt END
FROM Lookup l
CROSS APPLY (SELECT CAST('B0000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), l.ShipN), 12) AS UNIQUEIDENTIFIER) AS NewId) idc
CROSS APPLY (SELECT CAST('70000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), l.OrderN), 12) AS UNIQUEIDENTIFIER) AS NewOrderId) oidc
WHERE NOT EXISTS (SELECT 1 FROM Shipments s WHERE s.Id = idc.NewId)
OPTION (MAXRECURSION 1000);
GO
