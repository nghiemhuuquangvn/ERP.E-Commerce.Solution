-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 004: bills of materials (versions and lines).
-- BA spec 01 §5.8 (BR-BOM-01..15), §7.2. Append-only and idempotent. Base columns of BaseEntity as in 001-003; optimistic locking uses xmin.
-- A version number is unique per (item, plant) and is never reused, even after a draft was deleted (the row stays, IsDeleted = true).
-- ============================================================

CREATE TABLE IF NOT EXISTS "Boms" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "ItemId"        uuid          NOT NULL,                   -- ProductService product (no key: another service)
    "PlantId"       uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "ItemCode"      varchar(100)  NOT NULL,                   -- snapshot for display
    "ItemName"      varchar(255)  NOT NULL,
    "Version"       integer       NOT NULL,
    "BaseQuantity"  numeric(18,6) NOT NULL DEFAULT 1,
    "YieldPct"      numeric(5,2)  NOT NULL DEFAULT 100,
    "Status"        smallint      NOT NULL DEFAULT 1,         -- 1 Draft, 2 Approved, 3 Obsolete
    "EffectiveFrom" date          NOT NULL,
    "EffectiveTo"   date          NULL,
    "ApprovedBy"    varchar(200)  NULL,
    "ApprovedAt"    timestamptz   NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Boms_Tenant_Item_Plant_Version" ON "Boms" ("TenantId", "ItemId", "PlantId", "Version");
CREATE INDEX IF NOT EXISTS "IX_Boms_Tenant_Plant_Status" ON "Boms" ("TenantId", "PlantId", "Status");

CREATE TABLE IF NOT EXISTS "BomLines" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "BomId"               uuid          NOT NULL REFERENCES "Boms" ("Id"),
    "LineNo"              integer       NOT NULL,
    "LineType"            smallint      NOT NULL DEFAULT 1,   -- 1 Component, 2 ByProduct, 3 CoProduct
    "ComponentItemId"     uuid          NOT NULL,
    "ComponentCode"       varchar(100)  NOT NULL,
    "ComponentName"       varchar(255)  NOT NULL,
    "Quantity"            numeric(18,6) NOT NULL,
    "UomId"               uuid          NOT NULL,             -- MasterData unit of the line
    "ScrapPct"            numeric(5,2)  NOT NULL DEFAULT 0,
    "IsFixedQuantity"     boolean       NOT NULL DEFAULT false,
    "IsPhantom"           boolean       NOT NULL DEFAULT false,
    "IssueMethod"         smallint      NOT NULL DEFAULT 1,   -- 1 Manual, 2 Backflush
    "OperationSeq"        integer       NULL,
    "AlternativeGroup"    varchar(20)   NULL,
    "AlternativePriority" integer       NULL,
    "CostAllocationPct"   numeric(5,2)  NULL,
    "Notes"               varchar(500)  NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_BomLines_Tenant_Bom_LineNo" ON "BomLines" ("TenantId", "BomId", "LineNo");
CREATE INDEX IF NOT EXISTS "IX_BomLines_Tenant_Component" ON "BomLines" ("TenantId", "ComponentItemId");
