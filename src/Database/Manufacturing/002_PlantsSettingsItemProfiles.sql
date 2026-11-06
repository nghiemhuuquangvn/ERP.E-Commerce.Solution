-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 002: plants, production parameters (+ audit), item manufacturing profiles, plant access.
-- BA spec 01 §5.1-5.3, §7.1. Append-only and idempotent. Every table has the base columns of BaseEntity (see 001);
-- optimistic locking uses xmin. Codes are unique per tenant, never globally.
-- ============================================================

CREATE TABLE IF NOT EXISTS "Plants" (
    "Id"                       uuid          NOT NULL PRIMARY KEY,
    "TenantId"                 uuid          NOT NULL,
    "Description"              varchar(2000) NULL,
    "IsDeleted"                boolean       NOT NULL DEFAULT false,
    "CreatedBy"                varchar(200)  NULL,
    "CreatedAt"                timestamptz   NOT NULL DEFAULT now(),
    "UpdatedBy"                varchar(200)  NULL,
    "UpdatedAt"                timestamptz   NOT NULL DEFAULT now(),
    "Code"                     varchar(20)   NOT NULL,
    "Name"                     varchar(200)  NOT NULL,
    "TimeZoneId"               varchar(50)   NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    "RawMaterialWarehouseId"   uuid          NOT NULL,
    "FinishedGoodsWarehouseId" uuid          NOT NULL,
    "QuarantineWarehouseId"    uuid          NOT NULL,
    "ScrapWarehouseId"         uuid          NULL,
    "DefaultCalendarId"        uuid          NULL,   -- FK to WorkCalendars is added with the calendars (M2)
    "IsActive"                 boolean       NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Plants_TenantId_Code" ON "Plants" ("TenantId", "Code");

CREATE TABLE IF NOT EXISTS "ProductionSettings" (
    "Id"                            uuid          NOT NULL PRIMARY KEY,
    "TenantId"                      uuid          NOT NULL,
    "Description"                   varchar(2000) NULL,
    "IsDeleted"                     boolean       NOT NULL DEFAULT false,
    "CreatedBy"                     varchar(200)  NULL,
    "CreatedAt"                     timestamptz   NOT NULL DEFAULT now(),
    "UpdatedBy"                     varchar(200)  NULL,
    "UpdatedAt"                     timestamptz   NOT NULL DEFAULT now(),
    "PlantId"                       uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "OverProductionTolerancePct"    numeric(5,2)  NOT NULL DEFAULT 0,
    "UnderCompletionTolerancePct"   numeric(5,2)  NOT NULL DEFAULT 0,
    "OverIssueTolerancePct"         numeric(5,2)  NOT NULL DEFAULT 0,
    "BlockReleaseOnShortage"        boolean       NOT NULL DEFAULT false,
    "AllowNegativeStockOnBackflush" boolean       NOT NULL DEFAULT false,
    "RequireSequentialOperations"   boolean       NOT NULL DEFAULT true,
    "AutoCompleteOnFullReceipt"     boolean       NOT NULL DEFAULT true,
    "DefaultIssueMethod"            smallint      NOT NULL DEFAULT 1,   -- 1 Manual, 2 Backflush
    "CostingMethod"                 smallint      NOT NULL DEFAULT 1,   -- 1 Actual, 2 Standard
    "MrpHorizonDays"                integer       NOT NULL DEFAULT 90,
    "MaxBomLevels"                  integer       NOT NULL DEFAULT 20,
    "FgLotNumberFormat"             varchar(100)  NOT NULL DEFAULT '{ItemCode}-{yyMMdd}-{seq2}'
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductionSettings_TenantId_PlantId" ON "ProductionSettings" ("TenantId", "PlantId");

CREATE TABLE IF NOT EXISTS "ProductionSettingAudits" (
    "Id"          uuid          NOT NULL PRIMARY KEY,
    "TenantId"    uuid          NOT NULL,
    "Description" varchar(2000) NULL,
    "IsDeleted"   boolean       NOT NULL DEFAULT false,
    "CreatedBy"   varchar(200)  NULL,
    "CreatedAt"   timestamptz   NOT NULL DEFAULT now(),
    "UpdatedBy"   varchar(200)  NULL,
    "UpdatedAt"   timestamptz   NOT NULL DEFAULT now(),
    "PlantId"     uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "Field"       varchar(100)  NOT NULL,
    "OldValue"    varchar(200)  NOT NULL,
    "NewValue"    varchar(200)  NOT NULL,
    "ChangedBy"   varchar(200)  NOT NULL,
    "ChangedAt"   timestamptz   NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_ProductionSettingAudits_TenantId_PlantId_ChangedAt" ON "ProductionSettingAudits" ("TenantId", "PlantId", "ChangedAt");

CREATE TABLE IF NOT EXISTS "ItemManufacturingProfiles" (
    "Id"                   uuid          NOT NULL PRIMARY KEY,
    "TenantId"             uuid          NOT NULL,
    "Description"          varchar(2000) NULL,
    "IsDeleted"            boolean       NOT NULL DEFAULT false,
    "CreatedBy"            varchar(200)  NULL,
    "CreatedAt"            timestamptz   NOT NULL DEFAULT now(),
    "UpdatedBy"            varchar(200)  NULL,
    "UpdatedAt"            timestamptz   NOT NULL DEFAULT now(),
    "ItemId"               uuid          NOT NULL,           -- ProductService product; no cross-service FK
    "PlantId"              uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "ItemCode"             varchar(100)  NOT NULL,           -- snapshot for display
    "ItemName"             varchar(255)  NOT NULL,
    "MakeOrBuy"            smallint      NOT NULL,           -- 1 Make, 2 Buy, 3 Both
    "FixedLeadTimeDays"    integer       NOT NULL DEFAULT 0,
    "PurchaseLeadTimeDays" integer       NOT NULL DEFAULT 0,
    "LotSizingPolicy"      smallint      NOT NULL DEFAULT 1, -- 1 LotForLot, 2 FixedQty, 3 MinMultiple, 4 PeriodOrder
    "FixedLotQty"          numeric(18,6) NULL,
    "MinOrderQty"          numeric(18,6) NULL,
    "OrderMultiple"        numeric(18,6) NULL,
    "PeriodDays"           integer       NULL,
    "SafetyStockQty"       numeric(18,6) NOT NULL DEFAULT 0,
    "StandardLotSize"      numeric(18,6) NOT NULL DEFAULT 1,
    "DefaultIssueMethod"   smallint      NULL,
    "RequiresFqc"          boolean       NOT NULL DEFAULT false,
    "FqcInspectionPlanId"  uuid          NULL,               -- FK added with inspection plans (Phase 4)
    "ShelfLifeDays"        integer       NOT NULL DEFAULT 0,
    "LowLevelCode"         integer       NOT NULL DEFAULT 0,
    "StandardCost"         numeric(18,6) NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ItemManufacturingProfiles_TenantId_ItemId_PlantId" ON "ItemManufacturingProfiles" ("TenantId", "ItemId", "PlantId");
CREATE INDEX IF NOT EXISTS "IX_ItemManufacturingProfiles_TenantId_PlantId_ItemCode" ON "ItemManufacturingProfiles" ("TenantId", "PlantId", "ItemCode");

CREATE TABLE IF NOT EXISTS "PlantUserAccesses" (
    "Id"          uuid          NOT NULL PRIMARY KEY,
    "TenantId"    uuid          NOT NULL,
    "Description" varchar(2000) NULL,
    "IsDeleted"   boolean       NOT NULL DEFAULT false,
    "CreatedBy"   varchar(200)  NULL,
    "CreatedAt"   timestamptz   NOT NULL DEFAULT now(),
    "UpdatedBy"   varchar(200)  NULL,
    "UpdatedAt"   timestamptz   NOT NULL DEFAULT now(),
    "PlantId"     uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "UserId"      uuid          NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlantUserAccesses_TenantId_PlantId_UserId" ON "PlantUserAccesses" ("TenantId", "PlantId", "UserId");
CREATE INDEX IF NOT EXISTS "IX_PlantUserAccesses_TenantId_UserId" ON "PlantUserAccesses" ("TenantId", "UserId");
