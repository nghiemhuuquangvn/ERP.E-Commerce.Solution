# Roadmap: Platform-Wide "Common Service" Candidates

## Purpose

IdentityService was successfully turned into a standalone, reusable service (see
[../business/Identity/CommonServiceRoadmap.md](../business/Identity/CommonServiceRoadmap.md))
via two mechanisms: (1) restructuring `ERP.Shared` from one monolithic project into
per-concern sub-projects so a service's actual dependency surface becomes explicit, and
(2) a repeatable export script that copies the service + its specific Shared
dependencies, strips ERP-specific content, and packages self-contained Docker + docs.

This document extends that lens across the other 13 services: which of them are
genuinely generic, domain-agnostic infrastructure (reusable in an unrelated project the
way an auth service is) vs. which ARE this ERP's actual business domain — not "common
services," they're the product itself. Six services (Notification, SharedMasterData,
ChatBot, Payment, Reporting, Product) were surveyed directly (code, `.csproj`
dependency graphs, DB seed data, READMEs); the remaining 7 (SaleOrder, PurchaseOrder,
Warehouse, Accounting, CRM, HRM, Shipping, POS, Promotion) are assessed via
[domain-map.md](domain-map.md) and [dependency-map.md](dependency-map.md)'s dependency
graphs, which already establish they're deeply coupled into the order-to-cash /
procure-to-pay pipelines specific to this ERP.

**Status (2026-08):** Analysis complete. NotificationService packaging (N1-N4) done
and isolated-build-verified; Docker-level runtime verification blocked on local
resource constraints — see status table and Tier 1 item 1 below.

## Status table

| # | Service | Tier | Status |
|---|---------|------|--------|
| 1 | NotificationService (NTF) | 1 — strong | 🔨 Packaged + isolated-build-verified; Docker runtime check pending |
| 2 | SharedMasterDataService (MST) | 1 — moderate | ⏳ Not started |
| 3 | ChatBotService (CHB) | 1 — moderate | ⏳ Not started |
| 4 | PaymentService (PAY) | 2 — needs refactor first | ⏳ Not started |
| 5 | ReportingService (RPT) | 2 — needs refactor first | ⏳ Not started |
| — | ProductService (PRD) | Not a candidate | Excluded (see below) |
| — | SaleOrder/PurchaseOrder/Warehouse/Accounting/CRM/HRM/Shipping/POS/Promotion | Not candidates | Excluded (see below) |

---

## Tier 1 — strong/moderate candidates, package using the Identity pattern

### 1. NotificationService (NTF) — strongest candidate, comparable to Identity

**Why it's a good fit:** the Domain layer (`Notification`/`NotificationTemplate`/
`NotificationPreference`/`NotificationLog`, `Channel`/`Priority` enums) is fully
generic — zero ERP entities leak into it. There are no synchronous REST calls out to
any other service at all; the only outbound HTTP is to external delivery providers
(Twilio, FCM, a generic webhook sender). `NotificationDB`'s seed data uses one fixed
system tenant GUID with no cross-service FK assumptions. (Correction from the
original survey: Kafka/RabbitMQ consumption isn't as unwired as first thought —
`IdentityEventConsumer`, `NotificationKafkaConsumer` (payment-topic, shipping-topic,
mst-topic), and `SalesOrderRabbitMqConsumer` are all actually registered in
`Program.cs`. Only the PurchaseOrder/HRM approval-chain handlers were genuinely
unwired — no consumer for `purchasing-topic`/`hrm-topic` exists.)

**Status: packaging done (N1-N4), Docker-level runtime verification pending.** Full
step-by-step technical detail — including what each trimmed/replaced file's own code
comments explain and why — is written up in
[../business/Notification/CommonServiceRoadmap.md](../business/Notification/CommonServiceRoadmap.md);
this section is a summary of that document.

- **N1** ✅ Done, verified. `ERP.NTF.Domain/Application/Infrastructure/API`
  retargeted off the monolithic `Shared/ERP.Shared.csproj` onto the specific
  per-concern sub-projects each layer actually uses (determined by grepping every
  `using ERP.Shared.*`, not guessed): Domain → `Core`; Infrastructure → `Core`,
  `Messaging`, `Contracts`, `Caching`, `Security`; API → `Authorization`,
  `Extensions`, `Caching`, `Localization`, `Messaging`, `Security`, `Versioning`
  (same 10-project surface as IdentityService's own export, since both services'
  usage patterns turned out similar). Full `NotificationService.sln` rebuild: 0
  warnings/errors, umbrella no longer in the dependency tree.
- **N2-N4** ✅ Done. `deployment/scripts/export-notification-service.ps1` (mirrors
  `export-identity-service.ps1`'s structure): copies the 4 NTF projects + the same
  10 Shared sub-projects Identity's export uses; trims `ModuleCodes.cs`/
  `LicenseEnforcementMiddleware.cs` from the copied `Authorization` (tied to this
  platform's tenant-license model — unlike Identity's export, this one *keeps*
  `AuthenticationExtensions.cs`/`HubTicketStore.cs`/`UserContextHeaderMiddleware.cs`
  since NotificationService actually uses them, being a token *validator* not
  *issuer*); replaces `PermissionConstants.cs` with a 6-constant version (this
  service's own 4 codes + a `Sample_*` pair); replaces `IntegrationEventHandlers.cs`'s
  ~13 ERP-specific event/handler pairs and all 3 ERP-topic-specific consumers with
  one generic `ExampleIntegrationEvent`/`ExampleKafkaConsumer` pair demonstrating the
  same wiring pattern against a placeholder topic; drops the now-unused
  `RabbitMQ.Client` package reference; excludes `004_SeedDemoData.sql` (IdentityService
  customer-GUID-formula-keyed) and `005_SeedWorkflowTemplates.sql` (PO/Leave-specific),
  replacing the latter with a generic `PASSWORD_RESET` example; ships a self-contained
  Dockerfile/docker-compose.yml (own SQL Server + Redis, Kafka optional via
  `--profile with-kafka`) and a from-scratch README for external consumers, including
  an explicit "this service validates tokens, it does not issue them" callout.
  Negative-check grep found only prose (code comments explaining exclusions/patterns,
  same as Identity's own export) — no functional leaks.
- **Isolated build** ✅ Done, verified: ran the export, `dotnet build` on the output
  from a location with zero path back to the monorepo — 0 warnings/0 errors.
- **Docker-level runtime verification** ⏳ **Blocked, not yet done.** Attempted
  `docker compose -p ntf-verify build` against the local Docker Desktop engine (which
  also hosts this environment's live K8s stack — 14+ pods, per
  [port-map.md](port-map.md)) and it left the engine returning `500 Internal Server
  Error` on every API call, including plain `docker version`, for an extended period.
  Root cause: only ~1.7 GB of 16 GB system RAM was free at the time — the live K8s
  stack was already consuming nearly everything, and stacking a second full
  SQL-Server-plus-app Compose stack on top pushed the engine into a bad state. An
  orphaned `docker compose build`/`buildx bake` process tree (left running after the
  attempt was cancelled) was found and killed, but the engine had not recovered by
  the time this was written up. Deliberately not pursued further to avoid
  destabilizing the live K8s environment — needs either more free memory (e.g. the
  live stack scaled down temporarily) or a Docker Desktop restart, both of which are
  the user's call, not something to do unprompted. The isolated `dotnet build` pass
  above is real signal that the export is structurally sound; it just hasn't had the
  same full boot-and-functional-round-trip pass Identity's export got.

### 2. SharedMasterDataService (MST) — moderate candidate

**Why it's a good fit:** the Geography (Country→Province→District→Ward) +
UnitOfMeasure + SupportedLanguage/Translation slice is excellent generic
reference-data material — almost every multi-tenant B2B app needs some version of
this. It's also already ahead of the pack structurally: its Domain layer references
only the split `Shared.Core` project, not the umbrella.

**Biggest blocker:** the `Department`/`Position`/`Employee` aggregates are real HR
business data, explicitly "moved here from HRMService" per the service's own README —
this directly contradicts the "generic reference data" framing the rest of the service
earns. Recommended approach when this service's turn comes: split Employee/
Department/Position out of what ships entirely (same exclusion pattern Identity's
export used for ChatBot-specific migrations, see §7 there), package only
Geography/UoM/Language as the "common" module. A service still wanting the HR slice
would need a separate decision, not bundled by default.

**Other blockers:** Kafka coupling to IdentityService's `UserRegistered` event (for
auto-hiring employees) is decouplable by simply not registering that consumer in a
standalone build, same pattern as Notification's unwired consumers.

### 3. ChatBotService (CHB) — good candidate, moderate effort

**Why it's a good fit:** the AI orchestration core is already cleanly separated from
ERP-specific content — `OpenAiChatCompletionProvider` (LLM abstraction),
`ChatHub`/`SignalRChatStreamNotifier` (streaming), and the tool-calling loop in
`SendMessageCommandHandler` have no ERP-specific logic. Tools are pluggable via a
clean `IErpTool` interface (despite the name), so swapping them is a real, supported
mechanism, not a hack.

**Blockers to extraction:**
- The 7 built-in tools (`GetMyOrdersTool`, `GetOrderByIdTool`, `GetProductInfoTool`,
  `GetInventoryAvailabilityTool`, `GetShipmentTrackingByOrderTool`,
  `GetActivePromotionsTool`, plus the CRM-escalation client) are entirely ERP-domain —
  thin REST wrappers into Sales/Product/Warehouse/Shipping/Promotion/CRM. These need
  stripping/replacing with 1-2 generic example tools (e.g. a trivial "get current
  time" or "echo" tool) demonstrating the `IErpTool` contract without asserting ERP
  domain knowledge.
- License/module-gating (`ModuleCodes.ChatBot`, `TenantLicense`) is wired to
  IdentityService's licensing model — needs to either ship as an optional
  integration or be stripped in favor of a simple on/off config flag.
- Still on the monolithic `Shared/ERP.Shared.csproj`, same retargeting work as
  Notification.

---

## Tier 2 — plausible candidates, but need internal refactor before extraction is worthwhile

Not started; flagged for a future decision on whether the refactor investment is
worth it before packaging effort begins.

### 4. PaymentService (PAY)

The gateway integration itself (VNPay transaction/refund/settlement processing) is
reasonably generic. But two event consumers —
`InvoiceCreatedConsumer` (Kafka `accounting-topic`) and `SalesOrderRabbitMqConsumer`
(RabbitMQ `sales_order_events`, implementing `OrderFulfillmentSaga` commands like
`InitiatePaymentCommand`/`RefundPaymentCommand`) — are deeply hard-wired to this
ERP's specific topic names and saga contract. The `Payment` aggregate itself bakes in
non-nullable `OrderId`/nullable `InvoiceId`/`SettlementId` rather than a generic
external reference concept. Extracting this service as-is would ship a payment
service that only makes sense wired to this ERP's saga — real refactor work
(decoupling the consumers, genericizing the reference-id concept) is needed before
packaging is worthwhile, not a lift-and-shift like Notification. The service's own
README is also stale (describes Stripe/PayPal and a different schema than the actual
VNPay-based implementation) and would need rewriting regardless of the extraction
decision.

### 5. ReportingService (RPT)

The underlying idea — an event-sourced OLAP read-model engine — is sound, and a
partial generic skeleton already exists (`ReportingConsumerBase`, a reflection-free
`JDoc` JSON helper, `DateKeyHelper`). But it's inconsistently used: 6 of the
fact/dimension consumers (`SalesOrderEventConsumer`, `PaymentEventConsumer`,
`InventoryEventConsumer`, `POSEventConsumer`, `HRMEventConsumer`,
`ProductEventConsumer`) are hand-written classes with hardcoded topic names and
hardcoded field mappings to this ERP's specific event shapes — there's no
config-driven "register a topic → fact table mapping" engine, just one bespoke class
per ERP module. The service's own README describes a much richer, more
generic-sounding model (SCD Type-2 dimensions, 13-service coverage) that doesn't
actually exist in code — only 7 fact/dim types and 6 of ~13 services are wired.
Extraction would mean finishing the engine/schema split that was apparently started
but never completed, not packaging what's there today.

---

## Not candidates — this IS the ERP's business domain, not shared infrastructure

**SaleOrderService, PurchaseOrderService, WarehouseService, AccountingService,
CRMService, HRMService, ShippingService, POSService, PromotionService** — confirmed
via [dependency-map.md](dependency-map.md)'s full dependency matrix: these carry deep
synchronous + event coupling into the order-to-cash and procure-to-pay pipelines that
define this specific ERP. They aren't infrastructure another, unrelated project would
drop in wholesale — they're the reason this ERP exists. (CRMService is the one
arguable edge case — standalone CRM SaaS products do exist as a category — but that
would be a distinct future product decision, not a natural extension of this
initiative, and isn't pursued here.)

**ProductService (PRD)** was explicitly surveyed as a contrast case rather than
assumed. Its core catalog (Product/Variant/Category/Image/PriceList) is reasonably
generic on its own — a standalone storefront could plausibly reuse most of that
schema. What disqualifies it is that it's wired for ERP interop at every seam: a
`WarehouseRabbitMqConsumer`-fed `StockQuantity` cache assumes a Warehouse service
exists, `ProductUomConversion` is a warehouse/ERP concept, and the SKU↔ProductId
contract is explicitly designed to serve 5 sibling services per the service's own
README. Extracting it means either dragging that sync code along (defeating the
point) or nontrivial surgery to remove it. Excluded from this initiative; could be
revisited later as its own, larger effort if there's ever a concrete need for a
standalone product catalog service.

## Related

- [../business/Identity/CommonServiceRoadmap.md](../business/Identity/CommonServiceRoadmap.md) —
  the proven pattern (Shared restructuring + export script) this roadmap applies to
  the rest of the platform, §7 in particular for the exact mechanism.
- [../business/Notification/CommonServiceRoadmap.md](../business/Notification/CommonServiceRoadmap.md) —
  full step-by-step technical detail for Tier 1 item 1's packaging work (what each
  trimmed/replaced file's code comments explain, the export script's numbered
  steps, current Docker-verification blocker).
- [domain-map.md](domain-map.md) — domain groupings and per-domain event flows used to
  assess the "not candidates" services.
- [dependency-map.md](dependency-map.md) — the full service-to-service dependency
  matrix used to confirm coupling depth for both candidates and non-candidates.
