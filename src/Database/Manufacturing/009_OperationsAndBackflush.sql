-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 009: operation execution and backflush (Phase 3, P3-4).
-- Time logs (employee sessions on an operation), operation reports (good / scrap / rework, applied to the order when their backflush document is Posted),
-- and the backflush columns of the stock documents. Append-only and idempotent.
-- ============================================================

CREATE TABLE IF NOT EXISTS "OperationReports" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "DocNo"                 varchar(30)   NOT NULL,
    "PlantId"               uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "OrderId"               uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "OrderNo"               varchar(30)   NOT NULL,
    "OrderOperationId"      uuid          NOT NULL REFERENCES "ProductionOrderOperations" ("Id"),
    "OperationSeq"          integer       NOT NULL,
    "ReportedAt"            timestamptz   NOT NULL,
    "PostingDate"           date          NOT NULL,
    "GoodQty"               numeric(18,6) NOT NULL DEFAULT 0,
    "ScrapQty"              numeric(18,6) NOT NULL DEFAULT 0,
    "ReworkQty"             numeric(18,6) NOT NULL DEFAULT 0,
    "ScrapReasonId"         uuid          NULL REFERENCES "ReasonCodes" ("Id"),
    "ReworkReasonId"        uuid          NULL REFERENCES "ReasonCodes" ("Id"),
    "MachineId"             uuid          NULL,
    "ReportedBy"            varchar(200)  NOT NULL,
    "Status"                smallint      NOT NULL DEFAULT 1,        -- 1 Pending (waiting for its backflush), 2 Posted (applied), 3 Failed
    "BackflushDocumentId"   uuid          NULL,
    "FailureCode"           varchar(60)   NULL,
    "FailureMessage"        varchar(500)  NULL,
    "IdempotencyKey"        varchar(100)  NULL,
    "RequestHash"           varchar(64)   NULL,
    "ReversalOfId"          uuid          NULL,                      -- for T4.2 (reversal of a report); unused in P3-4
    "IsReversed"            boolean       NOT NULL DEFAULT false,
    "Note"                  varchar(500)  NULL,
    "PostedAt"              timestamptz   NULL
);
-- operation reports are numbered per plant and month (BR-GEN-04, unlike MI / MR / BF whose number is the Warehouse key), so the number is unique per plant
DROP INDEX IF EXISTS "IX_OperationReports_Tenant_DocNo";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_OperationReports_Plant_DocNo" ON "OperationReports" ("TenantId", "PlantId", "DocNo");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_OperationReports_Tenant_IdemKey" ON "OperationReports" ("TenantId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_OperationReports_Order" ON "OperationReports" ("TenantId", "OrderId");
CREATE INDEX IF NOT EXISTS "IX_OperationReports_Operation_Status" ON "OperationReports" ("TenantId", "OrderOperationId", "Status");

CREATE TABLE IF NOT EXISTS "LaborTimeLogs" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "OrderId"               uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "OrderOperationId"      uuid          NOT NULL REFERENCES "ProductionOrderOperations" ("Id"),
    "OperationSeq"          integer       NOT NULL,
    "EmployeeId"            uuid          NOT NULL,
    "EmployeeNumber"        varchar(50)   NOT NULL,
    "EmployeeName"          varchar(200)  NOT NULL,
    "MachineId"             uuid          NULL,
    "StartAt"               timestamptz   NOT NULL,
    "EndAt"                 timestamptz   NULL,
    "PausedAt"              timestamptz   NULL,
    "PausedMinutes"         numeric(10,2) NOT NULL DEFAULT 0,
    "DurationMinutes"       numeric(10,2) NULL,
    "LaborRatePerHour"      numeric(18,4) NULL,
    "IsManual"              boolean       NOT NULL DEFAULT false
);
CREATE INDEX IF NOT EXISTS "IX_LaborTimeLogs_Employee_Start" ON "LaborTimeLogs" ("TenantId", "EmployeeId", "StartAt");
CREATE INDEX IF NOT EXISTS "IX_LaborTimeLogs_Operation_Open" ON "LaborTimeLogs" ("TenantId", "OrderOperationId") WHERE "EndAt" IS NULL;
CREATE INDEX IF NOT EXISTS "IX_LaborTimeLogs_Order" ON "LaborTimeLogs" ("TenantId", "OrderId");

-- Backflush documents (type 3) point at the report they consume for; a line can book stock below zero (AllowNegativeStockOnBackflush).
ALTER TABLE "MaterialTransactions" ADD COLUMN IF NOT EXISTS "SourceOperationReportId" uuid NULL;
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactions_Report" ON "MaterialTransactions" ("TenantId", "SourceOperationReportId") WHERE "SourceOperationReportId" IS NOT NULL;
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "IsNegativeStock" boolean NOT NULL DEFAULT false;
