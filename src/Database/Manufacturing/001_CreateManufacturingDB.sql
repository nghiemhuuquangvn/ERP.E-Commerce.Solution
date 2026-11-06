-- ============================================================
-- ManufacturingDB (PostgreSQL 16) - ManufacturingService (port 5018, code MFG)
-- Migration 001: base tables. Append-only and idempotent (IF NOT EXISTS): the pg-migrate job runs every file on
-- every start. The database itself is created by deployment/docker/run-pg-migrations.sh (CREATE DATABASE cannot
-- run inside a script that is applied in one go).
--
-- Column rules (same for every table): Id uuid PK, TenantId uuid NOT NULL, IsDeleted, audit columns (UTC),
-- Description; optimistic locking uses the system column xmin (no column here). Business codes are unique per
-- (TenantId, ...) - never globally.
-- ============================================================

CREATE TABLE IF NOT EXISTS "DocumentSequences" (
    "Id"          uuid          NOT NULL PRIMARY KEY,
    "TenantId"    uuid          NOT NULL,
    "Description" varchar(2000) NULL,
    "IsDeleted"   boolean       NOT NULL DEFAULT false,
    "CreatedBy"   varchar(200)  NULL,
    "CreatedAt"   timestamptz   NOT NULL DEFAULT now(),
    "UpdatedBy"   varchar(200)  NULL,
    "UpdatedAt"   timestamptz   NOT NULL DEFAULT now(),
    "DocType"     varchar(10)   NOT NULL,
    "PlantId"     uuid          NOT NULL,
    "Period"      date          NOT NULL,
    "LastValue"   bigint        NOT NULL
);

-- The counter row of one document type, plant and month: the target of the atomic upsert (BR-GEN-04).
CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentSequences_TenantId_DocType_PlantId_Period"
    ON "DocumentSequences" ("TenantId", "DocType", "PlantId", "Period");
