-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 005: routings (versions and operations).
-- BA spec 01 §5.9 (BR-RT-01..08), §7.2. Append-only and idempotent. Base columns of BaseEntity as in 001-004; optimistic locking uses xmin.
-- A version number is unique per (item, plant) and never reused, even after a draft was deleted (the row stays, IsDeleted = true).
-- ============================================================

CREATE TABLE IF NOT EXISTS "Routings" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "ItemId"        uuid          NOT NULL,                   -- ProductService product (no key: another service)
    "PlantId"       uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "ItemCode"      varchar(100)  NOT NULL,                   -- snapshot for display
    "ItemName"      varchar(255)  NOT NULL,
    "Version"       integer       NOT NULL,
    "Status"        smallint      NOT NULL DEFAULT 1,         -- 1 Draft, 2 Approved, 3 Obsolete
    "EffectiveFrom" date          NOT NULL,
    "EffectiveTo"   date          NULL,
    "ApprovedBy"    varchar(200)  NULL,
    "ApprovedAt"    timestamptz   NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Routings_Tenant_Item_Plant_Version" ON "Routings" ("TenantId", "ItemId", "PlantId", "Version");
CREATE INDEX IF NOT EXISTS "IX_Routings_Tenant_Plant_Status" ON "Routings" ("TenantId", "PlantId", "Status");

CREATE TABLE IF NOT EXISTS "RoutingOperations" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "RoutingId"            uuid          NOT NULL REFERENCES "Routings" ("Id"),
    "OperationSeq"         integer       NOT NULL,
    "StandardOperationId"  uuid          NULL,                -- where the values were copied from; a reference only (BR-OPS-01)
    "Name"                 varchar(200)  NOT NULL,
    "WorkCenterId"         uuid          NOT NULL REFERENCES "WorkCenters" ("Id"),
    "WorkCenterCode"       varchar(20)   NOT NULL,            -- snapshot for display
    "SetupTimeMinutes"     numeric(10,2) NOT NULL DEFAULT 0,
    "RunTimeMinutes"       numeric(10,4) NOT NULL,
    "RunTimeBasis"         smallint      NOT NULL DEFAULT 1,  -- 1 PerUnit, 2 PerBatch
    "BatchSize"            numeric(18,6) NULL,
    "QueueTimeMinutes"     integer       NOT NULL DEFAULT 0,
    "MoveTimeMinutes"      integer       NOT NULL DEFAULT 0,
    "IsSubcontract"        boolean       NOT NULL DEFAULT false,
    "IpqcInspectionPlanId" uuid          NULL,                -- quality plans arrive with Phase 4
    "Instructions"         varchar(2000) NULL
);
-- unique among live rows only: a removed operation does not block its number
DROP INDEX IF EXISTS "IX_RoutingOperations_Tenant_Routing_Seq";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_RoutingOperations_Tenant_Routing_Seq_Live" ON "RoutingOperations" ("TenantId", "RoutingId", "OperationSeq") WHERE "IsDeleted" = false;
CREATE INDEX IF NOT EXISTS "IX_RoutingOperations_Tenant_WorkCenter" ON "RoutingOperations" ("TenantId", "WorkCenterId");
