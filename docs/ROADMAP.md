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
>
> Each finished task also updates the BA-level business document of its domain (see `docs/business/Accounting/BA-Accounting.md` for the format).
>
> Problems found while doing the roadmap and left unfixed on purpose are tracked in
> [DEFERRED-ISSUES.md](DEFERRED-ISSUES.md) — handle them after the main tasks.

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
| P1-1d | ✅ 2026-10-24 (`feat/gl-payroll`) — **Payroll → GL**: pay slip pay / cancel lifecycle + events, `PaySlipGenerated` carries gross / overtime / deductions, Accounting posts slip, payment and reversal (new keys SalaryExpense, SalariesPayable, PayrollWithholdingsPayable). Real insurance / PIT / employer cost stay in P1-7. |
| P1-1e | ✅ 2026-10-24 (`feat/gl-refunds`) — **Refunds / returns → GL (money side)**: Payment publishes the refund event only after the gateway confirms (with refund id / method); Accounting posts refund (Dr AR / Cr Cash|Bank) and completed return (Dr Sales returns / Cr AR), and cancelling a paid order reverses the revenue. Restock + COGS reversal and the refund triggered by a return stay in P1-5. **P1-1 is closed.** |
| P1-2 | ✅ 2026-10-24 (`feat/vas-chart`) — **Chart of accounts + closing**: account tree (parent/sub-account, edit, deactivate; parents take no postings), chart templates `Simple14` (default) and `Vas99` (Circular 99/2025, accountant review required), apply-template API, void (hand-written entries of an open period) and reverse (chosen date), period-end closing 911 → 421 with checks and reopen, balance sheet with the unclosed result, admin UI. See `docs/business/Accounting/ChartOfAccounts.md`. |
| P1-2b | ✅ 2026-10-24 (`feat/reports-ledger-cashflow`) — **Reports**: account ledger (sổ cái), trial balance with opening / period / closing columns and roll-up, direct-method cash-flow statement (accounts get `IsCash` + `CashFlowCategory`, migration Accounting 010), admin report tabs. See `docs/business/Accounting/Reports.md`. |
| P1-2c | ✅ 2026-10-24 (`feat/vas-statements`) — **B01-DN / B02-DN by indicator code** (Vas99 only, draft layout for accountant review), prior-period column, CSV export on all report routes, `unmapped` accounts report. See `docs/business/Accounting/Reports.md`. |
| P1-2d | ✅ 2026-10-24 (`feat/b03-yearend-xlsx`) — **B03-DN by indicator code** (direct method, draft layout), **closing policy** Monthly / YearEnd with year-end carry-forward (migration Accounting 011), **Excel (.xlsx) export** on all report routes. BA detail: `docs/business/Accounting/BA-Accounting.md` (FR-48…57, BR-64…79, UC-10…13, UAT-25…39). |
| P1-2e | ✅ 2026-10-24 (`feat/report-profile`) — **amount unit** (VND / thousand / million) on B01 / B02 / B03 with exact-amount integrity checks, **report profile** (company header + signatories, migration Accounting 012) printed on the statements and their CSV / Excel. BA detail: `docs/business/Accounting/BA-Accounting.md` (FR-58…62, BR-80…86, UC-14, UAT-40…45). |
| P1-2f | ✅ 2026-10-25 (`feat/pdf-export`) — **PDF export of B01 / B02 / B03** (`format=pdf`, PDFsharp-MigraDoc, embedded Noto Sans, company header, unit, signatories, page numbers); other reports answer 400. BA detail: `BA-Accounting.md` (FR-63…66, BR-87…93, UC-15, UAT-46…50). No schema change. |
| P1-2g | ✅ 2026-10-25 (`feat/cashflow-indirect`) — **B03-DN by the indirect method** (`method=indirect`, layout B03-DN-GT, reconciled with the direct method, same unit / header / exports). BA detail: `BA-Accounting.md` (FR-67…70, BR-94…102, UC-16, UAT-51…55). No schema change. |
| P1-2h | ✅ 2026-10-25 (`feat/b01-maturity`) — **B01 short- / long-term split of trade receivables and payables by the due date of open invoices and approved bills** (rows 131 / 211 and 311 / 331, ledger-capped, totals unchanged, past-date note). BA detail: `BA-Accounting.md` (FR-71…74, BR-103…108, UC-17, UAT-56…61). No schema change. |
| P1-2i | ✅ 2026-10-25 (`feat/notes-loans-offbalance`) — **14-account layouts** for B01 / B02 / B03, **off-balance accounts** (class 0) with their register and report (migration Accounting 013), **loan ledger** with schedule and exact B01 split of borrowings (014), **B09 notes** with accountant texts, tables from the books and PDF / Excel / CSV (015). BA detail: `BA-Accounting.md` (FR-75…85, BR-109…133, UC-18…21, UAT-62…74). |
| P1-2j | ✅ 2026-10-25 (`feat/p1-2j-loans-history-mapping-english`) — the **loan ledger posts** the drawdown and the repayments with interest and is **reconciled with account 341** (migration Accounting 016, keys LoanPayable / InterestExpense), **payment history** so the maturity split of B01 / B09 is exact for past dates (017), a tenant's **own chart mapped to the rows of B01 / B02** (018), **notes B09 in English** per language with a generated non-cash table and a prior-period column (019). BA detail: `BA-Accounting.md` (FR-86…101, BR-134…155, UC-22…25, UAT-75…91). |
| P1-2k | ✅ 2026-10-25 (`feat/p1-2k-loan-interest-mapping-versions`) — **receiving account per loan**, **accrued loan interest** with interest actually paid and an interest check (migration Accounting 020, key InterestPayable), **mapping of a tenant's own chart for B03 (direct and indirect) and the notes**, **note texts per fiscal year** with carry-forward (021). BA detail: `BA-Accounting.md` (FR-102…111, BR-156…167, UC-26, UAT-92…101). |
| P1-2l | ✅ 2026-10-25 (`feat/p1-2l-accrual-job-daycount-history-languages`) — **day count per loan** (schedule / actual-365 / actual-360), **opt-in month-end accrual job** (closed period skipped), **history of note-text edits**, **notes in Japanese, Chinese and French** (PDF for French only); migration Accounting 022. BA detail: `BA-Accounting.md` (FR-112…122, BR-168…175, UC-27, 28, UAT-102…112). |
| P1-2m | Rate changes and bank-statement based interest, PDF in Japanese / Chinese (CJK font), reviewed translations, history of other report settings (DEFERRED §4l). |
| P1-3 | ✅ 2026-10-25 (`feat/p1-3-vat-einvoice`) — **VAT** on sales invoices, returns and bills (tenant rate with per-document override, key `VatInput`), **VAT listing and simplified return 01/GTGT** (csv / xlsx / pdf), **e-invoice adapter** with a sandbox provider (issue, cancel, lookup code, VAT locked while issued); migration Accounting 023. BA detail: `BA-Accounting.md` (FR-123…134, BR-176…188, UC-29…32, UAT-113…125). |
| P1-3b | ✅ 2026-10-26 (`feat/p1-3b-vat-lines-settlement`) — **VAT lines** (several rates, goods not subject to VAT) on invoices and bills, return by the invoice's VAT share, **VAT settlement** entry, **replacement / adjustment e-invoices** and auto-issue, **unposted-document warning** and repost, **HTKK-style XML** of the return; migration Accounting 024. BA detail: `BA-Accounting.md` (FR-135…146, BR-189…200, UC-33…36, UAT-126…140). |
| P1-3c | Real e-invoice connector (VNPT / Viettel / MISA / BKAV: needs the owner's provider account), the authority's invoice code and signing, validation of the XML against the official HTKK schema, VAT payment entry, per-line VAT from Sales / Purchasing (DEFERRED §4n). |
| P1-4 | ✅ 2026-10-26 (`feat/p1-4-fx-bank-reconciliation`) — **foreign currency**: invoices / bills in another currency (rate typed, suggested from MasterData through a service-to-service route), base currency setting, **realized exchange difference** on payment (keys FxGain / FxLoss), **period-end revaluation** of open foreign receivables and payables (reversed next day); **line-level bank reconciliation**: statement lines (CSV import), automatic and manual matching to the bank's ledger lines, difference to the closing balance, **fee / interest entries from a statement line**; migration Accounting 025. BA detail: `BA-Accounting.md` (FR-147…157, BR-201…213, UC-37…39, UAT-141…158). |
| P1-4b | Foreign bank accounts and the cash-flow FX effect (row 61), conversion of Payment events in a foreign currency, redoing a revaluation, VAT lines on foreign documents, a statement line linked to a loan repayment / interest (DEFERRED §4o). |
| P1-5 | ✅ 2026-10-26 (`feat/p1-5-saga-completion`) — **saga timeouts, retries and compensation** (`Saga:*` options, `SagaRecoveryHostedService` pushes the stuck step then refunds + cancels), **shipment creation failure** (`ShipmentCreationFailed`, one shipment per order), **`DeliveryConfirmed` → Delivered**, **returns**: Warehouse restocks at the order's issue cost (movement `Returned`, Accounting Dr Inventory / Cr COGS) and Payment refunds; consumers retry 5 times instead of requeueing for ever; **Order processing card** on the order page; fixed Warehouse never reading Sales' cancelled / shipped events. No migration. BA detail: `docs/business/Sales/OrderProcessing.md`, `BA-Accounting.md` (FR-158…159, BR-214, UC-40, UAT-159…160). |
| P1-5b | Partial returns by line, saga on the outbox, per-tenant timeouts, a screen for dead-lettered messages (DEFERRED §4p). |
| P1-6 | ✅ 2026-10-26 (`feat/p1-6-event-consumers`) — **events nobody consumed**: customer e-mails for order created / paid / shipment prepared / shipped / delivered / cancelled / returned, payment failed / refunded (the order events now carry the customer e-mail; Notification keeps the recipient per order; templates fixed, one message per fact, 5 retries); loyalty points earned once and taken back on cancel / return, points and coupon messages; CRM `OpportunityWon` → Reporting, `CaseResolved` → customer; Reporting: dead `sales-order-*` consumer removed, partial vs full refunds, pay slip net pay and breakdown (G13, AH7), returned orders; admin *Sent notifications* and *Message templates* (read-only). Migrations Notification 013, Reporting 008. BA detail: `docs/business/Notification/CustomerNotifications.md`. |
| P1-6b | Template editing (P1-12), earn points on payment, Reporting inventory facts (`warehouse-topic` has no producer), message resend, per-customer language, `notification-topic` / `sale-order-settings-topic` (DEFERRED §4r). |
| P1-7 | ✅ 2026-10-26 (`feat/p1-7-real-payroll`) — **real payroll**: salary terms with history, dependents, date-effective rate sets (insurance rates, caps, deductions, progressive tax brackets; seeded DRAFT defaults), worked days from attendance and unpaid leave, overtime, **annual leave balance**; pay slips carry the breakdown and Accounting posts it (Dr wages + employer insurance / Cr net, 3383 / 3384 / 3389 / 3335); employees without salary terms are skipped and listed; admin: Salary profiles, Payroll settings, pay slip detail. Migration HRM 008. BA detail: `docs/business/HRM/Payroll.md`, `BA-Accounting.md` (FR-160…163, BR-215…216, UC-41, UAT-161…164). |
| P1-7b | Payroll leftovers: PIT year-end settlement, overtime premium exemption, seniority leave accrual, holiday calendar, regional minimum wage, several contracts, cost centres, payslip PDF, Reporting net pay (DEFERRED §4q). |
| P1-8 | ✅ 2026-10-26 (`feat/p1-8-purchasing-rfq-match-returns`) — **RFQ and supplier quotations** (invite, quote, compare, award → draft PO), **supplier invoices stored and matched three ways** (order ↔ receipts ↔ invoice, per-tenant tolerances 0 % / 2 %, hold and approve-with-variance permission), **the bill now comes from the approved invoice** (price variance account; the order approval no longer books one), **returns to vendor** (stock out, debit note, supplier credit on the bill); admin RFQs, supplier invoices, returns, match settings and *Receive goods*. Migrations Purchasing 012, Accounting 026, Identity 029. BA detail: `docs/business/Purchasing/ProcureToPay.md`, `BA-Accounting.md` (FR-164…167, BR-217…219, UC-42, UAT-165…168). |
| P1-8b | RFQ from a purchase request, e-mail / portal for suppliers, foreign-currency supplier invoices, partial return restocks and replacements, bills screen with invoice number and credit (DEFERRED §4s). |
| P1-9 | ✅ 2026-10-26 (`feat/p1-9-replenishment`) — **warehouse replenishment**: min / max / safety stock per product (edited on the inventory page), low stock on **available** stock, one alert per time below the minimum (+ closing message when stock recovers, daily per-tenant scan job and *Scan stock now*), Purchasing proposals sized `max − (available + on order)` with the product's **preferred supplier**, *Create orders* → one **draft PO per supplier** + one **RFQ** for products without a preferred supplier; admin: Stock policy card, Purchase requests page, Preferred flag on supplier items. Migrations Warehouse 009, Purchasing 013. BA detail: `docs/business/Warehouse/Replenishment.md`. |
| P1-9b | Replenishment leftovers: demand-based minimums, MOQ / pack size, policy per warehouse, auto-submit of the draft order, e-mail to suppliers (DEFERRED §4t). **Risk register** (DEFERRED §6) to be worked through after the main roadmap tasks. |
| P1-10 | ✅ 2026-10-27 (`feat/p1-10-online-payments`) — **online payments**: MoMo and ZaloPay beside VNPay behind `IPaymentProvider` (per-shop encrypted accounts, one verified notification path with an amount check, anonymous signed Gateway routes), **cash on delivery** (the order does not wait; captured on delivery), the order records the customer's method, **real checkout** in the storefront (methods the shop offers, gateway redirect, result page) and mobile (picker, order screen); admin: gateway accounts per shop, COD switch, refund button. A cancelled order closes its pending payment; late money is flagged for refund. Migrations Payment 008, Sales 013. BA detail: `docs/business/Payment/OnlinePayments.md`. |
| P1-10b | Stripe / cards in other currencies, saved cards, ZaloPay refund status poll, payment expiry job, gateway statement reconciliation, e-mail on failed payment, real merchant accounts + tunnel test (DEFERRED §4u). Risk register R11…R25. |
| P1-10c | ✅ 2026-10-27 (`chore/risk-cleanup-p1-6-10`) — **risk clean-up before P1-11**: default roles per tenant (Identity 030), manual cancel closes the pending payment, payment-event idempotency, payment list filters, client/backend contract guard, mobile tenant host, runner-vs-migrations test, all 16 unit projects run, and a **browser walk-through** that found and fixed six defects (admin bell hub, storefront register, duplicate display name → 500 (Identity 031), plain-http `randomUUID`, customer order list 403). BA detail: `docs/business/Identity/CustomerSignup.md`. |
| P1-11 | ✅ 2026-10-27 (`feat/p1-11-carriers`) — **carriers**: a per-shop account (encrypted) at GHTK, Viettel Post and GHN behind `ICarrierProvider`; selection by the shop's rules then the cheapest quote with failover (a refusal is never swallowed; nobody accepts → the order is cancelled with all reasons); the carrier gets the cash to collect, declared value, weight and recipient phone; carrier webhooks (anonymous, shared-secret verified, idempotent) + a polling job + cancel at the carrier; delivery captures the COD payment; admin: carrier accounts, rules with a parcel tester, pickup address, Shipments page; storefront: phone at checkout. Migrations Shipping 008, Sales 014. **GHTK and GHN checked against their docs; Viettel Post written without access to its docs (unverified); J&T is a placeholder.** BA detail: `docs/business/Shipping/CarrierIntegration.md`. |
| P1-11b | ✅ 2026-10-28 (`feat/p1-11b-shipping-fee`) — **delivery fee charged to the customer**: the shop's flat fee, free from a threshold (decided by Shipping, asked by Sales through an internal call, fail closed), `SalesOrder.ShippingFee` inside the total / payment / COD cash / invoice; **ward** in the address (storefront, admin form, saga, carrier request); **real product weights** (internal Product endpoint, default weight on a miss); loyalty points on goods only; the order created event now carries the final total (it used to ignore the coupon). Migration Sales 015. BA detail: `docs/business/Shipping/ShippingFee.md`. |
| R38 | ✅ 2026-10-28 (`fix/r38-variants-and-service-token-keys`) — product variants can be created through the API again; the first service call after a redeploy is no longer refused (R39 left). |
| P1-11c | **Split into three branches (owner, 2026-10-28):** (1) ✅ 2026-10-28 `feat/p1-11c-fee-table` fee table by province × weight band with the flat fee as fallback, delivery revenue on its own key `ShippingRevenue` + invoice delivery part, the delivery fee refunded on a return only when the approver ticks it (migrations Shipping 009, Sales 016, Accounting 027; BA: `ShippingFee.md` §13); (2) `feat/p1-11d-returns-and-parcels` returns through the carrier, several parcels per order; (3) `feat/p1-11e-jnt-vtp` J&T and Viettel Post checked against public docs, unverified parts stay flagged. Original list — left from P1-11/11b: carrier price shown to the customer or a fee table by province / weight; a separate posting key for delivery revenue; returns through the carrier; one order in several parcels; J&T Express and a verified Viettel Post; **real sandbox run for GHTK / Viettel Post / GHN through a tunnel (owner-parked, DEFERRED 4w)**. Risk register R26…R38. |
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
