-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 008: stock documents (Phase 3, P3-3).
-- A stock document (material issue / return now; backflush and receipts later) is stored Pending, each line is sent to the Warehouse as ONE movement
-- under the key "<DocNo>#<n>" (free part "...F", compensation "...R"), and the document becomes Posted or Failed. Append-only and idempotent.
-- ============================================================

CREATE TABLE IF NOT EXISTS "MaterialTransactions" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "DocNo"           varchar(30)   NOT NULL,
    "PlantId"         uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "OrderId"         uuid          NOT NULL REFERENCES "ProductionOrders" ("Id"),
    "OrderNo"         varchar(30)   NOT NULL,
    "Type"            smallint      NOT NULL,                  -- 1 Issue, 2 Return (3 Backflush: P3-4)
    "Status"          smallint      NOT NULL DEFAULT 1,        -- 1 Pending, 2 Posted, 3 Failed
    "Mode"            smallint      NOT NULL DEFAULT 1,        -- 1 Forward, 2 Rollback (compensating the lines already posted)
    "PostingDate"     date          NOT NULL,
    "ReasonCodeId"    uuid          NULL REFERENCES "ReasonCodes" ("Id"),
    "Note"            varchar(500)  NULL,
    "IdempotencyKey"  varchar(100)  NULL,
    "RequestHash"     varchar(64)   NULL,
    "Attempts"        integer       NOT NULL DEFAULT 0,
    "NextAttemptAt"   timestamptz   NULL,
    "LastError"       varchar(500)  NULL,
    "LastErrorCode"   varchar(60)   NULL,
    "PostedAt"        timestamptz   NULL,
    "ReversalOfId"    uuid          NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaterialTransactions_Tenant_DocNo" ON "MaterialTransactions" ("TenantId", "DocNo");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaterialTransactions_Tenant_IdemKey" ON "MaterialTransactions" ("TenantId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactions_Order" ON "MaterialTransactions" ("TenantId", "OrderId");
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactions_Pending" ON "MaterialTransactions" ("TenantId", "NextAttemptAt") WHERE "Status" = 1;
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactions_Plant_Status" ON "MaterialTransactions" ("TenantId", "PlantId", "Status", "CreatedAt");

CREATE TABLE IF NOT EXISTS "MaterialTransactionLines" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "TransactionId"   uuid          NOT NULL REFERENCES "MaterialTransactions" ("Id"),
    "LineNo"          integer       NOT NULL,
    "OrderMaterialId" uuid          NOT NULL REFERENCES "ProductionOrderMaterials" ("Id"),
    "MaterialLineNo"  integer       NOT NULL,
    "ItemId"          uuid          NOT NULL,
    "ItemCode"        varchar(100)  NOT NULL,
    "WarehouseId"     uuid          NOT NULL,
    "LotId"           uuid          NULL,
    "Qty"             numeric(18,6) NOT NULL,
    "IsFromReservation" boolean     NOT NULL DEFAULT false,
    "MovementKey"     varchar(100)  NOT NULL,
    "Status"          smallint      NOT NULL DEFAULT 1,        -- 1 Pending, 2 Posted, 3 Refused, 4 Compensated
    "WhMovementId"    uuid          NULL,
    "UnitCost"        numeric(18,6) NULL,
    "Amount"          numeric(18,4) NULL,
    "PostedLotId"     uuid          NULL,
    "CompensationMovementId" uuid   NULL,
    "Error"           varchar(500)  NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaterialTransactionLines_Doc_Line" ON "MaterialTransactionLines" ("TenantId", "TransactionId", "LineNo");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaterialTransactionLines_Tenant_Key" ON "MaterialTransactionLines" ("TenantId", "MovementKey");
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactionLines_Material" ON "MaterialTransactionLines" ("TenantId", "OrderMaterialId");
