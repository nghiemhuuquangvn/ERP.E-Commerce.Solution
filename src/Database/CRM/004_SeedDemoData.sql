-- ============================================================
-- CrmDB Demo Data Seed — CRMService (Port 5006, Code: CRM)
-- Migration 004: Seeds Contacts (1000+), Leads (~300),
-- Opportunities (~200), Cases (~150) for demo/UI purposes.
--
-- Follows src/Database/SEED_DATA_CONVENTIONS.md:
--   - Contact.Id      = 60000000-0001-0000-0000-{N}   (N = 1..1000)
--   - Contact.UserId  = 20000000-0002-0000-0000-{N}   (same N as above —
--     ties a CRM Contact to the matching Identity Customer User)
--   - Lead.Id         = 60000000-0002-0000-0000-{N}   (N = 1..300)
--   - Opportunity.Id  = 60000000-0003-0000-0000-{N}   (N = 1..200)
--   - Case.Id         = 60000000-0004-0000-0000-{N}   (N = 1..150)
--   - Case.OrderId    = 70000000-0001-0000-0000-{N}   (Sales SalesOrder,
--     N pseudo-random in 1..1000, ~30% of Cases)
--   - TenantId        = 00000000-0000-0000-0000-000000000001 everywhere
--
-- Rows are generated set-based (recursive-CTE numbers table cross joined
-- with small name/word arrays) — no hand-written literal row-per-INSERT.
-- Idempotent: guarded by row-count fast path + per-row NOT EXISTS, safe to
-- re-run (including after a partial failure mid-batch).
-- ============================================================

USE CrmDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================
-- Shared helper data: a 1..1000 numbers table + small realistic
-- name/company/word dictionaries, built once and reused by every
-- INSERT below (temp tables persist for the life of this script's
-- single sqlcmd connection).
-- ============================================================

IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
CREATE TABLE #Numbers (N INT NOT NULL PRIMARY KEY);
GO

;WITH NumbersCte AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM NumbersCte WHERE N < 1000
)
INSERT INTO #Numbers (N)
SELECT N FROM NumbersCte
OPTION (MAXRECURSION 0);
GO

CREATE TABLE #FirstVN (Ord INT NOT NULL PRIMARY KEY, DisplayName NVARCHAR(50), AsciiName NVARCHAR(50));
INSERT INTO #FirstVN VALUES
(1,N'Minh','minh'),(2,N'Anh','anh'),(3,N'Hùng','hung'),(4,N'Hoa','hoa'),(5,N'Lan','lan'),
(6,N'Hương','huong'),(7,N'Mai','mai'),(8,N'Linh','linh'),(9,N'Trang','trang'),(10,N'Ngọc','ngoc'),
(11,N'Phương','phuong'),(12,N'Thu','thu'),(13,N'Hạnh','hanh'),(14,N'Nam','nam'),(15,N'Đức','duc'),
(16,N'Thành','thanh'),(17,N'Quang','quang'),(18,N'Tuấn','tuan'),(19,N'Long','long'),(20,N'Khánh','khanh');
GO

CREATE TABLE #LastVN (Ord INT NOT NULL PRIMARY KEY, DisplayName NVARCHAR(50), AsciiName NVARCHAR(50));
INSERT INTO #LastVN VALUES
(1,N'Nguyễn','nguyen'),(2,N'Trần','tran'),(3,N'Lê','le'),(4,N'Phạm','pham'),(5,N'Hoàng','hoang'),
(6,N'Huỳnh','huynh'),(7,N'Phan','phan'),(8,N'Vũ','vu'),(9,N'Võ','vo'),(10,N'Đặng','dang'),
(11,N'Bùi','bui'),(12,N'Đỗ','do'),(13,N'Hồ','ho'),(14,N'Ngô','ngo'),(15,N'Dương','duong');
GO

CREATE TABLE #FirstIntl (Ord INT NOT NULL PRIMARY KEY, FirstName NVARCHAR(50));
INSERT INTO #FirstIntl VALUES
(1,'John'),(2,'Emma'),(3,'Michael'),(4,'Sarah'),(5,'David'),(6,'Jennifer'),(7,'Robert'),(8,'Lisa'),
(9,'James'),(10,'Maria'),(11,'Daniel'),(12,'Anna'),(13,'Wei'),(14,'Yuki'),(15,'Somchai');
GO

CREATE TABLE #LastIntl (Ord INT NOT NULL PRIMARY KEY, LastName NVARCHAR(50));
INSERT INTO #LastIntl VALUES
(1,'Smith'),(2,'Johnson'),(3,'Brown'),(4,'Wilson'),(5,'Lee'),(6,'Chen'),(7,'Kim'),(8,'Wang'),
(9,'Taylor'),(10,'Garcia'),(11,'Nguyen'),(12,'Kowalski'),(13,'Zhang'),(14,'Tanaka'),(15,'Srisawat');
GO

CREATE TABLE #CompanyWordsVN (Ord INT NOT NULL PRIMARY KEY, Word NVARCHAR(50));
INSERT INTO #CompanyWordsVN VALUES
(1,N'Việt'),(2,N'Thành Công'),(3,N'Phát Đạt'),(4,N'Hoàng Gia'),(5,N'Đại Dương'),
(6,N'Sài Gòn'),(7,N'Thăng Long'),(8,N'Hồng Phát'),(9,N'Minh Khang'),(10,N'Tân Phát'),
(11,N'An Phú'),(12,N'Đông Á'),(13,N'Việt Tiến'),(14,N'Hòa Bình'),(15,N'Kim Cương'),
(16,N'Sao Việt'),(17,N'Miền Nam'),(18,N'Bình Minh'),(19,N'Ánh Dương'),(20,N'Phú Quý');
GO

CREATE TABLE #CompanyTypesVN (Ord INT NOT NULL PRIMARY KEY, TypeName NVARCHAR(50));
INSERT INTO #CompanyTypesVN VALUES
(1,N'Công ty TNHH'),(2,N'Công ty Cổ phần'),(3,N'Công ty TNHH MTV'),(4,N'Tập đoàn'),(5,N'Doanh nghiệp tư nhân');
GO

CREATE TABLE #IndustryWordsVN (Ord INT NOT NULL PRIMARY KEY, Word NVARCHAR(50));
INSERT INTO #IndustryWordsVN VALUES
(1,N'Thương Mại'),(2,N'Xây Dựng'),(3,N'Công Nghệ'),(4,N'Dịch Vụ'),(5,N'Xuất Nhập Khẩu'),
(6,N'Vận Tải'),(7,N'Đầu Tư'),(8,N'Sản Xuất'),(9,N'Truyền Thông'),(10,N'Thực Phẩm');
GO

CREATE TABLE #CompanyIntl (Ord INT NOT NULL PRIMARY KEY, Name NVARCHAR(100));
INSERT INTO #CompanyIntl VALUES
(1,'Global Trading Co.'),(2,'Pacific Import Export'),(3,'Sunrise Logistics Ltd'),(4,'Blue Ocean Enterprises'),
(5,'Silver Star Holdings'),(6,'Nova Tech Solutions'),(7,'Golden Gate Trading'),(8,'Orient Express Corp'),
(9,'Evergreen Industries'),(10,'Falcon International');
GO

CREATE TABLE #CitiesVN (Ord INT NOT NULL PRIMARY KEY, City NVARCHAR(100));
INSERT INTO #CitiesVN VALUES
(1,N'Hà Nội'),(2,N'Hồ Chí Minh'),(3,N'Đà Nẵng'),(4,N'Hải Phòng'),(5,N'Cần Thơ'),
(6,N'Nha Trang'),(7,N'Huế'),(8,N'Vũng Tàu'),(9,N'Biên Hòa'),(10,N'Quy Nhơn');
GO

CREATE TABLE #JobTitlesVN (Ord INT NOT NULL PRIMARY KEY, Title NVARCHAR(100));
INSERT INTO #JobTitlesVN VALUES
(1,N'Giám đốc'),(2,N'Trưởng phòng'),(3,N'Nhân viên kinh doanh'),(4,N'Kế toán trưởng'),(5,N'Quản lý mua hàng'),
(6,N'Giám đốc điều hành'),(7,N'Trưởng bộ phận CNTT'),(8,N'Chuyên viên Marketing'),(9,N'Trợ lý giám đốc'),(10,N'Chủ cửa hàng');
GO

CREATE TABLE #CountriesIntl (Ord INT NOT NULL PRIMARY KEY, Name NVARCHAR(100));
INSERT INTO #CountriesIntl VALUES
(1,'United States'),(2,'Japan'),(3,'South Korea'),(4,'China'),(5,'Singapore'),
(6,'Australia'),(7,'France'),(8,'Germany'),(9,'Thailand'),(10,'Malaysia');
GO

CREATE TABLE #DealWords (Ord INT NOT NULL PRIMARY KEY, Txt NVARCHAR(100));
INSERT INTO #DealWords VALUES
(1,N'Hợp đồng cung cấp'),(2,N'Dự án triển khai'),(3,N'Đơn hàng sỉ'),(4,N'Gói dịch vụ'),(5,N'Hợp tác phân phối'),
(6,'Enterprise License Deal'),(7,'Bulk Order Contract'),(8,'Service Package'),(9,'Distribution Partnership'),(10,'Annual Supply Contract');
GO

CREATE TABLE #SupportTopics (Ord INT NOT NULL PRIMARY KEY, Topic NVARCHAR(200));
INSERT INTO #SupportTopics VALUES
(1,N'Sản phẩm bị lỗi'),(2,N'Yêu cầu đổi trả'),(3,N'Chậm giao hàng'),(4,N'Hỏi về bảo hành'),(5,N'Khiếu nại thanh toán'),
(6,'Product Defect Report'),(7,'Return Request'),(8,'Delayed Shipment Inquiry'),(9,'Warranty Question'),(10,'Billing Complaint');
GO

CREATE TABLE #Categories (Ord INT NOT NULL PRIMARY KEY, Name NVARCHAR(100));
INSERT INTO #Categories VALUES
(1,N'Sản phẩm'),(2,N'Giao hàng'),(3,N'Thanh toán'),(4,N'Bảo hành'),(5,N'Khác');
GO

-- ============================================================
-- Contacts (1000 rows)
-- ============================================================
IF (SELECT COUNT(*) FROM Contacts WHERE TenantId = '00000000-0000-0000-0000-000000000001') < 1000
BEGIN
    INSERT INTO Contacts
        (Id, TenantId, FirstName, LastName, Email, Phone, Mobile, Company, JobTitle, Type, Source,
         OwnedByUserId, Street, City, State, PostalCode, Country, IsActive, Tags, Notes, UserId,
         CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted, Name, ContactSource, Description)
    SELECT
        x.NewId, '00000000-0000-0000-0000-000000000001',
        CASE WHEN x.IsIntl = 1 THEN fi.FirstName ELSE fv.DisplayName END,
        CASE WHEN x.IsIntl = 1 THEN li.LastName ELSE lv.DisplayName END,
        LOWER(CASE WHEN x.IsIntl = 1 THEN fi.FirstName ELSE fv.AsciiName END) + '.' +
            LOWER(CASE WHEN x.IsIntl = 1 THEN li.LastName ELSE lv.AsciiName END) + CONVERT(VARCHAR(12), n.N) +
            CASE WHEN n.N % 4 = 0 THEN '@yahoo.com' WHEN n.N % 4 = 1 THEN '@outlook.com'
                 WHEN n.N % 4 = 2 THEN '@company-mail.com' ELSE '@gmail.com' END,
        '028' + RIGHT('0000000' + CONVERT(VARCHAR(12), 1000000 + n.N), 7),
        '09' + RIGHT('00000000' + CONVERT(VARCHAR(12), 10000000 + n.N), 8),
        CASE WHEN x.TypeCode = 0 AND n.N % 3 = 0 THEN NULL
             WHEN x.IsIntl = 1 THEN ci.Name
             ELSE ct.TypeName + N' ' + iw.Word + N' ' + cw.Word END,
        CASE WHEN x.TypeCode = 0 THEN jt.Title ELSE NULL END,
        x.TypeCode, x.SourceCode,
        '20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), x.OwnerN), 12),
        CONVERT(VARCHAR(6), 10 + (n.N % 300)) + N' Đường ' + cw.Word,
        CASE WHEN x.IsIntl = 1 THEN coi.Name ELSE cv.City END,
        NULL,
        RIGHT('000000' + CONVERT(VARCHAR(12), n.N), 6),
        CASE WHEN x.IsIntl = 1 THEN coi.Name ELSE N'Việt Nam' END,
        CASE WHEN n.N % 23 = 0 THEN 0 ELSE 1 END,
        CASE WHEN n.N % 5 = 0 THEN N'VIP' WHEN n.N % 7 = 0 THEN N'Wholesale' ELSE NULL END,
        N'Khách hàng ' + CASE x.TypeCode WHEN 1 THEN N'doanh nghiệp' WHEN 2 THEN N'đối tác' ELSE N'cá nhân' END + N' - dữ liệu demo.',
        x.UserGuid,
        N'system-seed', x.CreatedAtV, N'system-seed', DATEADD(DAY, n.N % 5, x.CreatedAtV), 0,
        CASE WHEN x.IsIntl = 1 THEN fi.FirstName + N' ' + li.LastName ELSE lv.DisplayName + N' ' + fv.DisplayName END,
        x.SourceCode, NULL
    FROM #Numbers n
    CROSS APPLY (SELECT
        CAST(CONCAT('60000000-0001-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12)) AS UNIQUEIDENTIFIER) AS NewId,
        CAST(CONCAT('20000000-0002-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12)) AS UNIQUEIDENTIFIER) AS UserGuid,
        CASE WHEN n.N % 11 = 0 THEN 1 ELSE 0 END AS IsIntl,
        CASE WHEN n.N % 10 IN (7, 8) THEN 1 WHEN n.N % 10 = 9 THEN 2 ELSE 0 END AS TypeCode,
        n.N % 5 AS SourceCode,
        ((n.N * 13 - 1) % 50) + 1 AS OwnerN,
        DATEADD(MINUTE, -((n.N * 613) % 525600), SYSUTCDATETIME()) AS CreatedAtV
    ) x
    JOIN #FirstVN fv ON fv.Ord = ((n.N - 1) % 20) + 1
    JOIN #LastVN lv ON lv.Ord = ((n.N * 3 - 1) % 15) + 1
    JOIN #FirstIntl fi ON fi.Ord = ((n.N - 1) % 15) + 1
    JOIN #LastIntl li ON li.Ord = ((n.N * 3 - 1) % 15) + 1
    JOIN #CompanyWordsVN cw ON cw.Ord = ((n.N * 5 - 1) % 20) + 1
    JOIN #CompanyTypesVN ct ON ct.Ord = ((n.N - 1) % 5) + 1
    JOIN #IndustryWordsVN iw ON iw.Ord = ((n.N * 7 - 1) % 10) + 1
    JOIN #CompanyIntl ci ON ci.Ord = ((n.N - 1) % 10) + 1
    JOIN #CitiesVN cv ON cv.Ord = ((n.N - 1) % 10) + 1
    JOIN #JobTitlesVN jt ON jt.Ord = ((n.N * 11 - 1) % 10) + 1
    JOIN #CountriesIntl coi ON coi.Ord = ((n.N - 1) % 10) + 1
    WHERE n.N <= 1000
      AND NOT EXISTS (SELECT 1 FROM Contacts existing WHERE existing.Id = x.NewId);
END
GO

-- ============================================================
-- Leads (300 rows) — ~67% linked to an existing Contact
-- ============================================================
IF (SELECT COUNT(*) FROM Leads WHERE TenantId = '00000000-0000-0000-0000-000000000001') < 300
BEGIN
    INSERT INTO Leads
        (Id, TenantId, LeadName, ContactId, Email, Phone, Company, LeadStatus, LeadSource,
         EstimatedValue, AssignedToUserId, QualifiedAt, ConvertedAt, ConvertedToId, LostAt, LostReason,
         Notes, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted, Description)
    SELECT
        x.NewId, '00000000-0000-0000-0000-000000000001',
        COALESCE(c.Name, lv.DisplayName + N' ' + fv.DisplayName),
        c.Id,
        COALESCE(c.Email, LOWER(fv.AsciiName) + '.' + LOWER(lv.AsciiName) + CONVERT(VARCHAR(12), n.N) + '+lead@gmail.com'),
        COALESCE(c.Mobile, '09' + RIGHT('00000000' + CONVERT(VARCHAR(12), 20000000 + n.N), 8)),
        COALESCE(c.Company, ct.TypeName + N' ' + iw.Word + N' ' + cw.Word),
        x.StatusCode, x.SourceCode,
        CAST(5000000 + ((n.N * 977) % 495000000) AS DECIMAL(18, 2)),
        '20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), x.OwnerN), 12),
        CASE WHEN x.StatusCode >= 2 THEN DATEADD(DAY, 3, x.CreatedAtV) ELSE NULL END,
        CASE WHEN x.StatusCode = 4 THEN DATEADD(DAY, 10, x.CreatedAtV) ELSE NULL END,
        NULL,
        CASE WHEN x.StatusCode = 3 THEN DATEADD(DAY, 7, x.CreatedAtV) ELSE NULL END,
        CASE WHEN x.StatusCode = 3 THEN N'Không phù hợp ngân sách' ELSE NULL END,
        N'Lead được tạo tự động cho mục đích demo.',
        N'system-seed', x.CreatedAtV, N'system-seed', DATEADD(DAY, n.N % 5, x.CreatedAtV), 0, NULL
    FROM #Numbers n
    CROSS APPLY (SELECT
        CAST(CONCAT('60000000-0002-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12)) AS UNIQUEIDENTIFIER) AS NewId,
        CASE WHEN n.N % 3 = 0 THEN 0 ELSE 1 END AS HasContact,
        CAST(CONCAT('60000000-0001-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 17 - 1) % 1000) + 1), 12)) AS UNIQUEIDENTIFIER) AS ContactGuid,
        n.N % 5 AS StatusCode,
        n.N % 5 AS SourceCode,
        ((n.N * 13 - 1) % 50) + 1 AS OwnerN,
        DATEADD(MINUTE, -((n.N * 727) % 525600), SYSUTCDATETIME()) AS CreatedAtV
    ) x
    JOIN #FirstVN fv ON fv.Ord = ((n.N - 1) % 20) + 1
    JOIN #LastVN lv ON lv.Ord = ((n.N * 3 - 1) % 15) + 1
    JOIN #CompanyWordsVN cw ON cw.Ord = ((n.N * 5 - 1) % 20) + 1
    JOIN #CompanyTypesVN ct ON ct.Ord = ((n.N - 1) % 5) + 1
    JOIN #IndustryWordsVN iw ON iw.Ord = ((n.N * 7 - 1) % 10) + 1
    LEFT JOIN Contacts c ON x.HasContact = 1 AND c.Id = x.ContactGuid
    WHERE n.N <= 300
      AND NOT EXISTS (SELECT 1 FROM Leads existing WHERE existing.Id = x.NewId);
END
GO

-- ============================================================
-- Opportunities (200 rows) — ~70% linked to a Contact, ~40% linked to a Lead
-- ============================================================
IF (SELECT COUNT(*) FROM Opportunities WHERE TenantId = '00000000-0000-0000-0000-000000000001') < 200
BEGIN
    INSERT INTO Opportunities
        (Id, TenantId, Name, ContactId, LeadId, Stage, Amount, Probability, ExpectedCloseDate,
         AssignedToUserId, WonAt, LostAt, LostReason, Notes, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt,
         IsDeleted, OpportunityName, OpportunityStage, ProbabilityPercent, EstimatedValue, Description)
    SELECT
        x.NewId, '00000000-0000-0000-0000-000000000001',
        CASE WHEN c.Id IS NOT NULL THEN dw.Txt + N' - ' + COALESCE(c.Company, c.Name)
             WHEN l.Id IS NOT NULL THEN dw.Txt + N' - ' + l.LeadName
             ELSE dw.Txt + N' - ' + cw.Word END,
        c.Id, l.Id,
        x.StageCode, x.AmountV,
        CASE x.StageCode WHEN 4 THEN 100 WHEN 5 THEN 0 WHEN 3 THEN 75 WHEN 2 THEN 50 WHEN 1 THEN 25 ELSE 10 END,
        CAST(DATEADD(DAY, 30 + (n.N % 60), x.CreatedAtV) AS DATE),
        '20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), x.OwnerN), 12),
        CASE WHEN x.StageCode = 4 THEN DATEADD(DAY, 45, x.CreatedAtV) ELSE NULL END,
        CASE WHEN x.StageCode = 5 THEN DATEADD(DAY, 30, x.CreatedAtV) ELSE NULL END,
        CASE WHEN x.StageCode = 5 THEN N'Khách hàng chọn đối thủ cạnh tranh' ELSE NULL END,
        N'Cơ hội bán hàng được tạo tự động cho mục đích demo.',
        N'system-seed', x.CreatedAtV, N'system-seed', DATEADD(DAY, n.N % 5, x.CreatedAtV), 0,
        CASE WHEN c.Id IS NOT NULL THEN dw.Txt + N' - ' + COALESCE(c.Company, c.Name)
             WHEN l.Id IS NOT NULL THEN dw.Txt + N' - ' + l.LeadName
             ELSE dw.Txt + N' - ' + cw.Word END,
        x.StageCode,
        CASE x.StageCode WHEN 4 THEN 100 WHEN 5 THEN 0 WHEN 3 THEN 75 WHEN 2 THEN 50 WHEN 1 THEN 25 ELSE 10 END,
        x.AmountV, NULL
    FROM #Numbers n
    CROSS APPLY (SELECT
        CAST(CONCAT('60000000-0003-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12)) AS UNIQUEIDENTIFIER) AS NewId,
        CASE WHEN n.N % 10 < 7 THEN 1 ELSE 0 END AS HasContact,
        CAST(CONCAT('60000000-0001-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 19 - 1) % 1000) + 1), 12)) AS UNIQUEIDENTIFIER) AS ContactGuid,
        CASE WHEN n.N % 5 < 2 THEN 1 ELSE 0 END AS HasLead,
        CAST(CONCAT('60000000-0002-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 23 - 1) % 300) + 1), 12)) AS UNIQUEIDENTIFIER) AS LeadGuid,
        n.N % 6 AS StageCode,
        CAST(2000000 + ((n.N * 1237) % 998000000) AS DECIMAL(18, 2)) AS AmountV,
        ((n.N * 13 - 1) % 50) + 1 AS OwnerN,
        DATEADD(MINUTE, -((n.N * 839) % 525600), SYSUTCDATETIME()) AS CreatedAtV
    ) x
    JOIN #DealWords dw ON dw.Ord = ((n.N * 9 - 1) % 10) + 1
    JOIN #CompanyWordsVN cw ON cw.Ord = ((n.N * 5 - 1) % 20) + 1
    LEFT JOIN Contacts c ON x.HasContact = 1 AND c.Id = x.ContactGuid
    LEFT JOIN Leads l ON x.HasLead = 1 AND l.Id = x.LeadGuid
    WHERE n.N <= 200
      AND NOT EXISTS (SELECT 1 FROM Opportunities existing WHERE existing.Id = x.NewId);
END
GO

-- ============================================================
-- Cases (150 rows) — ~90% linked to a Contact, ~30% reference a Sales OrderId
-- ============================================================
IF (SELECT COUNT(*) FROM Cases WHERE TenantId = '00000000-0000-0000-0000-000000000001') < 150
BEGIN
    INSERT INTO Cases
        (Id, TenantId, CaseNumber, ContactId, OrderId, Subject, Description, Status, Priority, Category,
         AssignedToUserId, Resolution, ResolvedAt, ClosedAt, SlaDeadline, CreatedBy, CreatedAt, UpdatedBy,
         UpdatedAt, IsDeleted, CustomerId, CaseSubject, CaseDescription, CasePriority, CaseStatus, ResolutionNote)
    SELECT
        x.NewId, '00000000-0000-0000-0000-000000000001',
        'CASE-' + RIGHT('000000' + CONVERT(VARCHAR(12), n.N), 6),
        c.Id,
        CASE WHEN x.HasOrder = 1 THEN x.OrderGuid ELSE NULL END,
        st.Topic,
        st.Topic + N' - Khách hàng phản ánh về vấn đề trên, cần được xử lý sớm.',
        x.StatusCode, x.PriorityCode, cat.Name,
        '20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), x.OwnerN), 12),
        CASE WHEN x.StatusCode IN (3, 4) THEN N'Đã xử lý và phản hồi khách hàng.' ELSE NULL END,
        CASE WHEN x.StatusCode IN (3, 4) THEN DATEADD(DAY, 5, x.CreatedAtV) ELSE NULL END,
        CASE WHEN x.StatusCode = 4 THEN DATEADD(DAY, 7, x.CreatedAtV) ELSE NULL END,
        DATEADD(DAY, 3, x.CreatedAtV),
        N'system-seed', x.CreatedAtV, N'system-seed', DATEADD(DAY, n.N % 5, x.CreatedAtV), 0,
        g.CustomerGuid,
        st.Topic,
        st.Topic + N' - Khách hàng phản ánh về vấn đề trên, cần được xử lý sớm.',
        x.PriorityCode, x.StatusCode,
        CASE WHEN x.StatusCode IN (3, 4) THEN N'Đã xử lý và phản hồi khách hàng.' ELSE NULL END
    FROM #Numbers n
    CROSS APPLY (SELECT
        CAST(CONCAT('60000000-0004-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12)) AS UNIQUEIDENTIFIER) AS NewId,
        CASE WHEN n.N % 10 = 0 THEN 0 ELSE 1 END AS HasContact,
        ((n.N * 29 - 1) % 1000) + 1 AS ContactN,
        CASE WHEN n.N % 10 < 3 THEN 1 ELSE 0 END AS HasOrder,
        CAST(CONCAT('70000000-0001-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), ((n.N * 31 - 1) % 1000) + 1), 12)) AS UNIQUEIDENTIFIER) AS OrderGuid,
        n.N % 5 AS StatusCode,
        n.N % 4 AS PriorityCode,
        ((n.N * 13 - 1) % 50) + 1 AS OwnerN,
        DATEADD(MINUTE, -((n.N * 951) % 525600), SYSUTCDATETIME()) AS CreatedAtV
    ) x
    CROSS APPLY (SELECT
        CAST(CONCAT('60000000-0001-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), x.ContactN), 12)) AS UNIQUEIDENTIFIER) AS ContactGuid,
        CAST(CONCAT('20000000-0002-0000-0000-', RIGHT('000000000000' + CONVERT(VARCHAR(12), x.ContactN), 12)) AS UNIQUEIDENTIFIER) AS CustomerGuid
    ) g
    JOIN #SupportTopics st ON st.Ord = ((n.N * 7 - 1) % 10) + 1
    JOIN #Categories cat ON cat.Ord = ((n.N * 3 - 1) % 5) + 1
    LEFT JOIN Contacts c ON x.HasContact = 1 AND c.Id = g.ContactGuid
    WHERE n.N <= 150
      AND NOT EXISTS (SELECT 1 FROM Cases existing WHERE existing.Id = x.NewId);
END
GO

-- ============================================================
-- Back-link converted Leads to the Opportunity they produced
-- (Opportunities.LeadId is populated above for a subset; this
-- closes the loop on Leads.ConvertedToId). Safe to re-run.
-- ============================================================
UPDATE l
SET l.ConvertedToId = o.Id,
    l.UpdatedAt = SYSUTCDATETIME(),
    l.UpdatedBy = N'system-seed'
FROM Leads l
JOIN Opportunities o ON o.LeadId = l.Id
WHERE l.LeadStatus = 4 AND l.ConvertedToId IS NULL;
GO
