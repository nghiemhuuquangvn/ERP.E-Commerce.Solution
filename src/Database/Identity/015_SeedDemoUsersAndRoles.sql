-- ============================================================
-- AuthDB Seed Data — IdentityService (Port 5001)
-- Migration 015: Demo data seed — Staff Users & Customer Users
--
-- Implements the Identity rows required by
-- src/Database/SEED_DATA_CONVENTIONS.md's cross-service deterministic-GUID
-- registry:
--   Staff User    -> 20000000-0001-0000-0000-{N}   (~50 rows)
--   Customer User -> 20000000-0002-0000-0000-{N}   (1000+ rows, N also
--                    reused by CRM's Contact.UserId for the same person)
--
-- Does NOT touch the System Tenant (00000000-0000-0000-0000-000000000001)
-- or the SuperAdmin user (20000000-0000-0000-0000-000000000001) — both
-- already seeded by 001_CreateAuthDB.sql.
--
-- Password: every seeded demo user reuses the SAME bcrypt hash as
-- SuperAdmin ('$2a$10$8qVkWDVVWWgf0N.jPcUVfOm/c4VKXmk0q9FKp8KiGZPj.DLq3P0Ly',
-- i.e. plaintext 'SuperAdmin@123') for simplicity — this is dev/demo data
-- only, never do this in a real environment.
--
-- Role wiring notes:
--   - 006_AddCustomerAndStaffRoles.sql created the CUSTOMER/STAFF/USER roles
--     with NEWID() (no deterministic GUID was reserved for them), so this
--     script resolves their Ids by RoleCode at runtime instead of a literal.
--   - The generic STAFF role carries ZERO RolePermissions grants anywhere in
--     the codebase (only SuperAdmin/Admin/Manager/Purchaser/Viewer get
--     grants in 013_SeedRoleDefinitionsIntoRolePermissions.sql — Manager and
--     Purchaser are themselves never actually inserted as Roles rows, so
--     those grants are latent/no-ops too). A Staff user holding only STAFF
--     could not exercise any Admin-panel feature. So every seeded staff user
--     gets BOTH the generic STAFF role (matches the UserTypeCode='Staff' ->
--     IUserRoleResolver convention used by real registration) AND one
--     functional role (Admin/Operator/Viewer, the roles that actually carry
--     permission grants), distributed ~10% Admin / 30% Operator / 60%
--     Viewer.
--   - UserTenants rows are seeded for BOTH staff and customers (not just
--     customers) — the SuperAdmin seed in 001 does the same, and login/JWT
--     issuance reads UserTenants for tenant context, so every login-capable
--     demo user needs one regardless of which bullet point in the original
--     ask mentioned it.
-- ============================================================

USE AuthDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

DECLARE @SystemTenantId   UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminRoleId      UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
DECLARE @OperatorRoleId   UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000003';
DECLARE @ViewerRoleId     UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000004';
DECLARE @StaffRoleId      UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Roles WHERE RoleCode = 'STAFF'    AND TenantId = @SystemTenantId);
DECLARE @CustomerRoleId   UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Roles WHERE RoleCode = 'CUSTOMER' AND TenantId = @SystemTenantId);
DECLARE @DemoPasswordHash VARCHAR(500)     = '$2a$10$8qVkWDVVWWgf0N.jPcUVfOm/c4VKXmk0q9FKp8KiGZPj.DLq3P0Ly'; -- bcrypt('SuperAdmin@123')

DECLARE @StaffCount    INT = 50;
DECLARE @CustomerCount INT = 1050;

IF @StaffRoleId IS NULL
    PRINT 'WARNING: STAFF role not found (expected from 006_AddCustomerAndStaffRoles.sql) — staff users will only get their functional role.';
IF @CustomerRoleId IS NULL
    PRINT 'WARNING: CUSTOMER role not found (expected from 006_AddCustomerAndStaffRoles.sql) — customer users will get no UserRoles row.';

-- ============================================================
-- Staff Users: 20000000-0001-0000-0000-{N}, N = 1..@StaffCount
-- Name pool: 15 Vietnamese family names x 20 given names = 300 unique
-- combinations, more than enough for 50 distinct staff emails.
-- ============================================================
WITH Numbers AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Numbers WHERE N < @StaffCount
)
INSERT INTO Users (
    Id, TenantId, Email, UserName, FullName, PasswordHash,
    IsPhoneConfirmed, PhoneNumber, IsEmailConfirmed, IsActive, IsLocked,
    UserTypeCode, CreatedAt, UpdatedAt
)
SELECT
    CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
    @SystemTenantId,
    LOWER(gv.Name) + N'.' + LOWER(fam.Name) + N'@erp-demo.local',
    LOWER(gv.Name) + N'.' + LOWER(fam.Name) + CONVERT(NVARCHAR(10), n.N),
    fam.Name + N' ' + gv.Name,
    @DemoPasswordHash,
    1,
    N'09' + RIGHT(N'00000000' + CONVERT(NVARCHAR(8), (n.N * 91) % 100000000), 8),
    1,
    1,
    0,
    'Staff',
    DATEADD(DAY, -1 * ((n.N * 37) % 365), GETUTCDATE()),
    DATEADD(DAY, -1 * ((n.N * 37) % 365), GETUTCDATE())
FROM Numbers n
CROSS APPLY (
    SELECT Name FROM (
        SELECT Name, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS Idx
        FROM (VALUES
            (N'Nguyen'), (N'Tran'), (N'Le'), (N'Pham'), (N'Hoang'),
            (N'Huynh'), (N'Phan'), (N'Vu'), (N'Vo'), (N'Dang'),
            (N'Bui'), (N'Do'), (N'Ho'), (N'Ngo'), (N'Duong')
        ) AS t(Name)
    ) x WHERE x.Idx = (n.N - 1) % 15
) AS fam(Name)
CROSS APPLY (
    SELECT Name FROM (
        SELECT Name, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS Idx
        FROM (VALUES
            (N'An'), (N'Binh'), (N'Chi'), (N'Dung'), (N'Giang'),
            (N'Hoa'), (N'Khanh'), (N'Lan'), (N'Minh'), (N'Nga'),
            (N'Oanh'), (N'Phuc'), (N'Quyen'), (N'Son'), (N'Thao'),
            (N'Uyen'), (N'Viet'), (N'Xuan'), (N'Yen'), (N'Tam')
        ) AS t(Name)
    ) x WHERE x.Idx = ((n.N - 1) / 15) % 20
) AS gv(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM Users
    WHERE Id = CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
)
OPTION (MAXRECURSION 0);

-- Functional role per staff user (Admin/Operator/Viewer — the roles that
-- actually carry RolePermissions grants): ~10% Admin, 30% Operator, 60% Viewer.
WITH Numbers AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Numbers WHERE N < @StaffCount
)
INSERT INTO UserRoles (UserId, RoleId, TenantId)
SELECT
    CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
    CASE WHEN n.N <= 5 THEN @AdminRoleId WHEN n.N <= 20 THEN @OperatorRoleId ELSE @ViewerRoleId END,
    @SystemTenantId
FROM Numbers n
WHERE NOT EXISTS (
    SELECT 1 FROM UserRoles ur
    WHERE ur.UserId = CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
      AND ur.RoleId = CASE WHEN n.N <= 5 THEN @AdminRoleId WHEN n.N <= 20 THEN @OperatorRoleId ELSE @ViewerRoleId END
)
OPTION (MAXRECURSION 0);

-- Generic STAFF role alongside the functional one (matches UserTypeCode='Staff').
IF @StaffRoleId IS NOT NULL
BEGIN
    WITH Numbers AS (
        SELECT 1 AS N
        UNION ALL
        SELECT N + 1 FROM Numbers WHERE N < @StaffCount
    )
    INSERT INTO UserRoles (UserId, RoleId, TenantId)
    SELECT
        CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
        @StaffRoleId,
        @SystemTenantId
    FROM Numbers n
    WHERE NOT EXISTS (
        SELECT 1 FROM UserRoles ur
        WHERE ur.UserId = CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
          AND ur.RoleId = @StaffRoleId
    )
    OPTION (MAXRECURSION 0);
END

-- Staff UserTenants (default membership in the system tenant).
;WITH Numbers AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Numbers WHERE N < @StaffCount
)
INSERT INTO UserTenants (UserId, TenantId, IsDefault)
SELECT
    CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
    @SystemTenantId,
    1
FROM Numbers n
WHERE NOT EXISTS (
    SELECT 1 FROM UserTenants ut
    WHERE ut.UserId = CAST('20000000-0001-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
      AND ut.TenantId = @SystemTenantId
)
OPTION (MAXRECURSION 0);

-- ============================================================
-- Customer Users: 20000000-0002-0000-0000-{N}, N = 1..@CustomerCount
-- Emails use the customerNNNN@example.com convention (guaranteed unique at
-- this scale); FullName cycles through the same 15x20 Vietnamese name pool
-- (repeats across 1000+ rows are expected/realistic).
-- ~5% inactive, ~3% locked, IsEmailConfirmed = 1 for all (per spec).
-- ============================================================
WITH Numbers AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Numbers WHERE N < @CustomerCount
)
INSERT INTO Users (
    Id, TenantId, Email, UserName, FullName, PasswordHash,
    IsPhoneConfirmed, PhoneNumber, IsEmailConfirmed, IsActive, IsLocked, LockoutEnd,
    UserTypeCode, CreatedAt, UpdatedAt
)
SELECT
    CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
    @SystemTenantId,
    N'customer' + RIGHT(N'0000' + CONVERT(NVARCHAR(4), n.N), 4) + N'@example.com',
    N'customer' + RIGHT(N'0000' + CONVERT(NVARCHAR(4), n.N), 4),
    fam.Name + N' ' + gv.Name,
    @DemoPasswordHash,
    CASE WHEN n.N % 4 = 0 THEN 1 ELSE 0 END,
    N'09' + RIGHT(N'00000000' + CONVERT(NVARCHAR(8), (n.N * 137 + 11) % 100000000), 8),
    1, -- IsEmailConfirmed = 1 for all, per spec
    CASE WHEN n.N % 20 = 0 THEN 0 ELSE 1 END,  -- ~5% inactive
    CASE WHEN n.N % 33 = 0 THEN 1 ELSE 0 END,  -- ~3% locked
    CASE WHEN n.N % 33 = 0 THEN DATEADD(DAY, 7, GETUTCDATE()) ELSE NULL END,
    'Customer',
    DATEADD(DAY, -1 * ((n.N * 53 + 7) % 365), GETUTCDATE()),
    DATEADD(DAY, -1 * ((n.N * 53 + 7) % 365), GETUTCDATE())
FROM Numbers n
CROSS APPLY (
    SELECT Name FROM (
        SELECT Name, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS Idx
        FROM (VALUES
            (N'Nguyen'), (N'Tran'), (N'Le'), (N'Pham'), (N'Hoang'),
            (N'Huynh'), (N'Phan'), (N'Vu'), (N'Vo'), (N'Dang'),
            (N'Bui'), (N'Do'), (N'Ho'), (N'Ngo'), (N'Duong')
        ) AS t(Name)
    ) x WHERE x.Idx = (n.N - 1) % 15
) AS fam(Name)
CROSS APPLY (
    SELECT Name FROM (
        SELECT Name, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS Idx
        FROM (VALUES
            (N'An'), (N'Binh'), (N'Chi'), (N'Dung'), (N'Giang'),
            (N'Hoa'), (N'Khanh'), (N'Lan'), (N'Minh'), (N'Nga'),
            (N'Oanh'), (N'Phuc'), (N'Quyen'), (N'Son'), (N'Thao'),
            (N'Uyen'), (N'Viet'), (N'Xuan'), (N'Yen'), (N'Tam')
        ) AS t(Name)
    ) x WHERE x.Idx = ((n.N - 1) / 15) % 20
) AS gv(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM Users
    WHERE Id = CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
)
OPTION (MAXRECURSION 0);

-- CUSTOMER role for every customer user (matches UserType.Customer -> IUserRoleResolver).
IF @CustomerRoleId IS NOT NULL
BEGIN
    WITH Numbers AS (
        SELECT 1 AS N
        UNION ALL
        SELECT N + 1 FROM Numbers WHERE N < @CustomerCount
    )
    INSERT INTO UserRoles (UserId, RoleId, TenantId)
    SELECT
        CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
        @CustomerRoleId,
        @SystemTenantId
    FROM Numbers n
    WHERE NOT EXISTS (
        SELECT 1 FROM UserRoles ur
        WHERE ur.UserId = CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
          AND ur.RoleId = @CustomerRoleId
    )
    OPTION (MAXRECURSION 0);
END

-- Customer UserTenants (default membership in the system tenant).
;WITH Numbers AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Numbers WHERE N < @CustomerCount
)
INSERT INTO UserTenants (UserId, TenantId, IsDefault)
SELECT
    CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER),
    @SystemTenantId,
    1
FROM Numbers n
WHERE NOT EXISTS (
    SELECT 1 FROM UserTenants ut
    WHERE ut.UserId = CAST('20000000-0002-0000-0000-' + RIGHT('000000000000' + CONVERT(VARCHAR(12), n.N), 12) AS UNIQUEIDENTIFIER)
      AND ut.TenantId = @SystemTenantId
)
OPTION (MAXRECURSION 0);
GO
