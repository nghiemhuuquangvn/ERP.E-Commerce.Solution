# ADR - Manufacturing service (code MFG) on PostgreSQL

**Status:** accepted by the owner, 2026-11-06 · **Scope:** the Manufacturing / Production module of
`docs/business/M/01_PRODUCTION_BA_SPEC.md` (BR-*), `02_PRODUCTION_DEV_TASKS.md`, `03_PRODUCTION_TEST_CASES.md`.
ROADMAP P2-8 asked for an ADR (own service vs inside an existing one) before this module starts.

## Decisions

1. **Own service.** `ManufacturingService` (`src/services/ManufacturingService`, projects `ERP.MFG.{API,Application,Domain,Infrastructure}`),
   port **5018** (7018), module code **MFG**, database **ManufacturingDB**. Clean Architecture / DDD / CQRS / MediatR like every service.
   The code is `MFG`, not the spec's `PRD`, because `PRD` is ProductService.
2. **PostgreSQL** for this service only (owner decision 2026-11-06: the server installed on the host machine, `localhost:5432`, not a Docker container; containers reach it as `host.docker.internal`; credentials only in the git-ignored `.env`: `PG_HOST`, `PG_PORT`, `PG_USER`, `PG_PASSWORD`; the local server is PostgreSQL 18, so keep SQL compatible with 16-18) (every other service stays on SQL Server). EF Core with Npgsql 10; `Guid` ids from `BaseEntity` + `TenantId`;
   `timestamptz` (UTC); `numeric(18,6)` quantities, `numeric(18,4)` money; optimistic locking with the `xmin` system column (HTTP 409, BR-GEN-03).
   Tenant isolation is unchanged (`ITenantScopedDbContext`, `ApplyTenantQueryFilters`, `EnforceTenantOnSave`).
3. **Migrations are append-only, idempotent SQL scripts** in `src/Database/Manufacturing/*.sql`, applied by the `pg-migrate` job
   (`deployment/docker/run-pg-migrations.sh`; creates the database when missing). Not part of the SQL Server runners
   (`DatabaseRunnerTests` skips the folder). A PostgreSQL schema-drift test replaces `LiveSchemaDriftTests` for this service (open, see DEFERRED-ISSUES).
4. **No cross-service database access.** Items (Product), units of measure (MasterData), warehouses (Warehouse) and employees (MasterData) are
   referenced by id and read through internal REST clients (`internal/v1/...`, service tokens, fail closed, short tenant-keyed cache). Manufacturing
   owns the manufacturing facts about an item (make / buy, lead times, lot policy, FQC) in `ItemManufacturingProfile`.
5. **One database transaction with Inventory is impossible** (BR-MAT-06, NFR-05 assumed a monolith). From Phase 3 a stock document (issue, return,
   receipt, backflush) is created `Pending`, sent to Warehouse with an idempotency key (its document number), and becomes `Posted` with the returned
   unit cost; a retry job completes or reverses documents left `Pending` / `Failed`. Warehouse must accept the same key twice. To be detailed in the Phase 3 ADR.
6. **Names.** Errors `MFG_{AREA}_{NNN}` (same numbers as the spec's `PRD_*`; catalogue in `MfgErrors`), permissions `manufacturing:<resource>:<action>`,
   license module `MFG`, routes `api/v1/manufacturing/...`, Gateway prefix `/mfg/`, documents `MO-yyMM-00001` (per spec, per tenant + plant + month).
   Business codes are unique per `(TenantId, ...)`, never globally (the spec's global plant code rule).
7. **Document numbers** come from the `DocumentSequences` table with one atomic `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` (`DocumentNumberService`).
8. **Validation** runs through `ValidationBehavior` (MediatR pipeline): failures become a `BusinessRuleException` (HTTP 400) carrying the validator's `MFG_*` code.

## Consequences / risks
- A second database technology to operate (backup, monitoring, drift test, k8s StatefulSet, skills).
- Phase 3 needs a Warehouse upgrade first (decimal quantities, lot status, production movement types, internal idempotent write API, cost returned on issue) - see the plan in PROJECT-STATUS.
- Product has no lot / serial-controlled flags and MasterData no unit decimal places; both are added in the phase that needs them.
- Existing tenants are repaired by Identity migration 033 (module `MFG` on PROFESSIONAL / ENTERPRISE plans, `manufacturing:*` permissions for SuperAdmin and Admin).
