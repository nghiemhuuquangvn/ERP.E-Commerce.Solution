-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 011: reversal of operation reports and finished-goods receipts (Manufacturing Phase 4, P4-2).
-- A reversal is a stock document of type 5 (Reversal) whose ReversalOfId names the document it undoes; its lines say what they do in the Warehouse
-- (ReversalAction 1 Return, 2 Scrap, 3 Issue). The lots the Warehouse really used for a line are kept (MaterialTransactionLineLots, from Warehouse W7) so that
-- a reversal can give the stock back to the right lot(s). Append-only and idempotent.
-- ============================================================

ALTER TABLE "MaterialTransactions" ADD COLUMN IF NOT EXISTS "IsReversed" boolean NOT NULL DEFAULT false;     -- the document has been undone by a Posted reversal
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactions_ReversalOf" ON "MaterialTransactions" ("TenantId", "ReversalOfId") WHERE "ReversalOfId" IS NOT NULL;

ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "ReversalAction" smallint NULL;                -- Reversal documents only: 1 Return, 2 Scrap, 3 Issue
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "ReversesLineId" uuid     NULL;                -- the line of the original document this one undoes

CREATE TABLE IF NOT EXISTS "MaterialTransactionLineLots" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "LineId"      uuid          NOT NULL REFERENCES "MaterialTransactionLines" ("Id"),
    "Position"    integer       NOT NULL,
    "LotId"       uuid          NOT NULL,
    "LotNumber"   varchar(100)  NOT NULL,
    "Qty"         numeric(18,6) NOT NULL,
    "UnitCost"    numeric(18,6) NULL,
    "WarehouseId" uuid          NULL,
    "ParentLotId" uuid          NULL
);
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactionLineLots_Line" ON "MaterialTransactionLineLots" ("TenantId", "LineId");
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactionLineLots_Lot" ON "MaterialTransactionLineLots" ("TenantId", "LotId");

CREATE INDEX IF NOT EXISTS "IX_OperationReports_ReversalOf" ON "OperationReports" ("TenantId", "ReversalOfId") WHERE "ReversalOfId" IS NOT NULL;
