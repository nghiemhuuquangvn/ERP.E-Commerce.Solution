-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 006: production orders (Phase 3, P3-1) and the outbox.
-- BA spec 01 §5.10, §7.3. Append-only and idempotent. Base columns of BaseEntity as in 001-005; optimistic locking uses xmin.
-- Order lines (materials, operations, outputs) are a snapshot of the BOM and routing; a regenerated Draft marks its old lines IsDeleted, so
-- the unique keys of the lines are filtered on IsDeleted = false.
-- ============================================================

CREATE TABLE IF NOT EXISTS "ProductionOrders" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "OrderNo"         varchar(30)   NOT NULL,                  -- MO-yyMM-00001, unique per tenant (BR-PO-01)
    "PlantId"         uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "OrderType"       smallint      NOT NULL DEFAULT 1,        -- 1 Standard, 2 Rework, 3 Subcontract
    "ItemId"          uuid          NOT NULL,                  -- ProductService product (no key: another service)
    "ItemCode"        varchar(100)  NOT NULL,
    "ItemName"        varchar(255)  NOT NULL,
    "Quantity"        numeric(18,6) NOT NULL,
    "UomId"           uuid          NOT NULL,                  -- MasterData unit: the item's base unit
    "BomId"           uuid          NOT NULL REFERENCES "Boms" ("Id"),
    "RoutingId"       uuid          NULL REFERENCES "Routings" ("Id"),
    "Status"          smallint      NOT NULL DEFAULT 1,        -- 1 Draft, 2 Released, 3 InProgress, 4 OnHold, 5 Completed, 6 Closed, 7 Cancelled
    "StatusBeforeHold" smallint     NULL,
    "Priority"        smallint      NOT NULL DEFAULT 3,        -- 1 highest .. 5
    "DueDate"         date          NOT NULL,
    "PlannedStart"    timestamptz   NOT NULL,
    "PlannedEnd"      timestamptz   NOT NULL,
    "ActualStart"     timestamptz   NULL,
    "ActualEnd"       timestamptz   NULL,
    "IsLate"          boolean       NOT NULL DEFAULT false,
    "OutputWarehouseId" uuid        NOT NULL,                  -- Warehouse service warehouse of the plant
    "SourceType"      smallint      NOT NULL DEFAULT 1,        -- 1 Manual, 2 SalesOrder, 3 Mrp, 4 Ncr, 5 ParentOrder
    "SourceRefId"     uuid          NULL,
    "ParentOrderId"   uuid          NULL REFERENCES "ProductionOrders" ("Id"),
    "CompletedQty"    numeric(18,6) NOT NULL DEFAULT 0,
    "ScrappedQty"     numeric(18,6) NOT NULL DEFAULT 0,
    "IsRework"        boolean       NOT NULL DEFAULT false,
    "Notes"           varchar(1000) NULL,
    "ReservationId"   uuid          NULL,                      -- Warehouse reservation (P3-2)
    "ReservationReleasePending" boolean NOT NULL DEFAULT false
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductionOrders_Tenant_OrderNo" ON "ProductionOrders" ("TenantId", "OrderNo");
CREATE INDEX IF NOT EXISTS "IX_ProductionOrders_Tenant_Plant_Status_Due" ON "ProductionOrders" ("TenantId", "PlantId", "Status", "DueDate");
CREATE INDEX IF NOT EXISTS "IX_ProductionOrders_Tenant_Item_Status" ON "ProductionOrders" ("TenantId", "ItemId", "Status");
CREATE INDEX IF NOT EXISTS "IX_ProductionOrders_Tenant_Parent" ON "ProductionOrders" ("TenantId", "ParentOrderId");

CREATE TABLE IF NOT EXISTS "ProductionOrderMaterials" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "OrderId"         uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "LineNo"          integer       NOT NULL,
    "ComponentItemId" uuid          NOT NULL,
    "ComponentCode"   varchar(100)  NOT NULL,
    "ComponentName"   varchar(255)  NOT NULL,
    "UomId"           uuid          NOT NULL,
    "OperationSeq"    integer       NULL,
    "RequiredQty"     numeric(18,6) NOT NULL,
    "IssuedQty"       numeric(18,6) NOT NULL DEFAULT 0,
    "ReturnedQty"     numeric(18,6) NOT NULL DEFAULT 0,
    "ConsumedQty"     numeric(18,6) NOT NULL DEFAULT 0,        -- backflush
    "ReservedQty"     numeric(18,6) NOT NULL DEFAULT 0,
    "ScrapPct"        numeric(5,2)  NOT NULL DEFAULT 0,
    "IssueMethod"     smallint      NOT NULL DEFAULT 1,        -- 1 Manual, 2 Backflush
    "WarehouseId"     uuid          NOT NULL,
    "IsSubstitute"    boolean       NOT NULL DEFAULT false,
    "SubstituteForLineNo" integer   NULL,
    "AlternativeGroup" varchar(20)  NULL,
    "IsUnplanned"     boolean       NOT NULL DEFAULT false
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductionOrderMaterials_Order_Line" ON "ProductionOrderMaterials" ("TenantId", "OrderId", "LineNo") WHERE "IsDeleted" = false;
CREATE INDEX IF NOT EXISTS "IX_ProductionOrderMaterials_Tenant_Component" ON "ProductionOrderMaterials" ("TenantId", "ComponentItemId");

CREATE TABLE IF NOT EXISTS "ProductionOrderOperations" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "OrderId"         uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "OperationSeq"    integer       NOT NULL,
    "Name"            varchar(200)  NOT NULL,
    "WorkCenterId"    uuid          NOT NULL REFERENCES "WorkCenters" ("Id"),
    "WorkCenterCode"  varchar(20)   NOT NULL,
    "SetupTimeMinutes" numeric(10,2) NOT NULL DEFAULT 0,
    "RunTimeMinutes"  numeric(10,4) NOT NULL DEFAULT 0,
    "RunTimeBasis"    smallint      NOT NULL DEFAULT 1,        -- 1 PerUnit, 2 PerBatch
    "BatchSize"       numeric(18,6) NULL,
    "QueueTimeMinutes" integer      NOT NULL DEFAULT 0,
    "MoveTimeMinutes" integer       NOT NULL DEFAULT 0,
    "IsSubcontract"   boolean       NOT NULL DEFAULT false,
    "IpqcInspectionPlanId" uuid     NULL,                      -- quality plans: Phase 4
    "Status"          smallint      NOT NULL DEFAULT 1,        -- 1 Pending, 2 Ready, 3 Running, 4 Paused, 5 Completed
    "StandardMinutes" numeric(12,2) NOT NULL DEFAULT 0,
    "CalendarMinutes" integer       NOT NULL DEFAULT 0,
    "PlannedStart"    timestamptz   NULL,
    "PlannedEnd"      timestamptz   NULL,
    "ActualStart"     timestamptz   NULL,
    "ActualEnd"       timestamptz   NULL,
    "GoodQty"         numeric(18,6) NOT NULL DEFAULT 0,
    "ScrapQty"        numeric(18,6) NOT NULL DEFAULT 0,
    "ReworkQty"       numeric(18,6) NOT NULL DEFAULT 0,
    "IpqcPassedQty"   numeric(18,6) NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductionOrderOperations_Order_Seq" ON "ProductionOrderOperations" ("TenantId", "OrderId", "OperationSeq") WHERE "IsDeleted" = false;
CREATE INDEX IF NOT EXISTS "IX_ProductionOrderOperations_Tenant_WorkCenter_Status" ON "ProductionOrderOperations" ("TenantId", "WorkCenterId", "Status");

CREATE TABLE IF NOT EXISTS "ProductionOrderOutputs" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "OrderId"         uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "LineNo"          integer       NOT NULL,
    "LineType"        smallint      NOT NULL,                  -- 2 ByProduct, 3 CoProduct
    "ItemId"          uuid          NOT NULL,
    "ItemCode"        varchar(100)  NOT NULL,
    "ItemName"        varchar(255)  NOT NULL,
    "UomId"           uuid          NOT NULL,
    "PlannedQty"      numeric(18,6) NOT NULL,
    "ReceivedQty"     numeric(18,6) NOT NULL DEFAULT 0,
    "CostAllocationPct" numeric(5,2) NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductionOrderOutputs_Order_Line" ON "ProductionOrderOutputs" ("TenantId", "OrderId", "LineNo") WHERE "IsDeleted" = false;

CREATE TABLE IF NOT EXISTS "ProductionOrderStatusHistories" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "OrderId"         uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "FromStatus"      smallint      NOT NULL,
    "ToStatus"        smallint      NOT NULL,
    "ReasonCodeId"    uuid          NULL REFERENCES "ReasonCodes" ("Id"),
    "Note"            varchar(500)  NULL,
    "ChangedBy"       varchar(200)  NOT NULL,
    "ChangedAt"       timestamptz   NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_ProductionOrderStatusHistories_Order_At" ON "ProductionOrderStatusHistories" ("TenantId", "OrderId", "ChangedAt");

-- The outbox (shared OutboxMessage shape, ERP.Shared.Messaging). Not a tenant-owned table: the tenant travels inside the event.
CREATE TABLE IF NOT EXISTS "OutboxMessages" (
    "Id"             uuid          NOT NULL PRIMARY KEY,
    "Type"           varchar(500)  NOT NULL,
    "Content"        text          NOT NULL,
    "OccurredOnUtc"  timestamptz   NOT NULL,
    "ProcessedOnUtc" timestamptz   NULL,
    "Error"          text          NULL
);
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Pending" ON "OutboxMessages" ("OccurredOnUtc") WHERE "ProcessedOnUtc" IS NULL;
