-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 007: reservation reconciliation of production orders (Phase 3, P3-2).
-- The order commits its new state together with ReservationSyncPending = true; IOrderReservationSync (inline, then the ReservationSyncJob) brings the
-- Warehouse reservation in line and clears the flag. Append-only and idempotent.
-- ============================================================

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProductionOrders' AND column_name = 'ReservationReleasePending')
       AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ProductionOrders' AND column_name = 'ReservationSyncPending') THEN
        ALTER TABLE "ProductionOrders" RENAME COLUMN "ReservationReleasePending" TO "ReservationSyncPending";
    END IF;
END $$;

ALTER TABLE "ProductionOrders" ADD COLUMN IF NOT EXISTS "ReservationSyncPending"   boolean      NOT NULL DEFAULT false;
ALTER TABLE "ProductionOrders" ADD COLUMN IF NOT EXISTS "ReservationSyncAttempts"  integer      NOT NULL DEFAULT 0;
ALTER TABLE "ProductionOrders" ADD COLUMN IF NOT EXISTS "ReservationSyncError"     varchar(500) NULL;
ALTER TABLE "ProductionOrders" ADD COLUMN IF NOT EXISTS "ReservationNextAttemptAt" timestamptz  NULL;

-- the job looks for flagged orders across tenants
CREATE INDEX IF NOT EXISTS "IX_ProductionOrders_ReservationSyncPending" ON "ProductionOrders" ("TenantId", "ReservationNextAttemptAt") WHERE "ReservationSyncPending" = true;
