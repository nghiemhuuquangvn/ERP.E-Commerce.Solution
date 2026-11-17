-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 014: non-conformance reports, dispositions and rework orders (Manufacturing Phase 4, P4-4).
-- BA spec 01 §5.15 BR-NCR-01..04, BR-RW-01..03, §7.4. A Failed inspection creates its NCR; the quality manager splits the quantity into Rework / Scrap / UseAsIs lines
-- and executes them one by one. A Rework line makes a rework order: no BOM (ProductionOrders.BomId becomes optional) and one material = the held lot (SourceLotId).
-- The issue of that lot is a held-lot issue (MaterialTransactionLines.AllowHeldLot + MovementReason). Append-only and idempotent.
-- ============================================================

CREATE TABLE IF NOT EXISTS "NonConformances" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "NcrNo"             varchar(30)   NOT NULL,                    -- NCR-yyMM-nnnnn, one counter per tenant and month: the number is also the key of the Warehouse movements
    "PlantId"           uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "SourceType"        smallint      NOT NULL,                    -- 1 Inspection, 2 OperationReport (record only)
    "InspectionId"      uuid          NULL REFERENCES "Inspections" ("Id"),
    "OperationReportId" uuid          NULL,
    "OrderId"           uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "OrderNo"           varchar(30)   NOT NULL,
    "OperationSeq"      integer       NULL,                        -- IPQC / report source
    "ItemId"            uuid          NOT NULL,
    "ItemCode"          varchar(100)  NOT NULL,
    "ItemName"          varchar(255)  NOT NULL,
    "LotId"             uuid          NULL,                        -- FQC source: the held Warehouse lot
    "LotNumber"         varchar(100)  NULL,
    "ReceiptDocumentId" uuid          NULL,
    "ReceiptLineId"     uuid          NULL,
    "NonconformingQty"  numeric(18,6) NOT NULL,
    "Status"            smallint      NOT NULL DEFAULT 1,          -- 1 Open, 2 InDisposition, 3 Closed, 4 Cancelled
    "ClosedAt"          timestamptz   NULL,
    "ClosedBy"          varchar(200)  NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_NonConformances_Tenant_NcrNo" ON "NonConformances" ("TenantId", "NcrNo");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_NonConformances_Tenant_Inspection" ON "NonConformances" ("TenantId", "InspectionId") WHERE "InspectionId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_NonConformances_Tenant_Plant_Status" ON "NonConformances" ("TenantId", "PlantId", "Status");
CREATE INDEX IF NOT EXISTS "IX_NonConformances_Tenant_Order" ON "NonConformances" ("TenantId", "OrderId");

CREATE TABLE IF NOT EXISTS "NcrDispositions" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "NcrId"            uuid          NOT NULL REFERENCES "NonConformances" ("Id"),
    "LineNo"           integer       NOT NULL,
    "Type"             smallint      NOT NULL,                     -- 1 Rework, 2 Scrap, 3 UseAsIs
    "Qty"              numeric(18,6) NOT NULL,
    "ReasonCodeId"     uuid          NULL,                         -- Scrap: the Scrap reason
    "Note"             varchar(500)  NULL,
    "ApprovedBy"       varchar(200)  NULL,                         -- UseAsIs: who approved it (qc:approve-deviation)
    "ApprovedAt"       timestamptz   NULL,
    "ReworkOrderId"    uuid          NULL,                         -- Rework: the order made for the line
    "MovementKey"      varchar(60)   NULL,
    "ResultLotId"      uuid          NULL,                         -- UseAsIs: the lot that was released
    "ResultLotNumber"  varchar(100)  NULL,
    "IsExecuted"       boolean       NOT NULL DEFAULT false,
    "ExecutedAt"       timestamptz   NULL,
    "ExecutedBy"       varchar(200)  NULL
);
-- the lines are replaced as a set while none has started: the replaced ones stay as deleted rows, so a line number is unique among the live ones
CREATE UNIQUE INDEX IF NOT EXISTS "IX_NcrDispositions_Tenant_Ncr_Line" ON "NcrDispositions" ("TenantId", "NcrId", "LineNo") WHERE "IsDeleted" = false;
CREATE INDEX IF NOT EXISTS "IX_NcrDispositions_Tenant_ReworkOrder" ON "NcrDispositions" ("TenantId", "ReworkOrderId");

-- BR-RW-01: a rework order has no BOM (its one material is the non-conforming lot)
ALTER TABLE "ProductionOrders" ALTER COLUMN "BomId" DROP NOT NULL;

-- BR-RW-02: the material of a rework order is one fixed lot on quality hold
ALTER TABLE "ProductionOrderMaterials" ADD COLUMN IF NOT EXISTS "SourceLotId" uuid NULL;
ALTER TABLE "ProductionOrderMaterials" ADD COLUMN IF NOT EXISTS "SourceLotNumber" varchar(100) NULL;

-- the issue of that lot asks the Warehouse for a held-lot issue and names the rework order in the reason (R267)
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "AllowHeldLot" boolean NOT NULL DEFAULT false;
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "MovementReason" varchar(200) NULL;
