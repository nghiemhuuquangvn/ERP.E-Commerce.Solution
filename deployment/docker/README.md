# Running the platform with Docker (self-contained, no host SQL Server needed)

`docker-compose.yml` lives at the repo root (not in this folder — its build
contexts are relative to the repo root, so it can't move without rewriting every
service's `context: .`). This folder holds the helper script the compose file
mounts into the one-shot `db-migrate` service.

## What changed (2026-07-29)

Previously every service's connection string pointed at `host.docker.internal,1433`
— i.e. you had to install and run SQL Server on the **host machine** yourself
before `docker compose up` would work at all. That's why nothing in this repo
could be exercised on a machine that only has Docker.

Now:
- A `sqlserver` service (official `mssql/server:2022-latest` image) runs SQL
  Server **inside** the compose network. Every service's connection string
  points at `sqlserver,1433` instead of the host.
- A one-shot `db-migrate` service waits for `sqlserver` to report healthy, then
  runs [`run-migrations.sh`](run-migrations.sh): applies
  `deployment/sql/init-databases.sql`, then every service's
  `src/Database/{Service}/*.sql` migration file in numeric filename order (each
  service's own `001_Create*.sql` already does
  `IF NOT EXISTS (...) CREATE DATABASE X; GO` + `USE X; GO`, so the script never
  needs a folder-name → database-name map). Every 15 DB-owning service now
  `depends_on: db-migrate: condition: service_completed_successfully`, so
  `docker compose up` alone bootstraps schema + data before any service starts.

## Quick start (any machine with Docker installed)

```bash
git clone <repo-url>
cd ERP.E-Commerce.Solution
cp .env.example .env        # set SA_PASSWORD / RABBITMQ_PASSWORD (no JWT secret: IdentityService
                            # generates its RS256 signing / encryption certificates on first start,
                            # on the identity_signing volume — docs/business/Identity/SigningKeys.md)
docker compose up --build -d
docker compose logs -f db-migrate   # watch migrations apply, then Ctrl+C
```

That's it — no separate SQL Server install, no manually running `.sql` files.

## Re-running migrations later

Safe to re-run anytime (every migration file uses `IF NOT EXISTS` guards):
```bash
docker compose up db-migrate
```

## Verifying the 2026-07-29 permission-architecture consolidation

The specific thing this setup was added to let you test — see
`docs/business/Identity/module-02-identity-security.md` §12 for the full
write-up:

1. `docker compose up --build -d sqlserver db-migrate identityservice` and wait
   for `db-migrate` to exit 0 (`docker compose logs db-migrate` should end with
   `All migrations applied successfully.`, including
   `013_SeedRoleDefinitionsIntoRolePermissions.sql`).
2. Confirm the seed landed:
   ```bash
   docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C \
     -Q "SELECT COUNT(*) FROM AuthDB.dbo.Permissions; SELECT r.RoleCode, COUNT(*) FROM AuthDB.dbo.RolePermissions rp JOIN AuthDB.dbo.Roles r ON r.Id = rp.RoleId GROUP BY r.RoleCode;"
   ```
   Expect 244 permission rows total, and non-zero grants for `SuperAdmin`/`Admin`/
   `Manager`/`Purchaser`/`Viewer` roles (`Manager`/`Purchaser` will only show up
   here if a role with that exact code exists in some tenant — the seed migration
   matches by `RoleCode` across all tenants, it doesn't create roles).
3. Log in as a seeded user (SuperAdmin/Admin from `001_CreateAuthDB.sql`), decode
   the returned `accessToken` (e.g. jwt.io) and confirm the `permission` claims
   match what `ERP.Shared.Authorization.RoleDefinitions.GetPermissionsForRoles(...)`
   used to produce for that role — the whole point of the migration was for the
   JWT to come out identical, just DB-sourced instead of hardcoded.
4. Hit an endpoint gated by one of the newly-migrated `[AuthorizePermission(...)]`
   attributes (e.g. `GET /api/v1/roles` needs `identity:roles:view`) with that
   token and confirm it's no longer 401/403 for a role that should have it.

## Why this globs files instead of using `src/Database/RunAllScripts.ps1`

This repo already has two hand-maintained PowerShell runners
(`RunAllScripts.ps1`, `RunAllScripts_sqlcmd.ps1`) that list every migration file
explicitly in dependency order. `run-migrations.sh` deliberately does **not**
reuse that list: a disk-vs-list diff done while writing this script found
`RunAllScripts.ps1` is missing **16 real migration files** that exist on disk
(e.g. `CRM/002_FixCrmEntityColumns.sql`, `Sales/006` through `009`,
`Reporting/002_AddMissingEntityColumns.sql` — the one with the confirmed
never-applied `DateKey` index bug, see
`docs/architecture/database-performance-indexing-review.md` §5 — and 10 more).
It's drifted out of sync as new migrations were added over time. Globbing every
`*.sql` file per folder in filename order is less curated but strictly more
complete; that tradeoff is worth documenting, not silently reproducing the same
staleness in a second script. `RunAllScripts.ps1` was still updated to include
the new `013_SeedRoleDefinitionsIntoRolePermissions.sql` (directly relevant to
this session's work), but its other gaps are a pre-existing issue, out of scope
here.

## Known limitations of `run-migrations.sh`

- Only applies `*.sql` files directly inside each `src/Database/{Service}/`
  folder (`-maxdepth 1`) — it does not descend into `Functions/`, `Procedures/`,
  `Scripts/`, or `Views/` subfolders, and skips non-`.sql` files like
  `AllServices/004_FixMissingBaseEntityColumns.ps1`.
- `Promotion/` and `PromotionService/` both target `PromotionDB` under
  inconsistent folder names (a pre-existing issue, see
  `docs/architecture/database-performance-indexing-review.md` §7) — folders are
  processed in alphabetical order, which happens to apply `Promotion/001_...`
  before `PromotionService/002_...`, but this is incidental, not guaranteed by
  the script.
- A failed individual migration file is logged and does **not** stop the rest of
  the run (so one folder's pre-existing issue doesn't block every other
  service's schema from being created) — always scroll the full `db-migrate`
  log, not just the last line, before assuming a clean run.

## Using the SQL Server installed on the host (2026-10-23)

If port 1433 is already taken by a host SQL Server, or you want to keep its data, skip the `sqlserver`
container:

```bash
# .env:  SQL_HOST=host.docker.internal   SA_PASSWORD=<the host's sa password>
docker compose -f docker-compose.yml -f docker-compose.localsql.yml up -d --build
```

- The host instance must accept SQL logins and listen on TCP 1433; the Windows firewall must allow Docker.
- `db-migrate` then runs against your existing databases: back them up first. Their schema may have drifted from
  the code (`docs/architecture/schema-drift-2026-10-23.md`), and `db-migrate` now prints how many SQL errors it saw
  — "already exists" errors (1913/2714) are expected on an existing database.
- Docker Desktop on Windows: give WSL at least 8 GB (`~/.wslconfig`, `memory=12GB`). The default 4 GB makes the
  engine stop answering during the first build.
- Shell scripts must stay LF (`.gitattributes`); a CRLF checkout makes admin/storefront crash-loop.
