# Project Status & Handoff

> Read this right after `CLAUDE.md` when picking the work up on another machine.
> Keep it current: update it at the end of every work session (what changed, what's next).
> Last updated: **2026-10-23** (ROADMAP P0-1 first Docker run on `fix/docker-first-run`; new P0-9 schema drift; system inventory + roadmap: `docs/ROADMAP.md`).

> Business documentation (BA level) per domain: start with `docs/business/Accounting/BA-Accounting.md`; every task must update it (rule 10 in `Claude.md`).
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

### 2026-10-31 — ROADMAP P2-9 U1: design tokens + Lucide icons in the four apps (`feat/ui-u1-tokens-icons`, unmerged; branched from master)
- **Tokens:** `docs/frontend/design-system/tokens/tokens.json` is the source; `tools/build-tokens.js` generates Admin / Storefront (`tokens.css`, `tokens.ts`, `tokens.preset.cjs`) and Customer / Staff apps (`theme/theme.ts`, `tailwind.tokens.js`); `npm run check:tokens` in each app fails on drift, on a failing contrast pair and on a hex literal in app code. Base classes (`.btn*`, `.input`, `.card`, body, `.btn-brand`) read role tokens; Admin action colour is now `#0369a1` (white text 5.93:1, was 4.10:1), control border and placeholder meet AA / 3:1. Old numeric names (`primary-600`, `brand-500`) are aliases until U3.
- **Icons:** Lucide replaces emoji in the Admin sidebar (114 items, collapsed items have a title), header, bell, saga marks, warnings; Storefront header / placeholders / checkout / payment result / chat; both mobile tab bars and the Customer cart / profile / search / home screens. Chart colours come from tokens.
- **Checks:** `tsc -b`, `vue-tsc -b`, `tsc --noEmit` (both mobile apps) clean; Admin and Storefront deployed to Docker and viewed in headless Chrome as `glcheck` (login, Returns, Webhooks, Reports, Storefront home) — the first browser look at the P2-1b screens too. **Not verified:** the mobile bundle / device (R68), pages other than the five seen. New risks R67-R70; DS-1 and DS-3 closed. Guide: `docs/frontend/tokens-and-icons-guide.md`. Next in the owner's order: U2 (core components) or the backend list.

### 2026-10-31 — ROADMAP P2-1b: Shipping returns screen, Notification webhooks and template editing (`feat/p2-1b-returns-webhooks-templates`, stacked on R43, unmerged)
- **Shipping** (migration **011**): `GET shipments/returns` (paged; status, search, needs attention), `POST returns/{id}/rebook`, `POST returns/{id}/cancel` (at the carrier first). **Notification** (migration **014**): `WebhookUrlRules` moved to `ERP.Shared.Security` (Identity uses it too); webhooks keep a sealed secret that an edit keeps, validate address and events, have an event catalog, `POST test`, a delivery log (`WebhookDelivery`, 30 days) and fire `notification.failed`; templates: update, activate / deactivate, delete, override, preview; a switched-off template skips the message; both controllers moved to attribute authorization (allowlist −2).
- **Admin**: Returns page, Webhooks page, Templates editor (placeholder chips, live preview), menu, en / vi. Guide: `docs/frontend/shipping-notification-admin-guide.md`.
- Tests: SHP 176, NTF 57, Identity 259, Shared 103, Architecture 68; `tsc -b` clean. Live on `glcheck` (fake GHTK, example.com as a webhook target): UAT S1…S7, W1…W7, T1…T6 in the two BA docs. Not done: a browser walk-through; a webhook answering 2xx. Next in the owner's order: **U1** (UI tokens + Lucide icons, branch `docs/ui-design-standard`).

### 2026-10-28 — ROADMAP R43: cash on delivery captured per parcel (`feat/r43-per-parcel-cod`, unmerged)
- **Payment** (migration **009**): `PaymentCapture` per shipment (unique key = idempotency), `Payments.CapturedAmount`, status `PartiallyCaptured`, `Payment.CapturePart`; `PaymentProcessed` per parcel with `CaptureId` / `CapturedTotal`; refunds capped at `Collected`; the delivery consumer no longer waits for the last parcel. **Accounting** keys the entry and the invoice credit by capture; **Reporting** (migration **009**, `FactPaymentCaptures`) adds each part once; **Notification** sends one receipt per part; **Admin** shows "Partly collected".
- Fixes found by the live check: Reporting dropped payment events with a numeric method and counted split payments twice (`PaymentAuthorized` created the fact).
- Tests: PAY 99, ACC 595, RPT 18, NTF 19, SHP 169, SO 84, Architecture 68, Shared 103; `tsc -b` / `vue-tsc` clean. Live on `glcheck` (fake GHTK): UAT C1…C6 in `OnlinePayments.md` §13. Next in the owner's order: P2-1b, then U1.

### 2026-10-28 — ROADMAP P2-9: UI design standard for Web + Mobile (`docs/ui-design-standard`, document only, unmerged; branched from the P2-1a branch)
- **Written** in `docs/frontend/design-system/`: README (decisions, principles), foundations (colour roles for two themes, type, space, radius, motion, breakpoints, density, Lucide icons with the emoji → icon map for every nav / tab icon), components (spec + mapping to today's code), patterns-web (page-state matrix, Admin shell / list / form / detail / dashboard / settings, Storefront journeys), patterns-mobile (Customer + Staff screens, offline, scan), content-and-i18n, accessibility (computed contrast table), adoption-roadmap (U1–U7), `preview.html` (both themes, buttons, inputs, badges, tables, states, phone mock-up) and `tools/contrast.js`.
- **Findings that change today's values:** white on Admin button `#0284c7` is 4.10:1 (fails AA) → Back-office action becomes `#0369a1` (5.93); placeholder `#9ca3af` is 2.54 and control borders `#d1d5db` 1.47 → `#6b7280` (4.83).
- No code under `src/` changed. Not done: dark mode, accessibility audit of existing pages, shared money / date helpers (rules written). Next: owner chooses U1 or continues the backend.

### 2026-10-28 — ROADMAP P2-1a: Promotion + POS back office in the Admin app (`feat/p2-1a-admin-promotion-pos`, unmerged)
- **Backend** (no migration, no event): Promotion `PUT promotions/{id}` (draft only, `Promotion.Update`; other status = HTTP 400) and `GET promotions/{id}/coupons`; POS `GET pos/transactions` and `GET pos/shifts` (paged, filtered, newest first, last day inclusive, unknown status = 400) and `GET pos/terminals/{id}`.
- **Admin**: groups *Promotions* (promotions with coupons dialog, flash sales, loyalty) and *Point of sale* (transactions with void, shifts with cash difference, Z report, terminals); `api/promotion/promotion.api.ts`, `api/pos/pos.api.ts`; `promotion.*` / `pos.*` / `nav.*` in en + vi. Guide: `docs/frontend/promotion-pos-admin-guide.md`.
- Tests: PRM 17, POS 15, Architecture 68; `tsc -b` clean. Live on `glcheck` (API level, data removed): UAT S1…S6 / S1…S9 in the two `AdminBackOffice.md` docs. Not done: a browser walk-through of the screens. Left in P2-1: P2-1b (Shipping returns page, Notification webhooks / template edit), P2-1c (`/settings` index, permission constants).

### 2026-10-28 — ROADMAP P1-11d (2/3): several parcels + returns through the carrier (`feat/p1-11d-returns-and-parcels`, unmerged)
- **Shipping** (migration **010**): `Shipments.ParcelNo/ParcelCount`; `POST shipments/{id}/split` (`SplitShipmentCommandHandler`, `ParcelSplitPlanner`: books the new parcels first, cancels the original, undoes everything on a refusal); `ReturnBookingService` books the way back as the forward booking with the addresses swapped (`internal/v1/returns`); `ReturnShipment` carries the carrier booking and goes through the same webhook / polling path; `DeliveryConfirmed(OtherParcelsPending…)`, `ReturnReceived(OtherReturnsPending)`.
- **Sales** (migration **017**): `return/approve` (asks Shipping; unreachable = refused, answered-but-nothing-booked = approved by hand), `CompleteReturn` waits for the goods when a pickup is booked, the order is Delivered with the last parcel; the order read is no longer cached. **Payment**: the COD is captured with the last parcel (one payment per order).
- **Admin**: split dialog + parcel badge, return panel (approve / book again / goods received / refund waits).
- Tests: SHP 169, SO 84, PAY 84, Architecture 68, Shared 103; `tsc -b` clean. Live on `glcheck` (fake GHTK): UAT S12…S17 in `ParcelsAndReturns.md`. Not done: browser walk-through, a carrier refusal live, Viettel Post / GHN reverse orders. Next: `feat/p1-11e-jnt-vtp`.

### 2026-10-28 — ROADMAP P1-11c (1/3): delivery fee table (`feat/p1-11c-fee-table`, unmerged)
- **Shipping** (migration **009**): `ShippingFeeRules` price list (province × weight band), `ShippingFeePolicy` picks the first matching row by priority else the flat fee, the free threshold on top; `POST api/v1/shipping/fee` + `POST internal/v1/shipping-fee` take the province and the cart (weights from ProductService); `api/v1/shipping/fee-rules` CRUD.
- **Sales** (migration **016**): the fee request carries province + lines; `CompleteReturn(amount, refundShippingFee)` refunds the goods and the delivery fee only when ticked (the admin used to send the order total, i.e. refund the fee by default).
- **Accounting** (migration **027**): posting key `ShippingRevenue` (5113 Vas99, 4000 simple, falls back to sales revenue), `Invoice.ShippingNetAmount`, the entry splits goods and delivery.
- **Admin**: Delivery fees tab with a try-a-parcel box, return dialog with the tick, delivery column on invoices. **Storefront**: preview with province + cart.
- Tests: SHP 153, SO 78, ACC 593, Architecture 68; `tsc -b` / `vue-tsc` clean. Live on `glcheck`: UAT S7…S11 in `ShippingFee.md` §13.6. Next: `feat/p1-11d-returns-and-parcels`, `feat/p1-11e-jnt-vtp`.

### 2026-10-28 — R38: variants and service-token keys (`fix/r38-variants-and-service-token-keys`, merged as PR #45)
- **Product**: `IProductVariantRepository.AddAsync`; `CreateProductVariantHandler` adds the variant explicitly (it was tracked as Modified and refused, HTTP 500); a duplicate SKU is now a 400 `BusinessRuleException`.
- **Shared**: `TrustedCallerKeys` is its own throttled key cache (min 2 s between fetches per caller): a token with an unknown key id triggers a fetch before it is judged, so the first call after a caller restarts is accepted. Receivers redeployed: product, warehouse, promotion, shipping, masterdata, identity.
- Tests: Architecture 68 (variant creation on the real context, fails on the old handler), Shared 103 (rotation, throttle, caller down while restarting). Live on `glcheck`: variant 0.8 kg created via API (201, stored for the tenant); Sales redeployed and orders placed immediately: all 201. Next: P1-11c fee table (`feat/p1-11c-fee-table`).

### 2026-10-28 — ROADMAP P1-11b: delivery fee (`feat/p1-11b-shipping-fee`, merged as PR #44)
- **Shipping**: `ShippingFeePolicy` + `ShippingFeeService`; `GET api/v1/shipping/fee` (preview, any signed-in user, Gateway route) and `internal/v1/shipping-fee` (service token `salesorder`); `IProductWeightClient` (service token `product`) fills the parcel weight; Shipping now publishes service JWKS and trusts `salesorder`.
- **Product**: `internal/v1/product-weights` (grams, kg/g/lb/oz converted; trusts `shipping`).
- **Sales** (migration **015**): `ShippingFee`, `Address.Ward`; `IShippingServiceClient` (fail closed); created event carries the final total, fee and discount; saga command carries the ward. **Promotion**: points on goods only.
- **Storefront**: ward field, fee at review, fee in the confirmation. **Admin**: ward on the order form, fee line on the order, fee settings explained.
- Tests: SHP 125, SO 70, PRM 11, Architecture 66; storefront `vue-tsc` and admin `tsc -b` clean. Live on `glcheck`: UAT S1…S6 in `ShippingFee.md`. Not done: P1-11c; risk register R34…R38; DEFERRED 4w.

### 2026-10-27 — ROADMAP P1-11: carriers (`feat/p1-11-carriers`, merged as PR #43)
- **Shipping** (migration **008**; 008 + Sales 014 added to the runner): `ICarrierProvider` (GHTK, GHN, Viettel Post, J&T placeholder) replaces the global `ICarrierClient`; `CarrierProviderResolver` (platform switch → shop switch → complete account; `TenantCarrierCredentials` encrypted); `CarrierBookingService` + `CarrierSelector` (rules, then cheapest quote, failover; failure never swallowed); `CarrierWebhookService` (anonymous, shared secret, idempotent) + `CarrierTrackingService` + `CarrierTrackingSyncJob`; cancel at the carrier (also on `SalesOrderCancelled`); `Shipment.ApplyCarrierStatus` (forward-only state machine); API: shipments list, cancel-at-carrier, rebook, carrier rules CRUD, parcel quote, carrier accounts under settings; the toy per-kg quote removed. Registration lives in `AddCarrierIntegration` (a DI test resolves it).
- **Sales** (migration **014**): the order records the recipient's phone; the saga's shipment command carries phone, payment method, order total, COD amount and line prices.
- **Gateway**: anonymous routes `shipping-webhooks/{carrier}`. **Admin**: Settings → Shipping gets Tenant (pickup address) / Carriers / Carrier rules tabs and a system switch per carrier; new Shipments page. **Storefront**: phone at checkout.
- Tests: SHP 112 (was 9), SO 61 (was 52); Architecture 59/59; admin `tsc -b` and storefront `vue-tsc` clean. Live on `glcheck` against a **fake carrier server** (the real carriers cannot reach this stack): UAT-S1…S10 in the BA doc. Bugs found by the live check: carrier registrations missing from Program (fixed + DI test).
- Not done (P1-11b): DEFERRED §4v; risk register R26…R32.

### 2026-10-27 — Risk clean-up before P1-11 (`chore/risk-cleanup-p1-6-10`, on top of the unmerged P1-10 branch)
- **Identity** (migrations **030**, **031**): `DefaultTenantRoles` seeds CUSTOMER / STAFF / USER for every new tenant (030 repairs existing ones); registration needs no tenant code, uses the e-mail as account name (display name separate), user name unique per tenant (031); `HubTokenResolver` reads the SignalR ticket from the Authorization header as well as the query (the admin bell / chat hub negotiate was 401 everywhere).
- **Payment**: consumes `SalesOrderCancelled` (pending payment closed; captured never refunded); list filters `provider`, `needsReview`. **Accounting**: `PaymentProcessed` idempotent without a ledger entry. **Sales**: a customer reads their own orders without a permission (404 for others).
- **Clients**: mobile search / user name / order list, storefront order list, coupon and product types fixed; storefront `uuid()` for plain-http hosts; admin Payments filters; `.env.example` + docs for the mobile tenant host.
- **Guards**: `ClientApiContractTests`, `DatabaseRunnerTests` (29 unlisted migrations added to the runner). All 16 unit projects green (Architecture 59/59 after removing the obsolete key from the local k8s secrets file).
- **Browser walk-through** (Playwright + Chrome, 102 admin routes, storefront sign-up / orders): findings R19, open items R20…R25 in DEFERRED §6. Deployed: identity, payment, accounting, salesorder, notification, chatbot, admin, storefront; Identity 030 and 031 applied live.

### 2026-10-27 — ROADMAP P1-10: online payments (`feat/p1-10-online-payments`, unmerged)
- **Payment** (migration **008**; Payment 006–008 added to the runner): `IPaymentProvider` (VNPay, **MoMo**, **ZaloPay**) replaces `IPaymentGateway`; `PaymentProviderResolver` picks the provider and the CURRENT shop's keys (`TenantGatewayCredentials`, encrypted; platform switches `MoMoAvailable` / `ZaloPayAvailable`; `CodEnabled`); `GatewayNotificationService` is the one path for browser returns and server notifications (shop from the reference → verify with its keys → amount check → idempotent apply); endpoints `payment-gateway/methods`, `orders/{id}/checkout`, `orders/{id}/status`, `callback/{provider}`, `ipn/{provider}` (old VNPay routes kept); `ShippingDeliveryConsumer` captures COD on `DeliveryConfirmed`; refunds go to the payment's own gateway; a cancelled payment that receives money is flagged for refund.
- **Sales** (migration **013**): `SalesOrder.PaymentMethod`; the saga sends method / currency / customer / order number and skips the payment wait for COD; compensation always asks Payment to close the pending payment.
- **Gateway**: anonymous routes for `payment-gateway/callback|ipn/{provider}`; the rest authenticated. PaymentService in Docker now has `Security__SettingsEncryptionKey`.
- **Admin / Storefront / Mobile**: gateway cards + COD switch + refund button; real payment step, redirect, result page; picker, order screen. The storefront and mobile order calls now use the shapes the backend really reads and returns (`lineItems`, `orderId`, `orderNo`).
- Tests: PAY 81 (was 12), SO 50, WH 44, PO 61 unchanged; Architecture 53/54 (only the known `NoSharedTokenKeyTests`; one stale allowlist entry removed); `LiveSchemaDriftTests` green; `tsc -b` (admin), `vue-tsc` (storefront), `tsc` (mobile) clean. Live on `glcheck`: UAT-O1…O16 (see `OnlinePayments.md`); real sandbox calls to MoMo and ZaloPay (create, refund), notifications simulated. Bugs found live and fixed: unknown method was a 500, same-second references collided, a ZaloPay key remembered wrongly (only a test-setup issue), late money for a cancelled order. Test data removed.
- Not done (P1-10b): DEFERRED §4u; risk register R11…R18.

### 2026-10-26 — ROADMAP P1-9: warehouse replenishment (`feat/p1-9-replenishment`, unmerged)
- **Warehouse** (migration **009**, added with 007–008 to the runner): `Inventory` MinStock / MaxStock / SafetyStock / LowStockAlertedAt; low stock = available ≤ minimum; `EvaluateLowStockAlert` (one alert per time below the minimum, a closing message when stock recovers, repeat after 24 h for the scan); `PUT inventory/{id}/stock-policy`, `POST inventory/replenishment/scan`, `ReplenishmentScanJob` (`Replenishment:ScanHour`, UTC, default 2); `StockAlertEvent` carries SKU, available, min / max / safety.
- **Purchasing** (migration **013**): `PurchaseRequest` becomes a proposal (code, available, on order, min / max / safety, preferred supplier, price, lead time, links; statuses Ordered / Quoting); need = `max − (available + on order)`, refreshed not duplicated, closed when covered; `SupplierItem.IsPreferred` (`PUT suppliers/{id}/items/{itemId}/preferred`); `POST purchase-requests/create-orders` (draft PO per preferred supplier, one RFQ for the rest, idempotent); the alert consumer retries 5 times. No new permission (`purchase_request:manage`, `supplier:update`).
- **Admin**: Stock policy card, Min / Max columns, Purchase requests page (columns, selection, Create orders, Scan stock now, links), Preferred tick on supplier items.
- Tests: WH 44 (was 26), PO 61 (was 43), ACC 579 unchanged; Architecture 53/54 (only the known `NoSharedTokenKeyTests`); `LiveSchemaDriftTests` green; `tsc -b` clean. Live on `glcheck`: policy validation, one request per product with the preferred supplier's price, no duplicate alert on a second receipt, scan inside 24 h sends 0, create orders → 1 draft PO (14,700) + 1 RFQ, second run skipped, goods on order stop a new request, stock recovery dismisses the open request. Test data removed.
- Not done (P1-9b): DEFERRED §4t; risk register DEFERRED §6.

### 2026-10-26 — ROADMAP P1-8: purchasing RFQ, supplier invoices, returns to vendor (`feat/p1-8-purchasing-rfq-match-returns`, unmerged)
- **Purchasing**: RFQ + quotations + comparison + award (→ draft PO); `SupplierInvoice` with a stored three-way match (tolerances per tenant, default 0 % / 2 %), approve / approve-with-variance / reject / match again; `VendorReturn` (create, ship, credit). Migration **Purchasing 012** (11 tables; 009–012 added to the runner), **Identity 029** (nine permissions: rfq, supplier_invoice, vendor_return, purchasing_settings).
- **Accounting** (migration **026**): no bill from the order approval any more; the bill comes from `SupplierInvoiceApproved` (VAT, due date and received value as invoiced); price variance key `PurchasePriceVariance`; debit note and `Bills.CreditedAmount` from `VendorReturnCredited`; movement `ReturnedToVendor` → Dr GRNI / Cr Inventory.
- **Warehouse**: `MovementType.ReturnedToVendor = 6`, `IssueVendorReturnCommand` (all or nothing, idempotent, 5 attempts).
- **Admin**: Purchasing → RFQs, Supplier invoices, Returns to vendor, Match settings; *Receive goods* on the purchase order.
- Tests: PO 43 (was 9), ACC 579, WH 26; `LiveSchemaDriftTests` green; `tsc -b` clean. Live on `glcheck`: RFQ award at the cheaper quote, no bill at order approval, partial receipts, an invoice matched and booked as a bill, an invoice 5 % over held and approved with a reason (variance 45 on 632), an over-invoice held and rejected, a return that took stock 21 → 19 with its entries, a credit note that lowered the bill by 180, tolerances changed and re-matched. A bug found live (settings row inserted twice in one request) fixed in the last commit. Test data removed.
- Not done (P1-8b): DEFERRED §4s.

### 2026-10-26 — ROADMAP P1-6: customer notifications and unconsumed events (`feat/p1-6-event-consumers`, unmerged)
- **Notification**: `SalesOrderCreated` now carries the customer e-mail and order number; `OrderRecipients` / `UserContacts` projections; messages for order created / paid / shipment prepared / shipped / delivered / cancelled / returned, payment failed / refunded, loyalty points, coupon use, case resolved (platform templates, migration **Notification 013**); `DedupKey` = one message per fact; templates render case-insensitively incl. the subject; consumers retry 5 times; opted-out customers skipped quietly.
- **Promotion**: points earned once per order, reversed on `SalesOrderCancelled` / `Returned`. **CRM**: `OpportunityWon` and `CaseResolved` carry the contact (and e-mail).
- **Reporting** (migration **Reporting 008**): dead `sales-order-*` Kafka consumer removed; `FactCrmOpportunities`, `FactPaymentRefunds` (PartiallyRefunded vs Refunded), pay slip fact with net pay and breakdown, returned orders (G13, AH7, G14 closed).
- **Admin**: Settings → *Sent notifications*, *Message templates* (read-only).
- Tests: NTF 19, PRM 8, RPT 7, CRM 4 (new projects), SO 33; Architecture 53/54 (known `NoSharedTokenKeyTests`); `LiveSchemaDriftTests` green; `tsc -b` clean. Live on `glcheck`: full order → delivery → return produced 7 messages to the customer, points earned and reversed, partial then full refund in Reporting, won deal and resolved case. Local stack has no SMTP: messages end Failed ("Failure sending mail.") after retries.
- Not done (P1-6b): DEFERRED §4r.

### 2026-10-26 — ROADMAP P1-7: real payroll (`feat/p1-7-real-payroll`, unmerged)
- **HRM**: `PayrollCalculator` (pure; insurance with caps, progressive PIT with personal / dependent deductions, proration, overtime); tables `EmployeeCompensations`, `Dependents`, `PayrollRateSets` + `PitBrackets` (date-effective, seeded DRAFT defaults per tenant on first use), `LeaveBalances` revived; pay slip breakdown columns. Migration **HRM 008** (add to the runner: done in `RunAllScripts.ps1`). API: `salary-profiles`, `employees/{id}/compensations|dependents`, `payroll-settings/rate-sets`, `leave-requests/balances`; `payroll/process` now returns `{created, skipped}`.
- **Leave**: working days (Mon–Fri), annual balance checked at submit and approve.
- **Accounting**: keys `SocialInsurancePayable` 3383, `HealthInsurancePayable` 3384, `UnemploymentInsurancePayable` 3389, `PitPayable` 3335, `EmployerInsuranceExpense` 6421 (no SQL migration: keys resolve from the chart template); `ForPaySlip` posts the split.
- **Admin**: Salary profiles, Payroll settings (new pages), pay slip detail with pay / cancel and the skipped list, leave balance in the leave form.
- Tests: HRM 42 (was 8), ACC 569 (was 564); `LiveSchemaDriftTests` green; `tsc -b` clean. Live on `glcheck`: hand-calculated slips (net 27,215,000 and 52,748,600) equal the system's, balanced entries with the split, pay / cancel / reversal, a rate set picked by date, leave refused over the balance. Test data removed.
- Not done (P1-7b): DEFERRED §4q. **Rates are DRAFT: an accountant must confirm them.**

### 2026-10-26 — ROADMAP P1-5: saga completion (`feat/p1-5-saga-completion`, unmerged)
- **Saga**: `Saga:*` options (Payment 15, Stock 10, Shipment 15 min; 3 retries; 60 s check); `SagaRecoveryHostedService` pushes a stuck step and then compensates (refund + cancel + release); approval never retried. `GET` / `POST orders/{id}/saga[/retry]`. No schema change.
- **Shipping**: `ShipmentCreationFailed` instead of dropping the command; one shipment per order; consumer retries 5 times. **Sales** consumes `ShipmentCreationFailed`, `ShipmentFailed` (recorded), `DeliveryConfirmed` (→ Delivered, reason "Delivered (carrier confirmation)").
- **Returns**: `SalesOrderReturned` lists the shipped lines; **Warehouse** restocks at the issue cost (movement `Returned` = 5, lot `RET-…`), **Accounting** books Dr Inventory / Cr COGS, **Payment** refunds once per return. Warehouse / Payment consumers use the 5-attempt model.
- **Found and fixed in the live check**: Warehouse never read Sales' cancelled / shipped events (required `CorrelationId` Sales does not send): reservations of cancelled orders were never released and shipped stock never issued.
- **Admin**: *Order processing* card on the order page (steps, waiting time, retries, error, retry button).
- Tests: SO 33, SHP 9, WH 22, PAY 12, ACC 564; Architecture 53/54 (known `NoSharedTokenKeyTests`); `tsc -b` clean. Live on `glcheck`: no carrier → saga Failed, order Cancelled, payment Refunded, reservation released; with a carrier → saga Completed, ship, carrier confirms → Delivered, return → stock 8 → 10 at cost 50, refund 200,000, Dr 1561 / Cr 632 100; unpaid order → retried, manual retry, timed out → Cancelled; approval not retried. Test data removed, October closed again, saga test timeouts removed (service rebuilt with defaults).
- Not done (P1-5b): DEFERRED §4p.

### 2026-10-26 — ROADMAP P1-4: foreign currency and line-level bank reconciliation (`feat/p1-4-fx-bank-reconciliation`, unmerged)
- **Foreign currency**: invoices / bills carry currency, rate and foreign gross (base currency per tenant, default VND, in *Settings → Tax*); the rate is typed and can be suggested from MasterData (new `internal/v1/exchange-rates/effective` route, service token Accounting → MasterData; compose and k8s `ServiceAuth` settings added). *Mark as paid* takes the payment rate and posts the realized gain / loss (keys FxGain / FxLoss).
- **Period-end revaluation** (`accounting/fx/revaluation`, preview + post, panel on *Fiscal periods*): unrealized difference of open foreign receivables / payables at the closing rates, dated the last day and reversed the next day.
- **Bank reconciliation by line**: `BankStatementLines`, CSV import, automatic / manual match to the bank's ledger lines, difference to the closing balance, fee / interest posted from a line (keys BankFee / BankInterestIncome), reconcile only at difference 0. `BankAccountsController` moved to attribute authorization (allowlist shrank by one).
- Migration Accounting 025; drift test passes; ACC 563 tests, Architecture 53/54 (known `NoSharedTokenKeyTests`), admin `tsc -b` clean. Verified live on `glcheck` (rates by SQL: the tenant admin lacks the currency permission): 1,000 USD invoice → 24,000,000; received at 24,500 → Cr 515 500,000; bill 500 USD paid at 23,500 → Cr 515 250,000; revaluation +840,000 posted 31 Oct and reversed 1 Nov (found and fixed a tenant error when reversing an unsaved entry); CSV of 4 lines → 2 auto-matched, fee Dr 6427 75,000, interest Cr 515 12,500, difference 0, reconciled. Test data removed, October closed again.
- Not done (P1-4b): DEFERRED §4o.

### 2026-10-26 — ROADMAP P1-3b: VAT lines, settlement, e-invoice kinds, posting gaps, XML (`feat/p1-3b-vat-lines-settlement`, unmerged)
- **VAT lines** on invoices and bills (rates 0 / 5 / 8 / 10 %, goods not subject to VAT; table `VatLines`, migration 024), *Adjust VAT* by lines, bill approval with the supplier's rate, return by the invoice's VAT share, listing rows per line, return row 26.
- **VAT settlement** (`accounting/vat/settlement`, `settle`): Dr VAT output / Cr VAT input for the smaller balance, once per period (reverse to redo).
- **E-invoice**: replacement (original → Replaced, optional new rate applied first), adjustment (signed amounts, any number), **auto-issue** switch for invoices from orders.
- **Unposted documents**: `postedToLedger` / warning on invoices and bills, `posting-gaps` list and repost, bar + ⚠ in the admin. **XML** of the return (HTKK-style draft).
- Verified live on `glcheck` (October 2026 closed → warning, gaps, repost after opening; mixed-lines invoice 1,940,000 / VAT 140,000 and bill; listing and return incl. row 26; settlement Dr 3331 / Cr 1331 140,000, second settle 400; e-invoice issue → replace at 8 % (invoice VAT 143,703.70) → adjust −100,000 / −8,000; XML head). October 2026 was reopened for the check and closed again, settings restored, test data removed. Tests: ACC 495 (+40), drift test passes (migration 024), Architecture 53/54 (known failure), admin `tsc -b` clean. Migration 024 applied to the host DB; accountingservice and admin rebuilt from the branch.
- Not done (P1-3c): see ROADMAP; the return/pro-rata and auto-issue event paths are unit-tested only, admin screens type-checked not clicked.

### 2026-10-25 — ROADMAP P1-3: VAT, VAT listing / return, e-invoice adapter (`feat/p1-3-vat-einvoice`, unmerged)
- **VAT** on invoices (net / VAT / gross from the tenant's rate and price convention, override, *Adjust VAT*), returns (VAT inside the refund) and bills (new key `VatInput`, rate fixed at approval). Rate 0 (default) = behaviour as before. Migration 023 (`Invoices` / `Bills`.`TaxRatePercent`; `SubTotal` / `TaxAmount` already existed).
- **Reports → VAT**: sales / purchase listing with the check against the VAT account; simplified return 01/GTGT (csv / xlsx / pdf).
- **E-invoice**: `IEInvoiceProvider` adapter, `EInvoices` table, issue / cancel / lookup, sandbox provider; provider and series in Settings → Tax; VAT locked while issued.
- Verified live on `glcheck` (invoice 1,100,000 → Dr 131 / Cr 5111 1,000,000 / Cr 3331 100,000; bill → Dr 3388 2,000,000 / Dr 1331 200,000 / Cr 331; listings reconciled; return csv / xlsx / pdf; adjust to 8 % = 18,518.52; e-invoice issue → repeat 400 → adjust 400 → cancel → re-issue); the fiscal period October 2026 was reopened for the check and closed again; tenant tax settings restored; test data removed. Tests: ACC 455 (+48), drift test passes, Architecture 53/54 (known failure), admin `tsc -b` clean. Migration 023 applied to the host DB; accountingservice and admin rebuilt from the branch.
- Not done (P1-3b): see ROADMAP; the live check did not cover a return (unit-tested), and the admin screens were type-checked, not clicked.

### 2026-10-25 — ROADMAP P1-2l: day count, month-end accrual job, note-text history, notes in ja / zh / fr (`feat/p1-2l-accrual-job-daycount-history-languages`, unmerged)
- **Day count per loan** (schedule / actual-365 / actual-360): interest from the outstanding principal per day; default interest paid = interest of the days to the repayment; `interestDueToday` per installment. Migration 022.
- **Month-end job** `LoanInterestAccrualJob`: opt-in per tenant (closing policy switch, default off), accrues to the end of the previous month, skips a closed period, idempotent.
- **History of note texts** (`NoteTextRevisions`, `GET reports/notes/text/history`, History link in the editor).
- **Notes in Japanese, Chinese, French** (wording, section titles / hints, texts per language); PDF for fr, 400 for ja / zh (no CJK font). Translations are drafts.
- Verified live on `glcheck`: actual/360 accrual 5,000 + 0 + 5,000 and repayment Dr 341 1,000,000 / Dr 335 10,000 / Cr bank 1,010,000; job off → nothing, on → Dr 635 / Cr 335 5,800 dated 30 Sep; 3 text revisions; fr / ja / zh notes, PDF fr 200, ja 400, xlsx ja 200; test data removed, policy switched back off. Tests: ACC 407 (+36), drift test passes, Architecture 53/54 (known failure), admin `tsc -b` clean. Migration 022 applied to the host DB; `RunAllScripts.ps1` now also lists 021. accountingservice and admin rebuilt from the branch.
- Not done (P1-2m): rate changes, PDF for ja / zh, reviewed translations, browser click-through of the new admin pieces.

### 2026-10-25 — ROADMAP P1-2k: loan interest and receiving account, B03 / notes mapping, versioned note texts (`feat/p1-2k-loan-interest-mapping-versions`, unmerged)
- **Loans**: optional receiving account per loan (drawdown and repayments use it, else the Bank key); **accrued interest** (`accrue-interest` by calendar days of each installment's period, Dr 635 / Cr 335; the repayment clears it and books the rest, with the interest actually paid); cancel refused after accrual; interest check in the reconciliation. Migration 020, key InterestPayable.
- **Mapping** of a tenant's own chart now also feeds B03 direct, B03 indirect and the B09 tables (screen: statement select; side hidden where it does not apply).
- **Note texts per fiscal year** (migration 021): the report reads the text of its year, else the latest earlier year, else year 0; the editor has a year field and marks inherited texts.
- Verified live on `glcheck` (loan on account 1112, accrual 45 + 18, repayment Dr 341 1,000 / Dr 335 63 / Dr 635 37 / Cr 1112 1,100, cancel refused; own accounts in B03 and VI.2; 2026 → 2027 text inheritance); test data removed. Tests: ACC 371 (+32), drift test passes, admin `tsc -b` clean. Migrations 020 – 021 applied to the host DB.
- Not done (P1-2l): automatic monthly accrual, actual/360, history of text edits, other languages, browser click-through of the new admin screens.

### 2026-10-25 — ROADMAP P1-2j: loan posting, payment history, own-chart mapping, notes in English (`feat/p1-2j-loans-history-mapping-english`, unmerged)
- **Loans** post the drawdown (Dr bank / Cr 341, 2100 on the 14-account chart) and each repayment (principal + scheduled interest, Cr bank); option "already booked by hand" keeps older loans out; *Post drawdown* for an older loan without repayments; cancel reverses; `reports/loans-reconciliation` and a card on the Loans screen. Migration 016, keys LoanPayable / InterestExpense.
- **Payment history**: `PaymentApplications` (+ `Bills.ApprovedOn`, `Invoices.CancelledOn`, backfilled and flagged estimated) make the B01 / B09 split of trade receivables / payables exact for past dates; the "approximate" note only appears for estimated or missing dates. Migration 017.
- **Account mapping** for a tenant's own chart on B01 / B02 (`accounting/statement-mappings`, Reports → Account mapping). Migration 018.
- **Notes B09**: `lang=vi|en` with texts per language (migration 019), non-cash table from the journal, prior-period column; admin selectors.
- Verified live on `glcheck` (drawdown / repayment entries, reconciliation, past-date split, mapping 9001 → row 111 balances 270 = 440, English notes pdf / xlsx / csv); test data removed. Tests: ACC 339 (+38), Architecture drift test passes, admin `tsc -b` clean. Migrations 016 – 019 applied to the host DB.
- Not done (P1-2k): accrued interest, receiving account per loan, mapping for B03 / notes, versioned texts, other languages, browser click-through of the new admin screens.

### 2026-10-25 — ROADMAP P1-2i: 14-account layouts, off-balance accounts, loan ledger, B09 notes (`feat/notes-loans-offbalance`, unmerged)
- B01 / B02 / B03 (direct, indirect) and the notes choose their layout from the tenant's accounts (`ChartTemplates.Detect`): Simple14 layouts added. Found and fixed on the demo tenant: 421b took account 3100 twice.
- Off-balance: account type 6, accounts 001 – 008 in the Vietnamese template, register `OffBalanceEntries` (migration 013), `accounting/off-balance`, report `reports/off-balance`; journal and posting refuse class 0.
- Loan ledger: `Loans` / `LoanInstallments` (014), `accounting/loans`; B01 rows 320 / 338 split borrowings by the schedule, exact for past dates, capped by 341. `MaturityLedger` shared by B01 and the notes.
- B09: `NoteTexts` (015), `statements/notes` (28 sections, tables from the books, loans, off-balance), `notes/text`, PDF / Excel / CSV. Admin: Loans page, Off-balance page, report tabs Off-balance and B09 (with the text editor).
- Verified live on `glcheck` and SYSTEM; throw-away loan / off-balance / invoice data removed (the 001 test account is inactive, the profile is back to VND, note texts saved for the check remain). Tests: ACC 301 (+47), Architecture 53/54 (known), admin `tsc -b` clean. Migrations 013 – 015 applied to the host DB.
- Not done (P1-2j): posting link for loans, payment history, own-chart layouts, notes in English.

### 2026-10-25 — ROADMAP P1-2h: B01 maturity split (`feat/b01-maturity`, unmerged)
- B01 splits trade receivables (131 → rows 131 / 211, new group 210) and payables (331 → rows 311 / 331) by the due date of open invoices and approved bills (`MaturitySplit`, 12-month rule, capped by the ledger, totals unchanged); note on past dates. `StatementEngine` got a `StatementMaturity` input; the B01 handler reads the open documents. No schema change.
- Verified live on `glcheck` with throw-away invoices / bills / a payables entry: 131 1,500 → 600 + 211 900; 311 1,000 + 331 400 (as of 30 Nov); 270 = 440; pdf / xlsx / csv 200; test data removed. Tests: ACC 255 (12 new), Architecture 53/54 (known), no frontend change.
- Not done (P1-2i): B09, off-balance accounts, loan ledger, other charts.

### 2026-10-25 — ROADMAP P1-2g: B03 by the indirect method (`feat/cashflow-indirect`, merged)
- `statements/cash-flow?method=indirect` (`IndirectCashFlowLayouts` / `IndirectCashFlowEngine`, layout B03-DN-GT): profit before tax, non-cash add-backs, working-capital changes from balance changes, interest / tax paid and investing / financing from the direct cash rows; indirect 20 reconciled with direct 20 (difference reported, never forced). Admin: Method selector on the B03 tab. No schema change.
- Verified live on `glcheck`: indirect 01 = 1,300, 09 = −1,500, 20 = −200 = direct; 70 = 300; `method=foo` → 400; pdf / xlsx 200. Tests: ACC 243 (16 new, incl. a consistent-ledger reconciliation), Architecture 53/54 (known), admin `tsc -b` clean.
- Not done (P1-2h): B09, off-balance accounts, maturity split, other charts.

### 2026-10-25 — ROADMAP P1-2f: PDF export of B01 / B02 / B03 (`feat/pdf-export`, merged)
- `format=pdf` on `statements/balance-sheet|income-statement|cash-flow` (`PdfWriter`, PDFsharp-MigraDoc 6 MIT; Noto Sans embedded via `PdfFonts`, OFL licence in `Reports/Fonts`); other reports → 400. Admin: "Export PDF" on the B01 / B02 / B03 tabs. No schema change.
- Master (with P1-2e) was deployed first: all 18 services rebuilt one at a time (parallel builds made Docker Desktop drop the connection); then accounting + admin rebuilt for this branch.
- Verified live on `glcheck`: three PDFs (76 / 62 / 65 KB) with correct Vietnamese, B01 on 2 pages, fonts embedded, signatories printed; 400 on trial-balance / flat cash-flow. Tests: ACC 227, Architecture 53/54 (known `NoSharedTokenKeyTests`), admin `tsc -b` clean.
- Not done (P1-2g): indirect method, B09, off-balance accounts, maturity split, other charts.

### 2026-10-24 — ROADMAP P1-2e: amount unit and company header (`feat/report-profile`, merged)
- Table `ReportProfiles` (migration `012_ReportProfile.sql`, applied to the local DB), `GET/PUT accounting/reports/profile` (write needs `accounting:accounts:manage`): company name, tax code, address, three signatories, default unit.
- B01 / B02 / B03 take `unit=vnd|thousand|million` (else the profile's unit, else VND): rows rounded half away from zero, totals added from the rounded rows, integrity checks on the exact amounts with a rounding-difference note; DTO has `unit`, `unitDivisor`, `header`.
  CSV / Excel print the company block above and the signatories below. Admin: unit selector, report-profile panel, header on screen.
- Verified live on `glcheck`: profile saved with Vietnamese text; B01 in thousand shows assets 4 vs sources 3 with `isBalanced` true and the note; `unit=vnd` exact; `unit=foo` → 400; CSV starts with the company block and ends with the signatories; xlsx 200.
  Tests: ACC 216, Architecture 53/54 (known `NoSharedTokenKeyTests`), admin `tsc -b` clean. BA: `BA-Accounting.md` (UAT-40 … 45).
- Not done (P1-2f): indirect method, B09, off-balance accounts, maturity split, PDF, other charts.

### 2026-10-24 — ROADMAP P1-2d: B03, closing policy, Excel (`feat/b03-yearend-xlsx`, merged)
- `statements/cash-flow` (B03-DN, direct method, rows by mã số via `CashFlowLayouts` / `CashFlowStatementEngine`, movement placed by counter-account prefix + direction, `unmapped` + closing-cash check, prior column). Draft layout.
- Closing policy: table `ClosingPolicies` (migration `011_ClosingPolicy.sql`, applied to the local DB), `GET/PUT fiscal-periods/closing-policy`; YearEnd = months only lock, the last period of the fiscal year writes one entry
  from the remaining cumulative balances. Switching allowed only between years.
- `format=xlsx` on every report (ClosedXML; `ReportTables` shared with CSV). Admin: B03 tab, "Export Excel", closing-policy panel on Fiscal Periods.
- Verified live on `glcheck`: B03 October 70 = 300 = cash-flow net change; YearEnd Oct + Nov lock without entry, December writes one entry (Dr 5111 2,400 / Cr expenses 600 / Cr 4212 1,800); January 2027 opens P&L accounts at 0; xlsx downloads.
  Tests: ACC 190, Architecture 53/54 (only the known `NoSharedTokenKeyTests`), admin `tsc -b` clean. BA document: `docs/business/Accounting/BA-Accounting.md` (UAT-25 … 39).
- Not done (P1-2e): indirect method, B09, off-balance accounts, maturity split, PDF, other charts, rounding.

### 2026-10-24 — ROADMAP P1-2c: B01 / B02 by indicator code (`feat/vas-statements`, unmerged)
- `statements/balance-sheet` (B01-DN) and `statements/income-statement` (B02-DN): rows by mã số built from account-prefix groups and formulas (`StatementLayouts`, `StatementEngine`), contra accounts
  signed by their natural side, 131 / 331 split by balance side, not-yet-closed result in row 421b, prior-period column, `unmapped` accounts + `isBalanced`. **Draft layout (Vas99 only) — accountant review needed.**
  The Vas99 template gained 2291–2294 and 2147. No schema change, no migration.
- `format=csv` on trial-balance/full, account-ledger (all rows), cash-flow and both statements (UTF-8 BOM, RFC 4180, formula-injection safe). Admin: tabs B01 / B02 with comparison, "Export CSV" buttons.
- Verified live (glcheck): B02 October 1900 / 1300 = the flat profit-and-loss; B01 270 = 440 = 3300 after closing (421b = 1300), prior column filled; CSV starts with the BOM; SYSTEM (14 accounts) shows everything unmapped.
- Not done (P1-2d, listed in DEFERRED-ISSUES): B03 by code / indirect method, B09, off-balance accounts, year-end closing, maturity split, Excel / PDF. Tests: ACC 164, admin `tsc -b` clean.

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
