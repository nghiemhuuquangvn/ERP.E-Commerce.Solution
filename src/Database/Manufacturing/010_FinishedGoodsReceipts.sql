-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 010: finished-goods receipts, completion and closing (Phase 3, P3-5).
-- A receipt is a stock document (type 4) whose lines are either material lines (the backflush of materials no operation owns) or receipt lines
-- (the main product and co- / by-products: lot, dates, quality hold). Orders carry how much issued material was settled at close.
-- Append-only and idempotent.
-- ============================================================

-- receipt lines have no order material
ALTER TABLE "MaterialTransactionLines" ALTER COLUMN "OrderMaterialId" DROP NOT NULL;
ALTER TABLE "MaterialTransactionLines" ALTER COLUMN "MaterialLineNo" DROP NOT NULL;

ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "Kind"            smallint     NOT NULL DEFAULT 1;   -- 1 Material, 2 Receipt
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "OrderOutputId"   uuid         NULL;                 -- co- / by-product of the order; NULL on the main product
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "OutputLineNo"    integer      NULL;
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "LotNumber"       varchar(100) NULL;
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "ExpiryDate"      timestamptz  NULL;
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "ManufactureDate" timestamptz  NULL;
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "IsQcHold"        boolean      NOT NULL DEFAULT false;
ALTER TABLE "MaterialTransactionLines" ADD COLUMN IF NOT EXISTS "QcReleasedAt"    timestamptz  NULL;
CREATE INDEX IF NOT EXISTS "IX_MaterialTransactionLines_LotNumber" ON "MaterialTransactionLines" ("TenantId", "LotNumber") WHERE "LotNumber" IS NOT NULL;

-- how much of the net issued quantity was carried to consumption when the order was closed (BR-PO-16, condition 4)
ALTER TABLE "ProductionOrderMaterials" ADD COLUMN IF NOT EXISTS "SettledIssueQty" numeric(18,6) NOT NULL DEFAULT 0;
