# Manufacturing live checks

Scripts that drive the **running stack** (Gateway on 127.0.0.1:5000, the real Warehouse) the way a user or another service would, and print `OK` / `FAIL` lines ending in `ALL OK` or `n FAILED`. They are for **dev / demo tenants only**: every run creates a throw-away plant and DS-01 product set with prefix `ZZ...` (nothing is deleted afterwards), and the logins in them are the documented dev accounts of the local stack (glcheck, SYSTEM).

| Script | Proves |
|---|---|
| `mfg-m4.js`, `mfg-m5.js`, `mfg-m6.js` | engineering data: BOM / routing CSV import, Admin proxy, demo data (M4-M6) |
| `mfg-p31.js` | order core: create, snapshot, schedule, list (P3-1) |
| `mfg-p32.js` | release, reservation, hold, change of quantity, Warehouse outage on reservation (P3-2) |
| `mfg-p33.js` | material issue / return, pending documents, Warehouse stopped mid-issue (P3-3) |
| `mfg-p34.js` | time logs, operation report, backflush (P3-4) |
| `mfg-p35.js` | finished-goods receipt, lots, complete / short-close / reopen / close, Warehouse stopped (P3-5) |
| `mfg-p36-contract-check.js` | every key of the Admin's TypeScript types (`mfg.api.ts`) against the real JSON (P3-6) |
| `mfg-p43.js` | inspection plans (TC-QC-001 / 002), FQC pass / fail / re-inspect / minor defects (TC-QC-006..013), IPQC (TC-QC-014 / 015), reversal rules, Warehouse outage at submit (P4-3) |
| `mfg-p37-e2e.js` | TC-E2E-001, TC-E2E-005, TC-GEN-006 (50 parallel numbers), R219, R234, 403 checks (P3-7) |
| `wh-w3.js`, `wh-w5.js`, `wh-w6.js`, `wh-w7.js` | Warehouse side: lots / balances, internal stock API, DS-01 stock, lot split / per-lot breakdown / held-lot rework issue (W7) |
| `run-all.js` | runs all of the above for one tenant and prints a table |
| `tc-trace.js` | which test case of `docs/business/M/03_PRODUCTION_TEST_CASES.md` is cited by which test (traceability matrix) |

## Run
```
node scripts/manufacturing-live/run-all.js --tenant glcheck      # or --tenant system; --only mfg-p37-e2e,mfg-p35; --pause 65
node scripts/manufacturing-live/mfg-p37-e2e.js                   # one script; MFG_HOST / MFG_EMAIL / MFG_PASSWORD switch tenant
node scripts/manufacturing-live/tc-trace.js --groups PO,MAT,OP,FG --uncited
```
Needs: the stack up (`docker compose up -d`), the Gateway reachable on 127.0.0.1:5000, tenant hosts in /etc/hosts (`glcheck.erp.local`, `admin.erp.local`), Docker access (some scripts stop and start `erp-warehouse-service` on purpose), `sqlcmd` and `SA_PASSWORD` in `.env` for the Warehouse scripts that read the Accounting database. The Gateway rate-limits sign-in, so `run-all.js` waits 65 s between scripts (the whole run takes about an hour per tenant). `m4`-`m6` and `wh-w3` were written for glcheck; they take the tenant from the environment but may assume its data.

PostgreSQL schema drift is checked by a unit test, not by these scripts: `ERP_DRIFT_PG="Host=localhost;Port=5432;Username=postgres;Password=..." dotnet test tests/unit/ERP.Architecture.Tests --filter PostgreSqlSchemaDriftTests`.
