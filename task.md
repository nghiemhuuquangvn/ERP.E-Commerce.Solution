# Tasks — next work

> Working checklist derived from `docs/ROADMAP.md` (IDs match). Order agreed with the owner
> (2026-10-22): **P0 → P1 business flows → authorization phase 5 → P3 CI/CD, tests, operations →
> P2 missing UI → P2 new modules**. Tick a box when its commit is on a branch; move finished
> sections to `docs/PROJECT-STATUS.md` §2.
>
> Every task: plan → owner approval → implement + tests + docs, one branch per plan, one commit per
> step, never push/merge without being asked. Re-read CLAUDE.md "Multi-Tenant Isolation" before
> touching a DbContext, consumer, job or event.

## How to run things (VS Code, integrated terminal at the repo root)

```bash
# build one service (each service has its own .sln)
dotnet build src/services/<Service>/<Service>.sln

# unit tests (fast)
dotnet test tests/unit/ERP.Shared.UnitTests
dotnet test tests/unit/ERP.Identity.UnitTests
dotnet test tests/unit/ERP.Gateway.UnitTests
dotnet test tests/unit/ERP.PO.UnitTests   # also ACC, SO, SHP, WH, POS

# architecture tests (authorization + tenant isolation; builds every API, 3–10 min)
dotnet test tests/unit/ERP.Architecture.Tests

# admin SPA
cd src/frontend/admin && npm ci && npm run build

# whole platform (Docker Desktop running) — see deployment/docker/README.md
docker compose up -d --build
```

Note: `.vscode/tasks.json` / `launch.json` only cover IdentityService today.

---

## P0 — finish first

- [x] **P0-1 First Docker run + merge the open branches** — closed 2026-10-23 (`fix/docker-first-run`)
  - Branches: PR #10–#12 were already merged (`master` contains them).
  - Verified in Docker: RS256 token + JWKS `kid`, `/internal` 401, logout → 401, tenant admin 403 / platform
    admin 200 on Logs, PO two-level approval → one Accounting bill with the PO total.
  - POS sale/void ↔ inventory and storefront order → shipment were verified under P0-9.
  - Fixed on the way: see `docs/architecture/schema-drift-2026-10-23.md` ("Fixed").
  - How to run against the SQL Server installed on the host: `docker compose -f docker-compose.yml -f
    docker-compose.localsql.yml up -d --build` with `SQL_HOST=host.docker.internal` in `.env`; Docker Desktop
    needs ≥ 8 GB for WSL (`~/.wslconfig`), 4 GB hangs the engine.

- [x] **P0-9 Align the SQL schema with the EF models** — closed 2026-10-23 (`fix/schema-alignment`)
  - `LiveSchemaDriftTests` (now also types, nullability, computed columns) reports no drift on the host databases
    and on a database built from `src/Database` alone; the chain runs with 0 SQL errors on a new database.
  - All `Value<Guid>` / `as JArray` payload reads replaced (`EventJson`), guarded by `NoValueGuidConversionTests`.
  - Verified in Docker: POS sale 103→101, void →103; order → approve → payment captured → reservation → shipment
    with the real recipient/address/lines; PO → bill. Details: `docs/architecture/schema-drift-2026-10-23.md`.
  - Follow-ups: wire the drift test into CI (P3-1); run db-migrate twice on a new database (Reporting ETL seeds);
    a local, git-ignored `deployment/k8s/secrets.yaml` with an obsolete `jwt-signing-key` makes
    `NoSharedTokenKeyTests.NoDeploymentConfigurationSetsASharedTokenKey` fail on this machine — delete that line.

- [x] **P0-7 Identity external login & OTP** — closed 2026-10-23 (`fix/identity-external-login`)
  - First external login now provisions a Customer (random unusable password, provider name, confirmed email when the
    provider verified it) and the second login finds it (the link stores its TenantId). A *verified* email that already
    belongs to a user of the tenant links to that user; an unverified one is refused. Facebook and Microsoft report
    `EmailVerified = false` (no proof of ownership). `ExternalLoginRepository` fully implemented.
  - 2FA: the challenge keeps the masked address, the `TwoFactorCodeGenerated` event carries the real one; SMS without a
    phone fails with a business rule.
  - Found on the way: Identity's `KafkaMessageBus` double-encoded the Outbox JSON so NO Identity event reached
    NotificationService (2FA codes, password reset, email confirmation, UserCreated); default templates stranded on the
    empty tenant (Notification `012`).
  - Verified in Docker up to the notification record addressed to the real email (status Failed: no SMTP server is
    configured in the local stack). Tests: Identity 231.

- [x] **P0-8 Security hygiene** — closed 2026-10-23 (`fix/security-hygiene`)
  - NuGet Pkcs / Xml pinned to 10.0.13 in every Infrastructure project.
  - Identity: a 2FA resend ends the earlier challenge; enabling/disabling 2FA needs the current password; the 2FA
    code and reset / confirmation links are encrypted in the outbox and overwritten after delivery; webhook secrets
    encrypted at rest; Swagger only in Development (guard test); Gateway rate limiting; client IP via
    `ForwardedHeaders:TrustedHops` (Identity recorded the first nginx as every user's address).
  - Verified in Docker (see PROJECT-STATUS). Tests: Identity 255, Gateway 44, Shared 97.
- [ ] **P0-10 Least-privilege database access** (split off P0-8)
  - Per-service SQL logins instead of `sa` + `Encrypt=True` with a trusted certificate (compose, k8s secrets,
    appsettings; the migration runner stays privileged).
  - Notification's own webhook secrets (plaintext), phone-number verification before an SMS 2FA number is accepted.

## P1 — cross-service business flows

- [x] **P1-1 Automatic GL posting — phase 1** closed 2026-10-23 (`feat/gl-auto-posting`)
  - Posting engine + profile (`PostingAccountMappings`), default chart for new tenants, idempotent per source (`SourceKey`),
    period by date, reversal, status / date / source on journal entries; seeded account enums fixed (migration Accounting 009).
  - Flows: invoice, payment, order cancellation, bill approval / payment. Consumers retry instead of losing or looping; the dead
    `warehouse-topic` consumer is removed. Docs: `docs/business/Accounting/AutoPosting.md`.
- [x] **P1-1b Stock + COGS → GL** closed 2026-10-24 (`feat/gl-stock-cogs`): cost on Warehouse events, PO price to Warehouse, GRNI, COGS, adjustments
- [x] **P1-1c POS → GL** closed 2026-10-24 (`feat/gl-pos`): amounts on POS events, sale / void / shift variance posting
- [ ] **P1-1d Payroll → GL** (with P1-7)
- [ ] **P1-1e Refunds and returns → GL** (confirmed-refund event, return lines)
- [ ] **P1-2 VAS chart of accounts**: hierarchy (Circular 200/133 — confirm the current one),
  reversing/voiding entries, period-end closing entries (911 → 421), cash-flow statement.
- [ ] **P1-3 Tax & e-invoice**: VAT fields on invoice/bill, VAT listing/return, e-invoice provider
  adapter (VNPT / Viettel / MISA / BKAV — owner picks the provider).
- [ ] **P1-4 Bank reconciliation per line + multi-currency** (MasterData exchange rates, FX differences).
- [ ] **P1-5 Saga completion**
  - Timeouts + retry in `SagaRecoveryHostedService` (logs only today); compensation when shipment
    creation fails.
  - `DeliveryConfirmed` → order Delivered automatically.
  - `SalesOrderReturned` → Warehouse restock, Payment refund, Accounting credit note.
- [ ] **P1-6 Events nobody consumes**: `crm-topic` (OpportunityWon → Reporting/Notification),
  `CouponRedeemed`, `LoyaltyPointsEarned`, `PaymentInitiated`; notifications for order
  shipped/delivered/cancelled and payment failed/refunded; remove Reporting's dead `sales-order-*`
  consumer; decide on `notification-topic`, `sale-order-settings-topic`.
- [ ] **P1-7 Real payroll**: salary per employee/contract, social/health/unemployment insurance,
  progressive PIT with family deductions, annual leave balance; payroll → GL (P1-1).
  Today: 5000 gross and 30 % deduction hard-coded in `ProcessPayrollCommandHandler`.
- [ ] **P1-8 Purchasing**: RFQ / supplier quotations, stored supplier invoices for three-way match,
  returns to vendor.
- [ ] **P1-9 Replenishment**: min/max, safety stock, purchase request → PO automation.
- [ ] **P1-10 Payment gateways**: MoMo, ZaloPay (and/or Stripe) behind `IPaymentGateway`;
  storefront/mobile checkout calls Payment (today only a radio button).
- [ ] **P1-11 Carriers**: GHTK, Viettel Post; carrier selection by rule/price.
- [ ] **P1-12 Notification VN channels**: Zalo ZNS / SMS brandname; template update/delete.
- [ ] **P1-13 Carried over**: ~25 settings categories not applied at runtime; credit limit before
  approve; Redis `RemoveByPattern`; UserCreated → MasterData `IsEmployee`.

## Authorization phase 5

- [ ] Migrate the 56 controllers left in `EndpointAuthorizationTests.InActionCheckAllowlist` to
  `[AuthorizePermission]`.
- [ ] Hubs ticket-only (no raw JWT in the query string); guest chat session checked within a tenant.
- [ ] OpenIddict `urn:` tokens in services (after client_credentials go through `EffectivePermissions`).

## P3 — quality & operations

- [ ] **P3-1 CI/CD**: replace `.github/workflows/docker-cicd.yml` (Identity only, wrong paths, no
  tests) with a matrix pipeline: build + test every service, Gateway, frontends; push images.
- [ ] **P3-2 Tests**: `.csproj` for `tests/integration/ProductService.IntegrationTests` and
  `src/services/PurchaseOrderService/tests`; unit tests for services still without any (CRM, HRM,
  MasterData, Payment, Promotion, Notification, Reporting, ChatBot, Product, Logging); Testcontainers
  integration tests; Vitest + Playwright e2e for checkout; k6 load tests in `tests/performance`.
- [ ] **P3-3 Observability**: OpenTelemetry tracing, Prometheus + Grafana, alerts; healthchecks for
  every compose container.
- [ ] **P3-4 k8s production**: HPA, PodDisruptionBudget, NetworkPolicy, ingress TLS, managed SQL Server,
  multi-node Kafka/Elasticsearch.
- [ ] **P3-5 Backup & DR**: scheduled SQL backups, Elasticsearch snapshots, restore runbook.
- [ ] **P3-6 Clean-up**: duplicate Identity migration numbers (008, 015, `015_test_wrapped.sql`),
  Promotion migrations split in two folders, CRM `TenantIsolation` migration; empty
  `docs/architecture/system-overview.md`, `docs/conventions/git-flow.md`,
  `docs/conventions/sql-standard.md`, `agents/*.yml`; root `.sln` lists only a few projects;
  `.vscode/tasks.json` / `launch.json` cover only Identity.
- [ ] **P3-7 Messaging ADR**: RabbitMQ (SaleOrder, Warehouse) vs Kafka (everything else) — keep both
  deliberately or converge; align `docs/architecture/event-catalog.md` with the code.

## P2 — missing UI

- [ ] **P2-1 Admin**: Promotion, Shipping (shipments + returns), POS back office, Notification
  templates/webhooks, `/settings` index (placeholder); replace ~169 hard-coded permission strings.
- [ ] **P2-2 Storefront**: reset-password, confirm-email, coupon UI, shipment tracking, address book
  with Vietnamese structure (ward/district/province + phone — also needed by GHN, see P0-5),
  wishlist, account security, VNPay redirect.
- [ ] **P2-3 Mobile**: customer — product detail, payment, push (`expo-notifications`); staff — real
  KPIs from Reporting, barcode scanning (`expo-camera`), i18n in the 5 tab screens.
- [ ] **P2-4 Reporting/BI**: facts for Purchasing, CRM pipeline, GL; DimCustomer/DimEmployee/
  DimWarehouse; KPI dashboard (revenue, margin, stock turnover, DSO/DPO).

## P2 — new ERP modules (each needs an ADR first)

- [ ] **P2-5 Fixed assets & depreciation** (asset register, schedules, depreciation entries via P1-1).
- [ ] **P2-6 Shared workflow / approval engine** (replaces hand-written approvals in Purchasing,
  Sales, HRM leave, Warehouse adjustments).
- [ ] **P2-7 HRM extension**: recruitment, training, performance/KPI reviews.
- [ ] **P2-8 Manufacturing**: BOM, routing, MRP, work orders (Product, Warehouse, Purchasing,
  Accounting).

Candidates, not confirmed: budgeting per cost center, shared document management, CRM campaigns.

---

## Done

- [x] 2026-10-22 P0-2…P0-6 (`fix/roadmap-p0`): Logs page + `logs:view` platform-only, supplier
  handlers, bill on PO approval, saga shipment address, POS stock — details in
  `docs/PROJECT-STATUS.md` §2.
