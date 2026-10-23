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

- [ ] **P0-1 First Docker run + merge the open branches**
  - Start Docker, `docker compose up -d --build`, run migrations (`deployment/docker/run-migrations.sh`,
    includes Identity `024`).
  - Walk the checklist in `docs/PROJECT-STATUS.md` §5 (RS256 token, `/internal` calls, logout → 401)
    plus the 2026-10-22 flows:
    - approve a PO → Accounting bill has the PO total;
    - storefront order → approve → shipment has the real address and lines;
    - POS sale → inventory decreases; void it → inventory restored;
    - tenant admin does not see **Logs**; platform admin (`admin.erp.local`) does.
  - Merge in order: `feature/service-tokens` → `docs/system-inventory-roadmap` → `fix/roadmap-p0`.
  - Done when: all checks pass in Docker and the branches are on `master` (PRs on the owner's go).

- [ ] **P0-7 Identity external login & OTP**
  - `ExternalLoginRepository`: Delete/GetAll/GetById/Update throw `NotImplementedException`.
  - External-login auto-provisioning fails (`User.Create` rejects an empty PasswordHash).
  - 2FA email OTP is sent to the *masked* address.
  - Done when: Google/Microsoft first login creates a Customer, OTP reaches the real address, tests in
    `tests/unit/ERP.Identity.UnitTests`.

- [ ] **P0-8 Security hygiene**
  - NuGet `System.Security.Cryptography.Pkcs` 9.0.4 high-severity advisory (transitive, every service).
  - Identity phase 2: invalidate the old 2FA challenge on resend, keep OTP out of `OutboxMessages`,
    Swagger off in production, per-service SQL logins (`Encrypt=True`), re-auth for 2FA changes,
    encrypt webhook secrets, Gateway rate limiting.

## P1 — cross-service business flows

- [ ] **P1-1 Automatic GL posting** (start here)
  - Posting profiles (account mapping per tenant) → `JournalEntry` generated from: invoice, bill,
    payment, refund, stock receipt/issue (inventory, COGS), POS sale, payroll.
  - Remove the dead `warehouse-topic` consumer in Accounting (Warehouse publishes on RabbitMQ
    `warehouse_exchange`).
  - Files: `src/services/AccountingService/**`, `AccountingKafkaConsumer.cs`.
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
