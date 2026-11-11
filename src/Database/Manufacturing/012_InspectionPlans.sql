-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 012: inspection plans (Manufacturing Phase 4, P4-3a).
-- BA spec 01 §5.15 BR-QC-01, §7.4. A plan is a SERIES (plant + Code) that exists in numbered versions (1 Draft, 2 Approved, 3 Obsolete) like a BOM; Code, Type
-- and Item belong to the series. The pointers ItemManufacturingProfiles.FqcInspectionPlanId, RoutingOperations.IpqcInspectionPlanId and
-- ProductionOrderOperations.IpqcInspectionPlanId name the series by the id of any of its versions. Append-only and idempotent.
-- ============================================================

CREATE TABLE IF NOT EXISTS "InspectionPlans" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "Code"                varchar(20)   NOT NULL,                   -- names the series; unique per plant with the version
    "Type"                smallint      NOT NULL,                   -- 1 Ipqc, 2 Fqc
    "PlantId"             uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "ItemId"              uuid          NOT NULL,                   -- ProductService product (no key: another service)
    "ItemCode"            varchar(100)  NOT NULL,                   -- snapshot for display
    "ItemName"            varchar(255)  NOT NULL,
    "Version"             integer       NOT NULL,
    "Status"              smallint      NOT NULL DEFAULT 1,         -- 1 Draft, 2 Approved, 3 Obsolete
    "SamplingMethod"      smallint      NOT NULL DEFAULT 1,         -- 1 Fixed, 2 Percent, 3 Full
    "SampleValue"         numeric(10,2) NULL,                       -- Fixed: number of units; Percent: the percentage; Full: not used
    "AllowedMinorDefects" integer       NOT NULL DEFAULT 0,
    "EffectiveFrom"       date          NOT NULL,
    "EffectiveTo"         date          NULL,
    "ApprovedBy"          varchar(200)  NULL,
    "ApprovedAt"          timestamptz   NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InspectionPlans_Tenant_Plant_Code_Version" ON "InspectionPlans" ("TenantId", "PlantId", "Code", "Version");
CREATE INDEX IF NOT EXISTS "IX_InspectionPlans_Tenant_Plant_Type_Status" ON "InspectionPlans" ("TenantId", "PlantId", "Type", "Status");
CREATE INDEX IF NOT EXISTS "IX_InspectionPlans_Tenant_Item" ON "InspectionPlans" ("TenantId", "ItemId");

CREATE TABLE IF NOT EXISTS "InspectionCharacteristics" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "PlanId"     uuid          NOT NULL REFERENCES "InspectionPlans" ("Id"),
    "Seq"        integer       NOT NULL,
    "Name"       varchar(200)  NOT NULL,
    "Kind"       smallint      NOT NULL,                            -- 1 Measurement, 2 Attribute
    "MinValue"   numeric(18,6) NULL,
    "MaxValue"   numeric(18,6) NULL,
    "Unit"       varchar(20)   NULL,
    "IsCritical" boolean       NOT NULL DEFAULT false
);
-- unique among live rows only: a removed characteristic does not block its number
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InspectionCharacteristics_Tenant_Plan_Seq_Live" ON "InspectionCharacteristics" ("TenantId", "PlanId", "Seq") WHERE "IsDeleted" = false;

-- the three pointers that were waiting for the plans ("FK added with inspection plans")
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ItemManufacturingProfiles_FqcInspectionPlan') THEN
        ALTER TABLE "ItemManufacturingProfiles" ADD CONSTRAINT "FK_ItemManufacturingProfiles_FqcInspectionPlan" FOREIGN KEY ("FqcInspectionPlanId") REFERENCES "InspectionPlans" ("Id");
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_RoutingOperations_IpqcInspectionPlan') THEN
        ALTER TABLE "RoutingOperations" ADD CONSTRAINT "FK_RoutingOperations_IpqcInspectionPlan" FOREIGN KEY ("IpqcInspectionPlanId") REFERENCES "InspectionPlans" ("Id");
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProductionOrderOperations_IpqcInspectionPlan') THEN
        ALTER TABLE "ProductionOrderOperations" ADD CONSTRAINT "FK_ProductionOrderOperations_IpqcInspectionPlan" FOREIGN KEY ("IpqcInspectionPlanId") REFERENCES "InspectionPlans" ("Id");
    END IF;
END $$;
