-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 013: IPQC / FQC inspections (Manufacturing Phase 4, P4-3b).
-- BA spec 01 §5.15 BR-QC-02..06, §7.4. An inspection is created Pending when a receipt line is Posted (FQC) or a Good report is applied (IPQC); results are saved as a draft
-- and submitted (Passed / Failed, then immutable). Results are kept per characteristic AND per sample unit (SampleNo), because a measurement is taken on every unit of the
-- sample (TC-QC-007). A Failed inspection is re-inspected by a new document (ReinspectionOfId). Append-only and idempotent.
-- ============================================================

CREATE TABLE IF NOT EXISTS "Inspections" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "DocNo"              varchar(30)   NOT NULL,                    -- QI-yyMM-nnnnn, one counter per plant and month (like the operation reports), so unique per plant
    "PlantId"            uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "Type"               smallint      NOT NULL,                    -- 1 Ipqc, 2 Fqc
    "PlanId"             uuid          NOT NULL REFERENCES "InspectionPlans" ("Id"),   -- the version used
    "PlanCode"           varchar(20)   NOT NULL,
    "PlanVersion"        integer       NOT NULL,
    "OrderId"            uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "OrderNo"            varchar(30)   NOT NULL,
    "OrderOperationId"   uuid          NULL,                        -- IPQC
    "OperationSeq"       integer       NULL,
    "OperationReportId"  uuid          NULL,
    "ReceiptDocumentId"  uuid          NULL,                        -- FQC
    "ReceiptLineId"      uuid          NULL,
    "ItemId"             uuid          NOT NULL,
    "ItemCode"           varchar(100)  NOT NULL,
    "ItemName"           varchar(255)  NOT NULL,
    "LotId"              uuid          NULL,                        -- the Warehouse lot (FQC)
    "LotNumber"          varchar(100)  NULL,
    "LotQty"             numeric(18,6) NOT NULL,
    "SampleQty"          numeric(18,6) NOT NULL,
    "Status"             smallint      NOT NULL DEFAULT 1,          -- 1 Pending, 2 Passed, 3 Failed, 4 Cancelled
    "InspectedBy"        varchar(200)  NULL,
    "InspectedAt"        timestamptz   NULL,
    "ReinspectionOfId"   uuid          NULL,
    "ReinspectedById"    uuid          NULL,
    "NcrRequired"        boolean       NOT NULL DEFAULT false,
    "ReleasedAt"         timestamptz   NULL
);
DROP INDEX IF EXISTS "IX_Inspections_Tenant_DocNo";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Inspections_Tenant_Plant_DocNo" ON "Inspections" ("TenantId", "PlantId", "DocNo");
-- one first inspection per source: a retried posting never creates a second one
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Inspections_Tenant_ReceiptLine" ON "Inspections" ("TenantId", "ReceiptLineId") WHERE "ReceiptLineId" IS NOT NULL AND "ReinspectionOfId" IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Inspections_Tenant_Report" ON "Inspections" ("TenantId", "OperationReportId") WHERE "OperationReportId" IS NOT NULL AND "ReinspectionOfId" IS NULL;
CREATE INDEX IF NOT EXISTS "IX_Inspections_Tenant_Status_Type" ON "Inspections" ("TenantId", "PlantId", "Status", "Type");
CREATE INDEX IF NOT EXISTS "IX_Inspections_Tenant_Order" ON "Inspections" ("TenantId", "OrderId");
CREATE INDEX IF NOT EXISTS "IX_Inspections_Tenant_Lot" ON "Inspections" ("TenantId", "LotId");

CREATE TABLE IF NOT EXISTS "InspectionResults" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "InspectionId"    uuid          NOT NULL REFERENCES "Inspections" ("Id"),
    "CharacteristicId" uuid         NOT NULL REFERENCES "InspectionCharacteristics" ("Id"),
    "SampleNo"        integer       NOT NULL,                       -- 1..sample units for a measurement; 1 for an attribute
    "MeasuredValue"   numeric(18,6) NULL,
    "AttributePassed" boolean       NULL,
    "IsPassed"        boolean       NOT NULL,
    "Note"            varchar(500)  NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InspectionResults_Tenant_Inspection_Char_Sample" ON "InspectionResults" ("TenantId", "InspectionId", "CharacteristicId", "SampleNo");

-- P4-3: Good output of an operation that waits for its in-process inspection or failed it; the next operation cannot take it (net output = Good - held)
ALTER TABLE "ProductionOrderOperations" ADD COLUMN IF NOT EXISTS "IpqcHeldQty" numeric(18,6) NOT NULL DEFAULT 0;
