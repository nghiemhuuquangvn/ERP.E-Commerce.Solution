# System Inventory & Roadmap

> Created 2026-10-22 from a code-level inventory of all 17 services, the Gateway, the 4 frontend
> apps, infrastructure and tests, compared with a standard ERP scope (SAP Business One / Odoo /
> Dynamics 365 Business Central) plus Vietnamese legal requirements (VAS, e-invoice, social
> insurance, PIT). `docs/PROJECT-STATUS.md` §4 points here; keep both in sync.
>
> Reading note: `docs/business/ERP-Business-Analysis.md` rates the services ~95% complete. That
> figure measures API/domain coverage per service. The code shows the gaps are **between**
> services (events nobody consumes, no automatic GL posting, hard-coded payroll), in the
> **frontends**, in **tests** and in **operations**.

## Part A — Completed

### A1. Platform & security (2026-07 → 2026-10-21)
- Clean Architecture / DDD / CQRS in 17 services; Ocelot Gateway (39 routes); docker-compose with
  SQL Server, RabbitMQ, Kafka (KRaft), Redis, Elasticsearch, Azurite, nginx; k8s manifests
  (21 Deployments, 5 StatefulSets, ingress, probes, resources).
- Outbox rules (`OutboxEventDeserializer`, stable `EventId`, batching, dead-lettering).
- Multi-tenant isolation everywhere (tenant = request host, query filters, `EnforceTenantOnSave`,
  `ITenantEvent`, per-tenant jobs, per-tenant business codes, tenant-filtered Elasticsearch) —
  PR #1, #4–#6; architecture + two-tenant tests for every DbContext.
- Authorization: platform vs tenant permissions (`PermissionScopes`, `EffectivePermissions`),
  no role-name authority, FallbackPolicy + explicit attributes (PR #2, #3); token revocation via
  Redis (PR #8); RS256/JWKS with Key Vault (PR #9); per-service service-to-service tokens,
  `/internal` endpoints, shared `Jwt:Key` removed (`feature/service-tokens`, not merged yet).
- Identity (122 endpoints): JWT + refresh, OpenIddict, SSO (Google/Microsoft/Facebook/Twitter
  PKCE), 2FA SMS/Email, audit log, sessions, scoped API keys, password policy, lockout, license
  plans/modules, feature flags, ~10 settings groups.

### A2. Business capabilities (API/domain level)
| Service | Endpoints | Done |
|---|---|---|
| Product | 74 | Catalog, variants, EAV attributes, brands, categories, price lists, images, bundles, cross/up-sell, reviews, UoM conversion, CSV bulk import/export, Elasticsearch search |
| Sales | 38 | Quotation → order, approval, credit limit check on create, partial shipment, returns, commissions, status timeline, `OrderFulfillmentSaga` (payment → reservation → shipment) |
| Purchasing | 29 | Two-level PO approval (manager / finance), GRN, receipt scheduling, supplier price comparison, three-way match (query), supplier rating, purchase requests from stock alerts |
| Warehouse | 54 | Multi-warehouse, bins, lots/serials/expiry, reservations + expiry job, transfers, stocktakes, approved adjustments, FIFO/LIFO/Average valuation + period snapshots, barcodes |
| Accounting | 39 | Flat COA, manual journal posting (balanced, closed-period guard), invoices/bills, AR/AP aging, bank accounts/statements, tax rates, trial balance, P&L, balance sheet |
| Payment | 25 | VNPay (signed IPN, refund API), settlements/reconciliation, saved methods, installments |
| Shipping | 25 | GHN (tracking, fees, PDF label), return shipments (RMA), POD, delivery retry |
| POS | 20 | Terminals, shifts with denomination count, split tender, Z-report, offline sync, customer display |
| Promotion | 21 | Rule engine, bulk coupons, flash sales, loyalty, scheduler |
| CRM | 35 | Leads → opportunities (pipeline), cases, activities, contacts + tags |
| HRM | 24 | Attendance + overtime, work shifts, leave approval, payroll/payslips (basic) |
| MasterData | ~111 | Org structure (company → team), employees, cost/profit centers, currencies + rates, payment terms, UoM, VN geography, calendars/holidays, languages |
| Notification | 32 | SMTP, Twilio SMS, FCM push, SignalR, HMAC webhooks, templates, preferences, retry with backoff |
| Reporting | 7 | Facts: Sales, Payment, InventoryMovement, POSTransaction, HRPayslip; DimDate, DimProduct; CSV export |
| ChatBot / Logging | 4 / 2 | Claude/ChatGPT by license, 6 Gateway tools, CRM escalation / Kafka → Elasticsearch log store |

### A3. Frontends
- Admin (React 18): ~100 routes covering Sales, Purchasing, Product, Inventory, CRM, HRM,
  Accounting, MasterData, Identity, Settings; i18n en/vi.
- Storefront (Vue 3): catalog, cart, 3-step checkout, my orders, profile, chat widget; i18n en/vi.
- Mobile customer / staff (Expo 51): MVP screens.

## Part B — Backlog

Execution order agreed with the owner (2026-10-22): **P0 → P1 business flows → authorization
phase 5 → P3 CI/CD, tests, operations → P2 missing UI → P2 new modules.** Every item still goes
plan → approval → implement + tests + docs, one branch per plan.

### P0 — Broken behaviour, wrong data, security
| ID | Item | Evidence / where |
|---|---|---|
| P0-1 | ✅ 2026-10-23 (`fix/docker-first-run`) — first Docker run done, PR #10–#12 already merged. Auth, tenant/Logs and PO → bill verified; POS ↔ stock and storefront → shipment blocked by P0-9 | PROJECT-STATUS §2, `docs/architecture/schema-drift-2026-10-23.md` |
| P0-9 | ✅ 2026-10-23 (`fix/schema-alignment`) — closed, see `docs/architecture/schema-drift-2026-10-23.md`. Was: **SQL schema does not match the EF models** (37 missing columns/tables, 30 NOT NULL blockers, type mismatches such as POS `PaymentMethod` INT vs string) and 40 `JObject.Value<Guid?>` calls lose Kafka messages. Found by P0-1; oracle = `LiveSchemaDriftTests` | `docs/architecture/schema-drift-2026-10-23.md` |
| P0-2 | ✅ 2026-10-22 (`fix/roadmap-p0`) — Admin build is broken: `LogsPage` imported but missing → build the log viewer (`GET /api/v1/logs`). Root cause: `.gitignore` `[Ll]ogs/` hid `pages/logs` and `api/logs`; `logs:view` is now a platform permission (logs have no tenant) | `src/frontend/admin/src/router/index.tsx:43` |
| P0-3 | ✅ 2026-10-22 — Supplier handlers are stubs (list returns empty, get maps `new object()`, deactivate is a no-op) | `src/services/PurchaseOrderService/src/ERP.PO.Application/Features/Suppliers/Handlers/` |
| P0-4 | ✅ 2026-10-22 — Bills created from POs always have amount 0 (event has no total) → bill booked on final approval with the PO total | `src/services/AccountingService/src/ERP.ACC.Infrastructure/BackgroundJobs/AccountingKafkaConsumer.cs` |
| P0-5 | ✅ 2026-10-22 — Saga-created shipments have placeholder recipient/address — `CreateShipmentCommand` must carry the delivery address | `src/services/ShippingService/src/ERP.SHP.Infrastructure/EventBus/SalesOrderRabbitMqConsumer.cs` |
| P0-6 | ✅ 2026-10-22 — POS sales never reduce stock (voided completed sales now restock too) — Warehouse must consume `pos-topic` `TransactionCreated` | POS `Program.cs` comment |
| P0-7 | ✅ 2026-10-23 (`fix/identity-external-login`) — was: Identity: `ExternalLoginRepository` throws `NotImplementedException`; external auto-provisioning fails; 2FA email OTP goes to the masked address | PROJECT-STATUS §4 P1 |
| P0-8 | ✅ 2026-10-23 (`fix/security-hygiene`) — was: NuGet `System.Security.Cryptography.Pkcs` advisory; Identity phase 2 hardening | PROJECT-STATUS §4 P0 |
| P0-10 | Per-service SQL logins (least privilege) instead of `sa`, `Encrypt=True` with a trusted certificate; Notification's own webhook secrets; phone-number verification for SMS 2FA (split off P0-8) | SecurityReview M6 / M7 |

### P1 — Cross-service business flows
| ID | Item |
|---|---|
| P1-1 | ✅ phase 1 2026-10-23 (`feat/gl-auto-posting`) — **Automatic GL posting**: posting engine + profile (account mapping), invoice, payment, order cancellation, bill flows; the dead `warehouse-topic` consumer is gone. See `docs/business/Accounting/AutoPosting.md`. |
| P1-1b | ✅ 2026-10-24 (`feat/gl-stock-cogs`) — **Stock + COGS → GL**: `StockMovementRecordedEvent` carries `UnitCost` / `ValueDelta`, PO receipts use the PO price (and partial receipts of one product are no longer dropped), Accounting posts receipt (Dr Inventory / Cr GRNI), issue (Dr COGS / Cr Inventory) and adjustments; bills debit GRNI. POS stock cost is included; POS revenue is P1-1c. |
| P1-1c | ✅ 2026-10-24 (`feat/gl-pos`) — **POS → GL**: `pos-topic` events carry subtotal / discount / tax / tenders / shift and the shift's counted, expected cash and variance; Accounting posts sale (Dr Cash/Bank, Cr Revenue + VAT), void (reversal) and cash over/short. Shift totals now exclude voided sales. |
| P1-1d | Payroll → GL: gross, overtime, deductions split, employer costs and a paid transition in the pay slip event (with P1-7). |
| P1-1e | Refunds and returns → GL: publish the payment refund event only when the gateway confirms; Sales return lines; credit note. |
| P1-2 | Hierarchical VAS chart of accounts (Circular 200/133 — confirm the currently applicable replacement), reversing/voiding entries, period-end closing entries (911 → 421), cash-flow statement. |
| P1-3 | Tax: VAT fields on invoice/bill, VAT listing/return; **e-invoice** (Decree 123/2020, Circular 78/2021) through a provider adapter (VNPT / Viettel / MISA / BKAV). |
| P1-4 | Line-level bank reconciliation (statement line ↔ payment/bill); multi-currency with MasterData exchange rates and FX differences. |
| P1-5 | Saga completion: timeouts + retry in `SagaRecoveryHostedService` (logs only today), compensation when shipment creation fails; consume `DeliveryConfirmed` → order Delivered; consume `SalesOrderReturned` in Warehouse (restock), Payment (refund), Accounting (credit note). |
| P1-6 | Events nobody consumes: `crm-topic` (OpportunityWon → Reporting/Notification), `CouponRedeemed`, `LoyaltyPointsEarned`, `PaymentInitiated`; Notification for order shipped/delivered/cancelled and payment failed/refunded. Remove Reporting's dead `sales-order-*` Kafka consumer; decide on `notification-topic` and `sale-order-settings-topic`. |
| P1-7 | Real payroll: salary per employee/contract, social/health/unemployment insurance, progressive PIT with family deductions, annual leave balance; payroll → GL (P1-1). Today: 5000 gross, 30 % deduction hard-coded in `ProcessPayrollCommandHandler`. |
| P1-8 | Purchasing: RFQ / supplier quotations, stored supplier invoices for three-way match, returns to vendor. |
| P1-9 | Warehouse replenishment: min/max, safety stock, purchase request → PO automation. |
| P1-10 | Payment: MoMo, ZaloPay (and/or Stripe) behind `IPaymentGateway`; storefront/mobile checkout calls Payment (today a radio button only). |
| P1-11 | Shipping: GHTK, Viettel Post; carrier selection by rule/price instead of "first active carrier". |
| P1-12 | Notification: Zalo ZNS / Vietnamese SMS brandname; template update/delete. |
| P1-13 | (carried over) ~25 settings categories not applied at runtime; credit limit before approve; Redis `RemoveByPattern`; UserCreated → MasterData `IsEmployee`. |

### Authorization phase 5 (after P1)
Migrate the 57 allowlisted controllers to attributes, hub ticket-only, guest chat session checks,
OpenIddict `urn:` tokens in services — see PROJECT-STATUS §8.

### P3 — Quality & operations
| ID | Item |
|---|---|
| P3-1 | CI/CD: replace `.github/workflows/docker-cicd.yml` (Identity only, wrong paths, no tests) with a matrix pipeline: build + test every service, Gateway, frontends; push images. |
| P3-2 | Tests: add `.csproj` to `tests/integration/ProductService.IntegrationTests` and `src/services/PurchaseOrderService/tests`; unit tests for the 15 services without any (saga, GL posting, payroll first); Testcontainers integration tests; frontend Vitest + Playwright e2e for checkout; k6 load tests in `tests/performance`. |
| P3-3 | Observability: OpenTelemetry tracing (Gateway → services → Kafka/RabbitMQ), Prometheus metrics + Grafana, alerts; healthchecks for every compose container (5 today). |
| P3-4 | k8s production: HPA, PodDisruptionBudget, NetworkPolicy, ingress TLS (cert-manager), managed SQL Server, multi-node Kafka/Elasticsearch. |
| P3-5 | Backup & DR: scheduled SQL backups, Elasticsearch snapshots, restore runbook. |
| P3-6 | Clean-up: duplicate Identity migration numbers (008, 015, `015_test_wrapped.sql`), Promotion migrations split across `Promotion/` and `PromotionService/`, CRM has no `TenantIsolation` migration; empty `docs/architecture/system-overview.md`, `docs/conventions/git-flow.md`, `docs/conventions/sql-standard.md`, `agents/*.yml`; root `.sln` lists only 7 projects. |
| P3-7 | Messaging ADR: SaleOrder/Warehouse on RabbitMQ, everything else on Kafka — keep both deliberately or converge; bring `docs/architecture/event-catalog.md` in line with the code. |

### P2 — Missing UI
| ID | Item |
|---|---|
| P2-1 | Admin: Promotion, Shipping (shipments + returns), POS back office, Notification templates/webhooks, `/settings` index (placeholder); replace ~169 hard-coded permission strings. |
| P2-2 | Storefront: reset-password, confirm-email, coupon UI, shipment tracking, address book, wishlist, account security, VNPay redirect. |
| P2-3 | Mobile customer: product detail, payment, push (`expo-notifications`). Staff: real KPIs from Reporting (hard-coded today), barcode scanning (`expo-camera`), i18n in the 5 tab screens. |
| P2-4 | Reporting/BI: facts for Purchasing, CRM pipeline, GL; DimCustomer/DimEmployee/DimWarehouse; KPI dashboard (revenue, margin, stock turnover, DSO/DPO). |

### P2 — New ERP modules (in scope, confirmed 2026-10-22)
Each needs an ADR first (own service vs inside an existing one, port/code, events).
| ID | Module |
|---|---|
| P2-5 | Fixed assets & depreciation (asset register, depreciation schedules, automatic depreciation entries via P1-1). |
| P2-6 | Shared workflow / approval engine replacing the hand-written approvals in Purchasing, Sales, HRM leave, Warehouse adjustments. |
| P2-7 | HRM extension: recruitment, training, performance/KPI reviews (7 of 11 BA sub-modules missing). |
| P2-8 | Manufacturing: BOM, routing, MRP, work orders — linked to Product, Warehouse, Purchasing, Accounting. |

Candidates, not confirmed: budgeting per cost center, shared document management, CRM campaigns.
