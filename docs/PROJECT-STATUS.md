# Project Status & Handoff

> Read this right after `CLAUDE.md` when picking the work up on another machine.
> Keep it current: update it at the end of every work session (what changed, what's next).
> Last updated: **2026-10-23** (ROADMAP P0-1 first Docker run on `fix/docker-first-run`; new P0-9 schema drift; system inventory + roadmap: `docs/ROADMAP.md`).

> Open problems that were deliberately left unfixed: **docs/DEFERRED-ISSUES.md** (read it before picking the next task).

## 0. Start here (new machine / new developer)

1. `git fetch && git switch master && git pull` — all finished work is on `master` (§1).
   Branches not merged yet are listed in §1; never start from them unless told to.
2. Read, in order: `CLAUDE.md` (rules — especially "Multi-Tenant Isolation", "API
   Authorization", "Outbox rules"), this file (§7 decisions, §8 next step), then the service
   README / `docs/business/...` of whatever you touch.
3. Verify the baseline (§5). If anything is red before you change code, stop and report it.
4. Work the agreed way (§6): plan → owner approval → implement + tests + docs, one branch per
   plan, one commit per step, never push/merge without being asked. Update this file at the
   end of the session.
5. Before changing a DbContext, a consumer, a background job or an event: re-read CLAUDE.md
   "Multi-Tenant Isolation" — `tests/unit/ERP.Architecture.Tests` fails the build if a
   tenant-owned entity is not filtered, and two-tenant tests exist for every service.

## 1. Where things stand

| Branch | State | Content |
|---|---|---|
| `master` (origin) | includes PR #1–#9 | Outbox/EventId fixes + Identity hardening (`73970b5`); CRM tenant isolation (PR #1); user/role/permission isolation + tenant = request host (PR #2); service authorization phase 1 (PR #3); tenant isolation phase 2 for all services (PR #4 Shared+Sales/Payment/Accounting, PR #5 supply chain, PR #6 cross-cutting); handoff docs (PR #7); token revocation, phase 3 (PR #8); RS256/JWKS, phase 4 release 1 (PR #9) |
| `feature/service-tokens` | **not merged yet** (push/PR when the owner asks) | Service-to-service tokens (each service its own key), `/internal` endpoints, shared `Jwt:Key` removed everywhere, SaleOrder URL fix, docs (see §2) |
| `docs/system-inventory-roadmap` | **not merged yet**, branched from `feature/service-tokens` (merge after it) | Docs only: `docs/ROADMAP.md`, this file, a pointer in `ERP-Business-Analysis.md` |
| `fix/roadmap-p0` | **not merged yet**, branched from `docs/system-inventory-roadmap` (merge after it) | ROADMAP P0-2…P0-6 (see §2 2026-10-22), migration Identity `024` |

All feature branches above are merged; start new work from `origin/master`.
Verify the baseline first (§5) — everything below was green on 2026-10-20.

## 2. Completed work (newest first)

### 2026-10-24 — ROADMAP P1-2b: ledger, trial balance, cash flow (`feat/reports-ledger-cashflow`, unmerged)
- New reports (docs/business/Accounting/Reports.md): `account-ledger` (opening, lines with counter-accounts and running balance, sub-accounts rolled in, paged), `trial-balance/full` (opening / period /
  closing columns, roll-up to parents, totals + balanced check; the old list route keeps working), `cash-flow` (direct method by counter-account: operating / investing / financing, opening and closing
  cash, reconciliation check; cash↔bank transfers ignored, closing and voided entries excluded). Accounts get `IsCash` and `CashFlowCategory` (editable; defaults in the templates).
- **Needs Accounting migration `010_CashFlowAndLedger.sql`** (two columns + back-fill of the standard cash / investing / financing numbers). Drift test clean.
- Admin: Reports page tabs Ledger and Cash Flow, six-column trial balance, account modal fields. en / vi.
- Verified live (tenant glcheck, Vas99): loan, equipment, rent, customer payment, bank→cash transfer, sale on credit → cash flow operating / investing / financing, reconciled; trial balance totals balanced
  and parent 111 / 112 rolled up; ledger of the parent and of 131; closing October left the cash flow unchanged and showed the closing entry in the ledger of 5111.
- **New file docs/DEFERRED-ISSUES.md**: every problem left unfixed on purpose so far (security / ops, GL posting, verification gaps, chart) — read it before picking the next task.
- Tests: ACC 137, admin `tsc -b` clean.

### 2026-10-24 — ROADMAP P1-2: chart of accounts, void / reverse, period-end closing (`feat/vas-chart`, unmerged)
- Accounts: parent / sub-account tree (`GET accounts/tree`, `PUT accounts/{id}`, activate / deactivate with safety checks); a parent takes no postings (manual entries, posting-profile PUT and the
  engine refuse it). **Found and fixed:** the account list / get used a cache key without a tenant (`acc:accounts:all`), so one tenant could be served another's chart for 5 minutes — the
  account queries are no longer cached. Other services may use tenant-less cache keys too (not audited).
- Templates: `Simple14` (default, unchanged) and `Vas99` (~110 accounts, Circular 99/2025/TT-BTC which replaced Circular 200 from 2026-01-01; **suggested chart, accountant review required**);
  `POST accounting/chart/apply-template` only for a tenant without journal entries; new posting keys IncomeSummary / RetainedEarnings (added lazily, no migration);
  `Accounting:DefaultChartTemplate` for new tenants. SYSTEM / demo stay on the 14 accounts.
- Void (hand-written entry, open period) and reverse (optional date, no longer tied to the auto-posting switch); closing: checks + one `PeriodClosing` entry (revenue / expenses → 911 → 421) and
  reopen (voids it); P&L leaves out closing entries; the balance sheet adds the unclosed result so it balances before closing. Admin UI: accounts tree, reopen period, reverse / void buttons.
- Verified live in Docker (tenant glcheck, reset to a clean state): apply Vas99 refused while entries exist, then 109 accounts + 17 keys; 111 refused as a posting target, 1111 accepted; void, reverse
  at a date; BS balanced before closing via the unclosed result; close → 5111 / 6421 cleared into 911 and 4212 (700); P&L still 1000 / 300; reopen voids the entry; close again posts a new one.
- Not done (P1-2b): cash-flow statement, general ledger report, B01–B03 layouts, off-balance accounts. Tests: ACC 117, admin `tsc -b` clean (no eslint config in the repo).

### 2026-10-24 — ROADMAP P1-1e: refunds and returns → GL (`feat/gl-refunds`, unmerged) — closes P1-1
- Payment: `PaymentRefundedEvent` is raised by `Payment.CompleteRefund` only after the gateway confirmed (it used to be raised when the refund was requested, so declined
  refunds were published too); it carries `RefundId`, `Currency`, `Method`, `ProcessedAt`, `GatewayRefundId`. Payment events are Kafka-keyed by payment id. Over-refund → 409 (was 500).
  New `tests/unit/ERP.PAY.UnitTests`.
- Sales: `CompleteReturn` refuses a refund ≤ 0 or above the order total.
- Accounting: refund → Dr 1100 / Cr 1000|bank and the invoice's `AmountPaid` goes down; `SalesOrderReturned` → Dr 4000 (key `SalesReturns`) / Cr 1100; cancelling a paid order now
  cancels the invoice and reverses the revenue (cash stays as a receivable credit until the refund is booked). No migration.
- Not done: restock + COGS reversal of returned goods, refund triggered by a return, return lines (all P1-5); refund idempotency key / gateway timeout reconciliation.
- Verified live in Docker (SYSTEM tenant): invoice 1000 + captured payment; refund 400 → entry Dr 1100 / Cr 1000 and invoice `AmountPaid` 600, status reopened; over-refund refused;
  `SalesOrderReturned` published to RabbitMQ → Dr 4000 / Cr 1100 1000; refund of the remaining 600 → AR of the order nets to 0. Cancel-after-payment is covered by unit tests only.
- Tests: PAY 7 (new project), SO 6, ACC 88.

### 2026-10-24 — ROADMAP P1-1d: payroll → GL (`feat/gl-payroll`, unmerged)
- HRM: a pay slip can now be paid (`POST hrm/payslips/{id}/pay`) or cancelled while pending (`…/cancel`); `PaySlipPaidEvent` / `PaySlipVoidedEvent` are new,
  `PaySlipGeneratedEvent` carries gross, overtime, deductions and the period end. A cancelled slip no longer blocks its month (HRM migration `007`, filtered
  unique index; HRM 005–007 were also missing from `RunAllScripts.ps1`). Pay slip events are Kafka-keyed by pay slip id.
- Accounting consumes `hrm-topic`: Dr 5200 / Cr 2100 (net) + Cr 2100 (deductions) dated the end of the pay period (at most today); payment Dr 2100 / Cr bank;
  cancellation reverses. Keys `SalaryExpense`, `SalariesPayable`, `PayrollWithholdingsPayable` are added lazily. The consumer refreshes topic metadata every
  30 s (a not-yet-created `hrm-topic` otherwise left the subscription idle for 5 minutes).
- Still stubbed: payroll figures (5000 gross, 30 % lump deduction) — ROADMAP P1-7 brings insurance, PIT, employer cost.
- Verified live in Docker (SYSTEM tenant): 3 slips 2025-04 → 3 salary entries; pay → Dr 2100 / Cr 1000 3600; second pay and cancel-after-pay → 400; cancel + regenerate →
  entry Reversed + reversal + new entry. A first run showed the cancel overtaking its generation with random Kafka keys — fixed by keying on the pay slip.
- Tests: HRM 8 (new project), ACC 81. Needs HRM migration `007`.

### 2026-10-24 — ROADMAP P1-1c: POS sale → GL (`feat/gl-pos`, on top of `feat/gl-stock-cogs`, unmerged)
- POS events now carry the money: `TransactionCreated/Voided` → `Amounts` (subtotal, discount, tax, tenders), shift id, external payment id;
  `CashShiftClosed` → opening, counted, expected cash and variance. Accounting consumes `pos-topic` and posts Dr Cash / Bank, Cr Revenue
  (net of discount) + Cr VAT; a void reverses it; a cash difference goes to the new `CashOverShort` key (5100). COGS stays with Warehouse.
- Fixed in POS: the shift's `TotalSales`/`TransactionCount` ignored voids; `ExpectedCashBalance`/`CashVariance` were never used (now computed at
  close from the shift's completed sales); `pos-topic` keys were random (now the transaction / shift id so a void cannot overtake its sale).
- Known, not fixed: POS tax ignores the discount (POS pricing rule); voids / offline sync after a shift is closed are not in the shift figures.
- Verified live in Docker (SYSTEM tenant): split cash 100 + card 100 sale with 20 discount and 20 tax → Dr 1000 100 + 100 / Cr 4000 180 / Cr 2200 20;
  a voided cash sale → entry Reversed + reversal entry; shift counted 195 vs expected 200 → Dr 5100 / Cr 1000 5; shift totals 200 / 1 sale.
- Tests: POS 10, ACC 73. No migration.

### 2026-10-24 — ROADMAP P1-1b: stock receipt / issue and COGS → GL (`feat/gl-stock-cogs`, unmerged)
- Warehouse's `StockMovementRecordedEvent` now carries `UnitCost` and a signed `ValueDelta` (receipt, ship COGS, POS sale / void,
  approved adjustment, stocktake variance). Accounting consumes it from RabbitMQ `warehouse_exchange` and posts: receipt Dr Inventory /
  Cr GRNI (2100), issue Dr COGS (5000) / Cr Inventory, gain / loss against the new `InventoryAdjustment` key; the bill now debits GRNI.
- Fixed on the way: PO receipts were booked at cost 0 (the PO event had no price) and a second partial receipt of the same product on
  the same PO was dropped as a "redelivery" (movement keyed on `PO-<id>`; now on the goods receipt). `PurchaseOrderReceivedDomainEvent`
  lines carry `UnitPrice`, `CurrencyCode`, `GoodsReceiptId`.
- No migration: the three new posting keys are added to a tenant's mapping on first use from the default accounts. Stock before
  the release is not back-posted (1200 starts below the Warehouse valuation until an opening entry is booked).
- Also fixed: receiving a product for the first time (`POST inventory/receive` for a new product) failed with "belongs to a different tenant" (Update() on a just-added row); this also blocked the PO-driven receipt, which needs an Inventory row per SKU.
- Tests: PO 9, WH 13, ACC 60. Verified live in Docker (SYSTEM tenant): manual receipt 10 x 40 -> Dr 1200 / Cr 2100 400; PO 20 x 50 received 12 then 8 -> both stocked at cost 50, entries 600 and 400; Sold 3 -> Dr 5000 / Cr 1200 150; Damaged 2 -> loss 100; Adjusted +4 -> gain 200; bill approval -> Dr 2100 / Cr 2000 1000. Not run live: ship-time COGS through a sales order, POS sale, redelivery (unit-tested only).

### 2026-10-23 — ROADMAP P1-1 (phase 1): automatic GL posting (`feat/gl-auto-posting`, merged as PR #15)
- Accounting never wrote journal entries by itself. Now `IJournalPostingService` posts invoice (Dr AR / Cr Revenue), payment
  (Dr Cash|Bank / Cr AR, applied to the invoice), cancelled order (invoice cancelled + entry reversed), bill approval (Dr Inventory /
  Cr AP) and bill payment, through a per-tenant **posting profile**. Idempotent per document (`JournalEntries.SourceKey` unique
  index); period by document date; default 14-account chart + profile created for a tenant with no accounts. Rules, keys, limits:
  `docs/business/Accounting/AutoPosting.md`.
- Found on the way: the seeded chart used 0-based `AccountType`/`NormalBalance`, the code and UI 1-based (P&L and trial balance misread
  the seed — fixed by migration Accounting 009); `JournalEntry` did not map status / date / reference (all entries stayed Draft);
  the Kafka consumer swallowed failures and auto-committed, the RabbitMQ one requeued for ever (both fixed); a redelivered
  `SalesOrderCreated` looped on the invoice number's unique index (fixed); the manual journal API accepted unknown accounts and
  one-sided lines (fixed); the `warehouse-topic` consumer never received anything (removed).
- Also found, not fixed here: `POST license/tenant/{id}/issue` returns 500 (`IssueLicenseRequestValidator` has async rules but is
  used by the synchronous ASP.NET validation); the ADMIN role created by tenant signup holds no accounting / sales / payment
  permissions (51 permissions only), so a new tenant cannot use those modules until roles are granted.
- Verified in Docker: new tenant → 14 accounts + 7 mappings + open month + `JE-2026-000001` on its first invoice; manual payment,
  bill approve / pay entries; sales order → invoice + entry over RabbitMQ; order cancel → invoice cancelled + reversal; payment
  capture → Dr 1000 / Cr 1100 and invoice paid; a duplicate `SalesOrderCreated` → one invoice; inactive mapped account and closed
  period → document saved, error logged, no entry; trial balance balanced, P&L revenue correct.
- Needs: Accounting migration `009`. Split off: P1-1b stock + COGS, P1-1c POS, P1-1d payroll, P1-1e refunds / returns.

### 2026-10-23 — ROADMAP P0-8: security hygiene (`fix/security-hygiene`, unmerged)
- NuGet advisory fixed (Pkcs/Xml 10.0.13 pinned in every Infrastructure csproj).
- Identity: resend supersedes the earlier 2FA challenge; 2FA enable/disable re-asks the current password (external-only
  accounts: token ≤ 5 min old); the code and reset / confirmation links are encrypted in `OutboxMessages` (`enc:v1:`) and
  overwritten with `[redacted]` after the publish; webhook secrets encrypted at rest. Details and key handling:
  SecurityReview "Phase 2 hardening". **Needs `SETTINGS_ENCRYPTION_KEY` (same value for Identity and Notification) and
  Identity migrations 027, 028.**
- Swagger / OpenAPI only in Development in 17 services (`SwaggerOnlyInDevelopmentTests`).
- Gateway: per-IP rate limit (30/min) on sign-in, token and chat endpoints.
- Found on the way: Identity recorded the first nginx (not the client) as the caller's IP behind the two nginx hops, so the
  per-IP limits and the failed-login window were shared by all users. `ForwardedHeaders:TrustedHops` fixes it
  (compose: 2; **set it for the real ingress chain on Kubernetes**).
- Verified in Docker: password re-ask on 2FA enable/disable, old code rejected after a resend, delivered outbox rows
  redacted while Notification still got the real code, webhook secret stored as `enc:v1:` and kept on a secret-less
  update, Identity Swagger 404, 429 from the 31st request even with rotating `X-Forwarded-For`.
- Split off as ROADMAP P0-10: per-service SQL logins + `Encrypt=True`, Notification webhook secrets, SMS number verification.

### 2026-10-23 — ROADMAP P0-7: Identity external login and 2FA OTP (`fix/identity-external-login`, unmerged)
- External login: `GetByExternalProviderAsync` returned an exception instead of null, so no new Google/Microsoft/
  Facebook/Twitter user could ever be provisioned; `User.Create` rejected the empty password hash; the stored link had
  no TenantId (the next login would not find it). Now a new Customer is provisioned (random unusable password, provider
  name, email confirmed only if the provider verified it); a *verified* email of an existing tenant user links to it
  (decision 2026-10-23), an unverified one is refused; Facebook and Microsoft report `EmailVerified = false`
  (Microsoft's `preferred_username` is a mutable UPN, "nOAuth"); `ExternalLoginRepository` implemented.
- 2FA: `TwoFactorChallenge` keeps the masked destination, the event carries the real recipient; SMS without a phone
  number fails with `TwoFactorNoDestination`.
- **Identity events never reached Notification**: `KafkaMessageBus` serialized the (already JSON) Outbox content again,
  so consumers got a JSON string. Fixed; Notification `012` moves the default templates stranded on the empty tenant to
  SYSTEM ("Template 'TWO_FACTOR_CODE' not found"). Run migration 012.
- Verified in Docker: email 2FA login -> stored `ad***@…`, event and Notification record use the real address
  (status Failed because no SMTP server is configured locally). Real Google/Microsoft tokens were not available, so the
  external-login paths are covered by unit tests (AuthService + InMemory repository), not by a live call.
- Still open (P0-8): invalidate the previous 2FA challenge on resend, keep the OTP out of `OutboxMessages`;
  new users created by external login get no email-confirmation message.

### 2026-10-23 — ROADMAP P0-9: SQL schema aligned with the EF models (`fix/schema-alignment`, unmerged)
Full write-up: `docs/architecture/schema-drift-2026-10-23.md` (oracle, clean-room recipe, every fix).
- `LiveSchemaDriftTests` (opt-in `ERP_DRIFT_SQL`) now compares names, nullability blockers, column types, computed
  columns. Zero drift on the host databases and on a database built from `src/Database` alone (pass 1: 0 SQL errors).
- New migrations (run them on every database): Identity `026`, Accounting `008`, CRM `006`, HRM `006` (+ `003` reordered),
  MasterData `008`, Notification `010`/`011`, Payment `007`, POS `007`/`008`, Product `018`, PromotionService `004`/`005`
  (seed moved to `002a`), Purchasing `010`/`011`, Reporting `007` (ETL seeds skip until the source DBs exist), Sales `012`,
  Shipping `006`/`007`, Warehouse `008`. `AllServices/004_*.ps1` and Identity `015_test_wrapped.sql` removed.
  On a NEW database run `db-migrate` twice (Reporting ETL backfill).
- Code: all `Value<Guid>` / `as JArray` event reads use `EventJson` (14 consumers; guard tests); EF mappings for
  GoodsReceipt, Shipment, ShipmentItem, Settlement, SalesQuotationItems; SaleOrder: `/internal/jwks.json` open,
  `OrderNumberGenerator`, tracked `GetByIdAsync`.
- Verified in Docker: POS sale/void ↔ stock, order → approve → payment → reservation → shipment (real address/lines),
  PO → bill. Tests: Shared 92, Identity 211, Gateway 10, PO 8, ACC 2, SO 2, SHP 2, WH 8, POS 3, Architecture 52/53 —
  the one failure is a local git-ignored `deployment/k8s/secrets.yaml` with an obsolete `jwt-signing-key`.

### 2026-10-23 — ROADMAP P0-1: first Docker run (`fix/docker-first-run`, unmerged)
Full write-up: `docs/architecture/schema-drift-2026-10-23.md`. PR #10–#12 were already merged into `master`
(§1 below is stale on that point). Ran the whole stack against the SQL Server installed on the host
(`docker-compose.localsql.yml`, `SQL_HOST` in `.env`; the container SQL stays the default).
- **Verified:** build of all images; Identity generates the RS256 signing/encryption certificates; token header
  `RS256` with the `kid` listed in JWKS; `/internal/...` → 401 with no token and with a user token; logout → 401;
  `logs:view` → tenant admin 403, platform admin 200; PO two-level approval → one Accounting bill with the PO total.
- **Bugs found and fixed:** token tables without `UpdatedAt`/`IsDeleted` (register / tenant-signup 500);
  `Bills.SupplierName` / `Invoices.CustomerName` NOT NULL; POS `TransactionItems.TenantId`; five legacy Product
  tables; `JObject.Value<Guid?>` throwing on GUID strings (Accounting consumer fixed, 40 calls left);
  `run-migrations.sh` hiding SQL errors; CRLF shell scripts crash-looping admin/storefront; WSL 4 GB hanging Docker.
- POS sale/void ↔ stock and storefront order → shipment were verified afterwards under P0-9 (next entry).
- New: migrations Identity `025`, Accounting `007`, POS `006`, Product `016`/`017`; `EventJson`;
  `LiveSchemaDriftTests` (opt-in, `ERP_DRIFT_SQL`).
- Deployment: run the new migrations; the host databases were backed up (`*_pre-p0-1_20261023.bak` in the
  instance's default backup folder) before the migrations ran.

### 2026-10-22 — ROADMAP P0-2…P0-6 (`fix/roadmap-p0`)
Owner decisions: bill on final PO approval; POS sale never refused for stock (may go negative +
alert); logs only for platform admins. Event changes: `docs/architecture/event-catalog.md`
"Flow fixes 2026-10-22".
- **P0-3** Purchasing supplier list / get / deactivate handlers were stubs → implemented.
- **P0-4** Accounting books the supplier bill from `PurchaseOrderApprovedDomainEvent` (now carries
  `TotalAmount`/`CurrencyCode`), once per PO; PO lines frozen after Draft. Was: bill from
  PurchaseOrderCreated → always 0.
- **P0-5** Saga `CreateShipmentCommand` carries recipient, address and lines; Shipping maps City →
  district, State → province and drops commands without an address. Limitation: orders have no
  ward/phone yet (ROADMAP P2-2).
- **P0-6** POS `TransactionCreatedEvent` carries lines, new `TransactionVoidedEvent` (completed sales
  only); Warehouse `PosTransactionConsumer` (`pos-topic`, group `warehouse-service-pos-consumer`)
  takes stock out / puts it back, idempotent (`POS-{tx}` / `POS-VOID-{tx}`). No terminal → bin
  mapping: only the product's total stock moves (no location stock / lot depletion for POS yet).
- **P0-2** Root cause of the broken admin build: `.gitignore` `[Ll]ogs/` kept `pages/logs` and
  `api/logs` out of git (now exempted). Logs page committed; `logs:view` is a platform permission
  (logs have no tenant) — `PermissionScopes`, migration Identity `024`, `[AuthorizePermission]`.
- New unit test projects: `tests/unit/ERP.{PO,ACC,SO,SHP,WH,POS}.UnitTests` (not in the root .sln).

### 2026-10-21 — Service-to-service tokens; no shared token key (`feature/service-tokens`)
Owner request: public keys between backend and frontend, a private key per API service for calls
between services. Details and runbook: `docs/business/Identity/SigningKeys.md`.
- **Service-to-service** (`ERP.Shared.Authorization.ServiceAuth`): a caller signs a 60 s RS256
  token with its OWN key (`ServiceAuth:SigningKeys` — Key Vault in k8s, `Ephemeral` in compose)
  and publishes `/internal/jwks.json`; receivers trust only `ServiceAuth:TrustedCallers`, each
  caller's key validating only its own tokens. Endpoints: `internal/...` +
  `[AuthorizeInternalService]` — SaleOrder → Warehouse availability / Promotion validate-coupon,
  Gateway & ChatBot → Identity `tenants/resolve` (the old anonymous `api/v1/tenants/resolve` is
  gone; Warehouse's public availability stays for the ChatBot tool). The token carries the tenant,
  which fixed the stock pre-check (404 since phase 2).
- **`Jwt:Key` removed everywhere** (phase 4 "release 2" done in the same branch — release 1 had
  only run locally): RS256 only; OpenIddict encryption uses an RSA-OAEP certificate
  (`EncryptionKeys`); `JwtKeyGuard` deleted; compose, k8s, `.env`, appsettings, export templates
  cleaned; `NoSharedTokenKeyTests` keeps it out.
- Certificate sources moved to `ERP.Shared.Security.Certificates` (Identity + services).
- Fixed on the way: SaleOrder had no Warehouse/Promotion base URLs in compose (called localhost →
  every order refused); the Gateway now serves its JWKS without a tenant lookup (would loop).

### 2026-10-20 — Service authorization phase 4, release 1: RS256 / JWKS (PR #9)
Only IdentityService can sign access tokens now: RS256 with a certificate from Azure Key Vault
(production) or a PFX on a persistent volume (local/Compose) — `SigningKeys:Source`; the
X509Store dev certificate (new key on every container re-creation) is gone. Every service and the
Gateway validate through `ERP.Shared.Authorization.ErpJwtBearerOptions` with the public keys from
Identity's `/.well-known/jwks.json` (`Jwt:Authority`), only `RS256` (+ `HS256` while `Jwt:Key` is
still configured), exact issuer/audience (the Gateway skipped both before). Outside Identity only
login tokens (issuer `ERP.Identity`) are accepted — OpenIddict `urn:` tokens stay Identity-only.
Identity `Jwt:AccessTokenSigning` (`Hmac`/`Rsa`) makes a rolling k8s cutover 401-free. Also fixed:
LoggingService had no Jwt settings in docker-compose (would not start outside Development).
Runbook (cutover, rotation, emergency): `docs/business/Identity/SigningKeys.md`.

### 2026-10-20 — Service authorization phase 3: token revocation (PR #8)
Logout, account lock/deactivation, password change/reset, role assign/remove, role permission
edits, tenant deactivation and license changes now take effect in EVERY service at once (was:
only in IdentityService; elsewhere the old JWT worked for up to 60 min). Shared
`ERP.Shared.Authorization.TokenRevocation` (Redis, prefix `erp:authz:`): revoked `jti` + "user /
tenant changed at" markers; `AddErpJwtAuthentication` rejects a revoked jti or a token whose `iat`
is older than the marker (5 s local cache, fail-open when Redis is down); Gateway checks too;
hubs re-check on every call (`RevocationHubFilter`). Identity: tokens carry `iat`;
`IUnitOfWork.Revocations` publishes after commit; admin lock now ends sessions and blocks refresh
(failed-login lockout does not). Clients get new permissions via their normal refresh-on-401.

### 2026-10-20 — Tenant isolation, phase 2 (PR #4–#6)
Every service DbContext is tenant-scoped (CLAUDE.md "Multi-Tenant Isolation" — read it before
touching any DbContext, consumer or background job). Shared: `ITenantOwned`,
`IPlatformWideEntity`, `ISharedReferenceData`, `AddRequestTenantProvider` (JWT → Gateway host
tenant for anonymous calls), `ITenantEvent` + `EventTenant`, `TenantHostResolver` (moved from
the Gateway). Every cross-service event carries `TenantId`; consumers skip events without one.
Cross-tenant jobs run per tenant. Business codes are unique per tenant. Product search filters
Elasticsearch by tenant; ChatBot guests get the storefront host's tenant (`HostTenantHubFilter`);
MasterData reference data and Notification templates are shared SYSTEM defaults + tenant rows.
Defects found on the way (hard-coded SYSTEM tenant in stock movements and shipping carrier,
globally unique codes, Reporting summing all tenants, …): SecurityReview "Tenant isolation
phase 2". Tests: `tests/unit/ERP.Architecture.Tests` (architecture, EF filter coverage,
two-tenant behaviour per service).

### 2026-10-20 — Service authorization, phase 1 (`feature/service-authz-phase1`)
Review of all 16 services: `docs/business/Identity/SecurityReview-2026-10.md` ("Service
authorization review"). Done: ProductService permissions (`products:*`, migration
`Identity/023`); `AddErpAuthorization()` FallbackPolicy in every service; explicit
`[AuthorizeAnyAuthenticatedUser]`/`[AllowAnonymous]`; `tests/unit/ERP.Architecture.Tests`
(fails on any action without explicit authorization; 57 legacy in-action-check controllers
allowlisted); coupon/review take the user from the token; templates need a permission; guest
chat history needs its session id; license enforcement in Accounting + SaleOrder; nginx /
ingress host conventions (`<tenant>.erp.local` admin, `<tenant>.shop.erp.local` storefront,
custom domains → storefront) with `/api` and `/hubs/*` same-origin; no published service
ports (`docker-compose.debug.yml`), infra on 127.0.0.1; broker UIs no longer public;
`Tenancy:StorefrontBaseDomain`.

### 2026-10-20 — Identity authorization & tenant-from-host (`feature/identity-authorization-tenant-host`)
Full write-up: `docs/business/Identity/SecurityReview-2026-10.md` ("Follow-up review").
- **Platform vs tenant permissions**: `src/Shared/Authorization/PermissionScopes.cs`
  (`PermissionScopes`, `PlatformTenant`, `PlatformRoles`). Platform permissions only effective
  in the SYSTEM tenant; stripped from every other token.
- **Token permissions** computed only by `Identity.Infrastructure/Security/EffectivePermissions.cs`
  (also `WhyNotGrantableAsync` = anti-escalation for role grants + API key scopes).
- `AuthorizePermissionAttribute`: **role-name "SuperAdmin" bypass removed** (all services).
- Cross-tenant endpoints: `[TenantRouteAccess(...)]` (`Identity.Api/Authorization`) backed by
  `ITenantAccessPolicy` — replaced 13 copy-pasted `CallerCanAccessTenantAsync`.
- **Tenant = host**: `TenantDomain` entity + `ITenantDomainService` (resolve endpoint
  `GET /api/v1/tenants/resolve?host=`, domain CRUD for platform admins); Gateway
  `GatewayAuthorizationMiddleware` + `TenantHostResolver` (404 unknown host, strips client
  `X-Tenant-*`/`X-User-*`, 401 if JWT tenant ≠ host tenant). Auth endpoints read the tenant
  only from the Gateway header; refresh bound to host tenant; `tenant-signup` only on the
  platform host; OIDC tenant = host. Storefront no longer sends a tenant (`VITE_TENANT_CODE` gone).
- Migrations: `src/Database/Identity/021_EnforcePlatformPermissionScope.sql`, `022_AddTenantDomains.sql`.
- Tests: `tests/unit/ERP.Identity.UnitTests/Security/AuthorizationScopeTests.cs`,
  new project `tests/unit/ERP.Gateway.UnitTests`.

### 2026-10-20 — Multi-tenant isolation (PR #1, merged)
- `src/Shared/Infrastructure/Data/{TenantScope.cs,TenantFilterConfiguration.cs}`: per-request
  query filter + `EnforceTenantOnSave`; old constant filter throws.
- CRMService isolated (`Database/CRM/005_BackfillTenantId.sql` → empty TenantId rows to SYSTEM);
  PurchaseOrderService query filter fixed. Pattern documented in CLAUDE.md "Multi-Tenant Isolation".

### 2026-10-19 — IdentityService security review + Outbox (commit `73970b5`)
- Register/external-login always Customer, forgot-password never returns the token (email via
  `PasswordResetRequestedDomainEvent`), token-based email confirmation, IsActive checks,
  trusted-proxy client IP, webhook SSRF policy, `JwtKeyGuard`, secrets removed from appsettings.
- Outbox: `OutboxEventDeserializer`, stable `EventId`, batching, dead-lettering (CLAUDE.md "Outbox rules").

## 3. Deployment prerequisites (don't skip — behaviour depends on them)
1. Run migrations in order: CRM `005`, Identity `021`, `022`, `023`, Notification `008`
   (`deployment/docker/run-migrations.sh` runs every `*.sql` in filename order).
2. `.env`: fresh `JWT_SIGNING_KEY` (`openssl rand -base64 48`) — containers run as Production and
   refuse placeholder/leaked keys (`JwtKeyGuard`). **Rotate** the SA password and JWT key that
   leaked in git history (see SecurityReview §"Required operator action") — not yet confirmed done.
3. Tenancy config: IdentityService `Tenancy:BaseDomain`, `Tenancy:PlatformHost`; Gateway
   `Tenancy:IdentityUrl`. `Tenancy:FallbackTenantCode` is `SYSTEM` only in Development /
   docker-compose (so `http://localhost` works) — **must be empty in real multi-tenant prod**.
4. Only nginx may be publicly reachable (services trust Gateway-set `X-Tenant-*`); docker-compose
   no longer publishes service ports. Hosts: `<tenant>.erp.local` (admin, platform =
   `admin.erp.local`), `<tenant>.shop.erp.local` (storefront), custom domains (storefront);
   locally add them to /etc/hosts or use `localhost` (falls back to SYSTEM).
   Tenants on plans without ACC/SO modules now get 403 from Accounting/SaleOrder.
5. Review the API keys listed by migration 021 and revoke them.
6. **Tenant isolation migrations (phase 2)** — run BEFORE deploying the new services:
   Sales `011`, Payment `006`, Accounting `006`, Product `015`, Warehouse `007`, Purchasing
   `009`, Shipping `005`, PromotionService `003`, POS `005`, HRM `005`, MasterData `007`,
   Notification `009`, ChatBot `002`, Reporting `006` (all `*_TenantIsolation*.sql`). They add
   missing TenantId columns, make codes unique per tenant and assign empty-tenant rows to SYSTEM.
7. **Deploy publishers and consumers together.** Consumers now skip events without `TenantId`;
   events already sitting in an Outbox from before the release have none — let Outboxes drain
   before upgrading, or accept that those events are skipped (logged as warnings).
8. **Elasticsearch re-index:** Product `015` queues every product; product search returns
   nothing for a tenant until `SearchIndexProcessor` has re-indexed its products (documents now
   carry `TenantId`; the filter uses `tenantId.keyword` from dynamic mapping).
9. ChatBot needs `Tenancy:IdentityUrl` (default `http://identityservice:5001`, same as the
   Gateway) to resolve guest/hub tenants from the host. The storefront no longer sends
   `VITE_TENANT_ID`.
10. Notification templates in the SYSTEM tenant are shared defaults: tenants can't edit them,
    they create their own template with the same code to override. ReportingDB: direct BI-tool
    queries must filter on `TenantId`.
11. **Redis is now part of authorization**: every service, the Gateway and Identity need
    `ConnectionStrings:Redis` (shared store `erp:authz:*`). Without it revocation only works inside
    one process (warning at startup); if Redis is down tokens are accepted (fail-open, logged).
    Tokens issued before this release have no `iat`: until they expire (≤ 60 min) only their `jti`
    is checked.
12. **Token keys (phase 4 + 2026-10-21)** — follow `docs/business/Identity/SigningKeys.md`:
    - every service + Gateway: `Jwt:Authority` (internal Identity URL) and, for in-cluster http,
      `Jwt:RequireHttpsMetadata=false` (configmap / compose) — otherwise no JWKS, every call 401;
    - Identity: `SigningKeys:Source` **and** `EncryptionKeys:Source` are required (start-up fails
      without them). k8s: Key Vault certificates `identity-token-signing` and
      `identity-token-encryption`; compose: PFX files auto-created on the `identity_signing` volume;
    - service-to-service: callers (SaleOrder, Gateway, ChatBot) need `ServiceAuth:Name` +
      `ServiceAuth:SigningKeys` (k8s: Key Vault `svc-<name>-signing` through their own
      ServiceAccount — several replicas must share the key, never `Ephemeral`); receivers
      (Warehouse, Promotion, Identity) need `ServiceAuth:Name` + `ServiceAuth:TrustedCallers`.
      Replace the `<your-key-vault>` / `<managed-identity-client-id>` placeholders in
      `deployment/k8s/deployments/*.yaml`;
    - delete `JWT_SIGNING_KEY` / the `jwt-signing-key` secret from every environment — nothing reads
      it any more. OpenIddict refresh tokens issued before this release can't be decrypted
      (dev-seeded OIDC clients must re-authorize).

13. **2026-10-22 (`fix/roadmap-p0`)**: run Identity `024_LogsViewPlatformOnly.sql` (tenant admins
    lose `logs:view`; tokens already issued keep it until they expire ≤ 60 min; revoke the API keys
    it lists). Deploy POSService and WarehouseService together (POS events published before have no
    lines → Warehouse logs and skips them); deploy SaleOrderService and ShippingService together
    (Shipping drops saga commands without an address); deploy PurchaseOrder and Accounting together
    (Accounting no longer books bills from PurchaseOrderCreated). Bills already created with amount 0
    are not corrected automatically.

## 4. Backlog (prioritised — from the 2026-10-20 system inventory)

> **2026-10-22:** the full, re-prioritised backlog (with IDs, evidence and the new ERP modules) is
> `docs/ROADMAP.md` — it supersedes the list below where they differ. New P0 items found then:
> admin build broken (`LogsPage` missing), Purchasing supplier handlers are stubs, bills from POs
> have amount 0, saga-created shipments have a placeholder address, POS sales don't reduce stock.

**P0 — security / data integrity**
- Accept OpenIddict (`urn:`) tokens in services, after client_credentials permissions go through
  `EffectivePermissions`.
- Service authorization phase 5 (plan in SecurityReview "Service authorization review"):
  migrate the 57 allowlisted controllers to attributes, hub ticket-only (also closes guest chat
  within one tenant: hub methods don't check the guest session), `ValidAlgorithms`.
- (P0-8 closed 2026-10-23: NuGet advisory, Identity phase 2, Swagger, Gateway rate limiting.) Still open: per-service SQL logins +
  `Encrypt=True` (ROADMAP P0-10).

**P1 — broken / unfinished**
- External-login auto-provisioning always fails (`User.Create` rejects empty PasswordHash).
- 2FA email OTP is sent to the *masked* address.
- `ExternalLoginRepository`: 4 methods throw `NotImplementedException`.
- Event flows not wired: SaleOrder ← UserCreated/PromotionCreated; Warehouse ← PurchaseOrderApproved;
  ReceiptScheduled, QuantityRejected, SalesOrderReturned have no consumers; `crm-topic`,
  `notification-topic` unsubscribed; Accounting auto-posting needs COA mapping; Reporting has
  only 5 facts.
- ~25 Settings categories are storage+CRUD only (not applied at runtime) — see
  `docs/business/Settings/SettingsOverview.md`.
- Shipping has two fee mechanisms; Sales credit-limit check before approve; Redis `RemoveByPattern`.

**P2 — missing UI / modules**
- Admin SPA: Promotion, Shipping, POS, Notification templates pages (APIs exist); ~169 hardcoded
  permission strings.
- Storefront: reset-password / confirm-email pages (email links point to them), coupons,
  shipment tracking, account security.
- HRM: 7 of 11 BA sub-modules not built; Finance: closing/aging/COA mapping.

**P3 — quality**
- Tests exist only for Identity, Shared, Gateway, Architecture (incl. two-tenant tests for every
  service DbContext) and Product (integration, no .csproj) PurchaseOrder
  test folders have **no .csproj** (never compiled).

## 5. Working environment notes
- Verify a change: build each touched `src/services/<Svc>/*.sln` (+ `src/Gateway/Ocelot.API`,
  `src/Shared/ERP.Shared.csproj` for shared changes), then
  `dotnet test tests/unit/ERP.Identity.UnitTests tests/unit/ERP.Shared.UnitTests tests/unit/ERP.Gateway.UnitTests tests/unit/ERP.Architecture.Tests`.
  Last green run (2026-10-22, `fix/roadmap-p0`): touched APIs build with 0 errors, admin
  `npm run build` OK; Architecture 50, Shared 86, Identity 211, Gateway 10, PO 8, ACC 2, SO 2,
  SHP 2, WH 8, POS 3 passed (Docker not running — `/verify` not possible). Previous: 2026-10-21,
  service tokens + `Jwt:Key` removal: all APIs + Gateway build with
  0 errors; Shared 86, Identity 210, Gateway 10, Architecture 50 passed. Docker runtime not
  verified yet — first run must check: Identity logs "Generated a new self-signed Signing / Encryption
  certificate"; a login token header is `RS256` with a `kid` listed at `/.well-known/jwks.json`;
  storefront order creation succeeds (SaleOrder → `/internal` Warehouse + Promotion answer 200 in
  the right tenant); Gateway host resolution works (Identity fetches `http://gateway:5000/internal/jwks.json`);
  `/internal/...` without a token or with a user token answers 401; after logout 401. Key Vault
  sources and the PowerShell export scripts are untested (no Azure / pwsh here).
- Repo rule: run the `/verify` skill (Docker-based) before committing code; if Docker isn't
  running, say so and fall back to the build/test commands above.
- `dotnet build ERP.Identity.sln` can take >5 min on a cold machine — run it in the background.
- Shell is zsh on macOS: use `sed -i ''`; avoid `echo ====` (zsh `=` expansion).
- `dotnet test tests/unit/ERP.Architecture.Tests` builds every API project — 3–10 min; run it in
  the background. It is the safety net for authorization and tenant isolation.
- Docker was not running on the development machine during phases 1–2: migrations, consumers,
  nginx host routing and the Elasticsearch re-index were verified by build + unit/architecture
  tests only. The first Docker run should follow §3 and check service logs for
  `without TenantId ignored` warnings and `TenantIsolationViolationException`.
- New tenant-owned entity / service checklist = CLAUDE.md "Multi-Tenant Isolation" + add a
  two-tenant case to `TenantIsolationBehaviorTests`; new migration = next number in
  `src/Database/<Svc>/`, idempotent (`IF NOT EXISTS` / `COL_LENGTH`).

## 6. Conventions agreed with the project owner
- Communicate in **Vietnamese**; code, comments, commit messages and docs in English.
- One branch per plan; split commits by plan step; don't push or merge without being asked.
- Non-trivial work: plan first (findings + approach), get approval, then implement + tests + docs.
- Data-ownership decisions (e.g. which tenant orphaned rows belong to) are asked, not assumed.

## 7. Decision log (do not re-litigate without the owner)

| Date | Decision | Where it shows up |
|---|---|---|
| 2026-10-20 | Rows with an empty TenantId belong to the **SYSTEM** tenant (`…0001`) | Every `*_TenantIsolation*.sql` / CRM `005` |
| 2026-10-20 | **Tenant = request host**: subdomain `<code>.erp.local` / `<code>.shop.erp.local` **and** custom domains (`TenantDomains`); platform admin = SYSTEM tenant's own domain (`admin.erp.local`) | Identity `ITenantDomainService`, Gateway, nginx/ingress |
| 2026-10-20 | Platform permissions only in the SYSTEM tenant; no authority from role names | `PermissionScopes`, `EffectivePermissions` |
| 2026-10-20 | Two host families (admin vs storefront); API and hubs same-origin on the tenant host; no service ports published | nginx, ingress, docker-compose |
| 2026-10-20 | Service authz in phases; phase 1 = explicit authorization everywhere + infra; phases 2–5 one at a time | SecurityReview "Service authorization review" |
| 2026-10-20 | **Tenant travels in the event payload** (`ITenantEvent`), not in message headers | event-catalog "Tenant in every event" |
| 2026-10-20 | MasterData reference data = **shared SYSTEM rows + tenant additions** (only platform edits shared rows); same model reused for Notification templates | `ISharedReferenceData` |
| 2026-10-20 | Consumers **skip** events without a tenant (fail closed), never guess | all consumers |
| 2026-10-20 | Guest (anonymous) tenant = host's tenant; client-sent tenant ids are only cross-checked | ChatBot, Product catalog, POS customer display |
| 2026-10-20 | Business codes unique per tenant | migrations phase 2 |
| 2026-10-20 | Token revocation: Redis down → **fail-open** (accept + warn); role permission edit → only the role's holders refresh; **admin lock ends sessions** (failed-login lockout does not); hubs re-check on **every call** | `ERP.Shared.Authorization.TokenRevocation`, Identity `IUnitOfWork.Revocations` |
| 2026-10-20 | Token signing: certificate in **Azure Key Vault** with rotation (newest signs, previous versions stay in the JWKS); HS256 → RS256 in **two releases**; the **Gateway keeps validating** (same code as services); services accept **only login tokens** (issuer `ERP.Identity`), OpenIddict `urn:` tokens stay Identity-only for now | `docs/business/Identity/SigningKeys.md`, `ErpJwtBearerOptions` |
| 2026-10-21 | Service-to-service: **each service its own key pair** (short-lived RS256 tokens, tenant + user carried); **separate `internal/` endpoints** for service-only calls (never anonymous, never routed by the Gateway); release 2 folded in (release 1 ran only locally) and **`Jwt:Key` removed entirely**, OpenIddict encryption via its own certificate | `ServiceAuth`, `SigningKeys.md`, `NoSharedTokenKeyTests` |

| 2026-10-22 | Execution order: **P0 → P1 business flows → authorization phase 5 → CI/CD, tests, operations → missing UI → new modules** | `docs/ROADMAP.md` Part B |
| 2026-10-22 | In scope as new ERP modules: **fixed assets, shared workflow/approval engine, HRM extension (recruitment/training/performance), manufacturing (BOM/MRP)** — each needs an ADR first | `docs/ROADMAP.md` P2-5…P2-8 |

## 8. Next step: ROADMAP P0-9 (schema alignment), rest of P0, then P1 (needs a plan + approval)

Update 2026-10-23: P0-1, P0-9, P0-7, P0-8 and P1-1 phase 1 are closed, P1-1b (stock + COGS) is implemented on `feat/gl-stock-cogs`. Next: P1-1c POS, or P1-2 (VAS chart) — P0-10 (least-privilege SQL access) can be scheduled any time. The text below is from 2026-10-22.

Working checklist: `task.md` at the repo root (same IDs as `docs/ROADMAP.md`).

P0-2…P0-6 are done on `fix/roadmap-p0` (unmerged). Remaining P0: P0-1 (Docker run + merge the
branches in order), P0-7 (Identity external login / OTP), P0-8 (NuGet advisory, Identity phase 2).
Then P1 beginning with P1-1 automatic GL posting. Service authorization phase 5 (below) moved after P1 (owner decision
2026-10-22).

### Later: service authorization phase 5

- Migrate the allowlisted controllers (56 left) (`EndpointAuthorizationTests.InActionCheckAllowlist`) to
  `[AuthorizePermission]` attributes.
- Hubs: ticket-only (no raw JWT in the query string, SecurityReview S11); guest chat sessions
  checked within one tenant (hub methods don't verify the guest session today).
- Open items: OpenIddict `urn:` tokens in services (after client_credentials permissions go
  through `EffectivePermissions`); other synchronous service-to-service calls added later must use
  `/internal` + `AddServiceToken` (CLAUDE.md "API Authorization").
Plan + owner approval first, as usual.
