# Deferred issues

Problems found while working through the ROADMAP that were **not fixed on purpose** (out of the plan's scope, needs a decision, or needs another roadmap item first).
Handle them after the main roadmap tasks; each plan that touches one of them should update its row. Last updated: 2026-10-24 (P1-2b done).

How to read: **Source** = where it was found; **Home** = the roadmap item or kind of work that should pick it up; **Impact** = why it matters.

## 1. Security and operations

| # | Issue | Source | Home | Impact |
|---|---|---|---|---|
| S1 | The local `sa` password is in git history and every service connects as `sa`. | P0-8 | P0-10 (least-privilege SQL logins) | Rotate the password; one login per service database. |
| S2 | `POST license/tenant/{id}/issue` returns 500: the validator has async rules but is run synchronously by ASP.NET. | P1-1 verification | Identity fix | A tenant cannot be licensed through the API (licence rows were inserted by SQL in tests). |
| S3 | The ADMIN role created by tenant sign-up holds 51 permissions and none for accounting, sales or payment. | P1-1 verification | Identity / onboarding | A new tenant cannot use those modules until roles are granted by hand. |
| S4 | Kubernetes `ForwardedHeaders__TrustedHops` is left at the default 1. | P0-8 | Deployment | Must equal the number of proxies in front of the Gateway, otherwise client IPs (rate limit, audit) are wrong. |
| S5 | Local `deployment/k8s/secrets.yaml` makes `NoSharedTokenKeyTests.NoDeploymentConfigurationSetsASharedTokenKey` fail (Architecture 53/54). | P0-8 | Clean the local file / template | The only failing architecture test; it hides real regressions. |
| S6 | `AutoMapper 12.0.1` has advisory NU1903 in PurchaseOrder (documented, intentional). | P0-8 | Dependency upgrade | Known vulnerability warning on every build. |
| S7 | `ICacheService` keys are global. Accounting's account queries used a tenant-less key (fixed in P1-2: no longer cached). Other services may cache tenant data the same way. | P1-2 | Audit all `ICacheService` call sites | Possible cross-tenant data exposure. |
| S8 | The admin SPA has no ESLint configuration (`npm run lint` fails); only `tsc -b` guards it. | P1-2 | Frontend tooling | No lint rules enforced. |
| S9 | Untracked file `src/frontend/admin/src/api/logs/logsClient.ts` has been sitting in the working tree. | since P0 | Owner decision | Commit it or delete it. |
| S10 | Test data left in the local databases: tenants `glcheck` (reset to a clean Vas99 chart in P1-2) and `p01check`, many orders / invoices / journal entries / pay slips in SYSTEM (pay periods 2025-03, 2025-04). | all verifications | Local clean-up | Pollutes local reports; do not carry to a shared environment. |

## 2. General-ledger posting (P1-1 follow-ups)

| # | Issue | Source | Home | Impact |
|---|---|---|---|---|
| G1 | Stock on hand before go-live and all documents before the posting releases are not back-posted. | P1-1, P1-1b | Opening-balance entry (manual) or an import tool | Account 1200 starts below the warehouse valuation. |
| G2 | A manual stock receipt (no PO, no bill) leaves a credit on GRNI that accounting must clear. | P1-1b | Accounting procedure | GRNI never reaches zero by itself. |
| G3 | FIFO / LIFO products **without lots** cost at the last receipt price (`Inventory.Cost`); only `Average` is weighted. | P1-1b | Warehouse costing | COGS differs from true FIFO. |
| G4 | Partial shipments appear to issue stock (and COGS) only when the order is fully shipped; unverified. | P1-1b (agent report) | Verify live, then Warehouse | COGS may lag for partially shipped orders. |
| G5 | POS: tax is not reduced by the discount (POS pricing rule). | P1-1c | POS pricing | Customer is charged tax on the undiscounted amount. |
| G6 | POS: a void or an offline sync after the shift is closed is not in the shift figures already booked. | P1-1c | POS shift handling | Cash variance can drift from the drawer. |
| G7 | POS: every tender other than "Cash" goes to the Bank key (no separate card / e-wallet clearing). | P1-1c | Posting keys per tender | Card and wallet settlements are not distinguishable in the ledger. |
| G8 | Payroll figures are the stub of P1-7: 5000 gross, 30 % lump deduction, no insurance / PIT split, no employer contributions, no cost centres; a paid slip cannot be cancelled; paying a legacy slip books nothing. | P1-1d | P1-7 | The ledger shows whatever HRM computes. |
| G9 | Refunds: returned goods are not restocked and their COGS is not reversed; nothing triggers the Payment refund from `SalesOrderReturned`; Sales has no return lines. | P1-1e | P1-5 | Revenue is reversed but cost is not; the refund must be requested by hand. |
| G10 | A Payment refund has no idempotency key; a gateway timeout is recorded as a failed refund (no refund IPN); two concurrent refunds can both pass the over-refund check; `RefundStatus.Processing` is unused. | P1-1e | Payment hardening | Possible double refund or a refund the gateway did process shown as failed. |
| G11 | No VAT on sales, returns, bills or POS (the `VatOutput` key exists but no flow fills it). | P1-1 | P1-3 | Tax payable is not tracked. |
| G12 | Single currency (VND); documents in another currency are not converted. Payment defaults to USD while the stack uses VND. | P1-1 | P1-4 | Wrong amounts for foreign-currency tenants. |
| G13 | Reporting marks a payment "Refunded" even for a partial refund and reads no `NetPay` for pay slips (total likely 0). | P1-1d, P1-1e | Reporting fix | Wrong BI figures. |
| G14 | Notification does not consume `PaymentRefunded` (the dependency map used to claim it did). | P1-1e | P1-6 | No refund receipts. |
| G15 | `SalesOrder.CompleteReturn` refusals are `InvalidOperationException`, which the error middleware does not map (HTTP 500 instead of 400/409). | P1-1e | Sales error handling | Misleading status code. |

## 3. Verified by unit tests only (not run live)

Ship-time COGS through a real sales order; POS sale and POS COGS; redelivery (same message twice) of stock, POS, payroll and refund events; cancel-after-payment; click-and-collect skip;
`PurchaseOrderReceivedConsumer` (no unit test at all). **Home:** a verification pass before the first real tenant.

## 4. Chart of accounts and closing (P1-2 follow-ups)

| # | Issue | Home |
|---|---|---|
| C1 | The `Vas99` account list and posting map were written from summaries of Circular 99/2025/TT-BTC, not the official text. An accountant must review them. | Accountant review (before any real use) |
| C2 | SYSTEM and demo data stay on the 14-account chart (seed joins by number). | Seed rewrite, optional |
| C3 | A tenant that already has journal entries cannot switch to another chart (apply-template refused). | Migration tool, if ever needed |
| C4 | Closing runs every period (monthly) and moves the result to retained earnings each time; year-end-only closing is not offered. | P1-2c |
| C5 | `FiscalPeriodClosed` has no consumer. | When a consumer needs it |
| C6 | Class 0 (off-balance) accounts are not supported. | P1-2c |

## 4b. Reports (P1-2b follow-ups)

| # | Issue | Home |
|---|---|---|
| R1 | The cash-flow statement is not the official B03-DN form (no indicator codes, no foreign-currency effect, interest and tax paid follow the account's category). Default categories are suggestions. | P1-2c, accountant review |
| R2 | The account ledger computes the running balance in memory over the whole range before paging; a very busy account over a very long range should use date-bounded snapshots. | Performance, if a tenant needs it |
| R3 | The cash-flow report needs accounts flagged `IsCash`; a tenant with its own chart must flag its cash / bank accounts (the screen says so when none is flagged). | Onboarding |
| R4 | The trial-balance list route changed content (roll-up on by default, opening-only accounts now appear); clients that sum the list would double count parents — they should use `trial-balance/full`. | API notes |
| R5 | Local DB `glcheck` now holds P1-2 / P1-2b verification entries on top of a clean Vas99 chart (October 2026 closed). | Local clean-up |

## 5. Roadmap items these issues are waiting for

P0-10 least-privilege SQL · P1-2c B01 / B02 layouts, off-balance accounts, year-end carry-forward, indirect-method cash flow · P1-3 VAT and e-invoice · P1-4 foreign currency and bank reconciliation ·
P1-5 saga completion and returns · P1-6 notifications · P1-7 real payroll.
