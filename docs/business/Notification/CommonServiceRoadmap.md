# Roadmap: NotificationService as a Reusable "Common" Service

**Service:** NotificationService (Port 5013, Code: NTF)

## Purpose

Tracks the work to make NotificationService reusable as a standalone notification
service for future, unrelated projects — not just this ERP. This is Tier 1, item 1
of [docs/architecture/common-services-roadmap.md](../../architecture/common-services-roadmap.md),
which surveyed 6 services and ranked NotificationService the strongest candidate
after IdentityService (see [../Identity/CommonServiceRoadmap.md](../Identity/CommonServiceRoadmap.md)
for that earlier, already-completed effort — this document follows the exact same
two-part mechanism: (1) restructure this service's dependency on `ERP.Shared` into an
explicit, minimal surface, and (2) build a repeatable export script that produces a
standalone, droppable copy).

**Status (2026-08):** Packaging done (N1-N4). Verified via an isolated build (0
warnings/errors, no path back to the monorepo). **Docker-level runtime verification
(compose build/up, migrations, functional round-trip) is blocked**, not skipped by
choice — see "Verification" below for why, and what's needed to finish it.

---

## N1 — Retarget the service's own `.csproj` files off the `ERP.Shared` umbrella ✅ Done, verified

**Why this step exists:** IdentityService's own packaging effort (§7 of its roadmap)
already restructured `src/Shared/ERP.Shared.csproj` — originally one monolithic
project — into 13 per-concern sub-projects (`ERP.Shared.Core`, `.Messaging`,
`.Contracts`, `.Authorization`, `.Caching`, `.Exceptions`, `.Data`, `.Localization`,
`.Middleware`, `.Observability`, `.Extensions`, `.Security`, `.Versioning`), while
`ERP.Shared.csproj` itself became a zero-code umbrella (`<Compile Remove="**\*.cs" />`
+ a `ProjectReference` to all 13) so the other services didn't need to change. That
restructuring made it *possible* for any one service to reference only what it
actually needs — but NotificationService itself still referenced the umbrella, so its
real dependency surface on `ERP.Shared` was implicit ("everything") rather than an
explicit, checkable list. N1 is that retargeting, done the same way Identity's own
`.csproj` files were retargeted.

**How the actual dependency surface was determined:** not guessed — every
`using ERP.Shared.*` statement in the service's 4 projects was greped and mapped to
the specific sub-project that namespace lives in:

| Project | `ERP.Shared.*` namespaces actually used | Sub-projects referenced |
|---|---|---|
| `ERP.NTF.Domain` | `ERP.Shared.Core.Domain` (every entity extends `BaseEntity`/`AggregateRoot`) | `Core` |
| `ERP.NTF.Application` | none | *(no change — it only ever referenced `ERP.NTF.Domain`)* |
| `ERP.NTF.Infrastructure` | `ERP.Shared.Core.Domain`, `ERP.Shared.Messaging` (`IMessageBus`, Outbox), `ERP.Shared.Contracts` (`SettingsChangedIntegrationEvent`), `ERP.Shared.Infrastructure.Caching`, `ERP.Shared.Security` (`ISecretProtector`/`AesSecretProtector`) | `Core`, `Messaging`, `Contracts`, `Caching`, `Security` |
| `ERP.NTF.API` | `ERP.Shared.Authorization`, `ERP.Shared.Extensions`, `ERP.Shared.Infrastructure.Caching`, `ERP.Shared.Localization`, `ERP.Shared.Messaging`, `ERP.Shared.Security`, `ERP.Shared.Versioning` | `Authorization`, `Extensions`, `Caching`, `Localization`, `Messaging`, `Security`, `Versioning` |

Each `<ProjectReference Include="..\..\..\..\Shared\ERP.Shared.csproj" />` in
`ERP.NTF.Domain.csproj`/`ERP.NTF.Infrastructure.csproj`/`ERP.NTF.API.csproj` was
replaced with the specific list above (e.g. Infrastructure's single umbrella
reference became 5 separate `<ProjectReference>` lines). `ERP.Shared.Extensions`
itself has its own internal `ProjectReference` to `Middleware` and `Observability`
(needed to compile, even though NotificationService's own code never calls into
those two directly), so those two ride along transitively — this is expected and
matches how Identity's own retargeting worked out.

**Verified:** `dotnet build src/services/NotificationService/NotificationService.sln`
— 0 warnings, 0 errors, and the umbrella `ERP.Shared.csproj` no longer appears
anywhere in the build's dependency-resolution output (confirming the retargeting
actually took effect, not just compiled by coincidence via a stale reference).

---

## N2 — Decide what ships vs. what's ERP-specific coupling ✅ Done (decisions below; mechanized in N4)

Unlike N1 (a monorepo-source change, safe because it's behavior-preserving), N2's
decisions are **not** applied to the monorepo source at all — following the same
principle Identity's own §7 (Part B3) established: *"change nothing in the monorepo;
the export ships a generic replacement instead."* The reasoning: this ERP's live
NotificationService genuinely needs its PO-approval/Leave-request templates and its
payment/shipping/sales-order consumers to keep working for the other 13 services in
this monorepo — editing them in place would break this ERP, not just clean up the
export. All of N2's decisions below are implemented as template-overlay files under
`deployment/scripts/export-templates/notification/`, applied by the N4 script at
export time.

### What's genuinely generic and ships as-is

The Domain layer (`Notification`, `NotificationTemplate`, `NotificationPreference`,
`NotificationLog`, `WebhookEndpoint`, the System/Tenant Email/Sms/Notification
Settings entities), the full Application layer (commands, handlers, validators —
these reference generic types like `SendNotificationCommand`, not ERP concepts), and
almost all of Infrastructure (repositories, `SmtpEmailSender`/`TwilioSmsSender`/
`FcmPushSender`/null stubs, `SignalRRealtimeNotifier`, `OutboxProcessor`,
`WebhookSender`) ship completely unmodified.

### What's ERP-specific and gets replaced

1. **`IntegrationEventHandlers.cs`** (`ERP.NTF.Application/EventHandlers/`) — defines
   ~13 event classes + `INotificationHandler<T>` implementations, one pair per ERP
   event this platform's other services publish: `UserCreatedIntegrationEvent`,
   `SalesOrderCreatedIntegrationEvent`, `PaymentProcessedIntegrationEvent`,
   `ShipmentCreatedIntegrationEvent`, `EmployeeHiredIntegrationEvent`,
   `TwoFactorCodeGeneratedIntegrationEvent`,
   `PurchaseOrderPendingFinanceApprovalIntegrationEvent`,
   `PurchaseOrderApprovedIntegrationEvent`, `LeaveApprovedIntegrationEvent`,
   `LeaveRejectedIntegrationEvent`, `RoleAssignedIntegrationEvent`,
   `PermissionGrantedIntegrationEvent`. Every one of these assumes a sibling service
   (SalesOrderService, PaymentService, IdentityService, HRMService, ...) exists and
   publishes that exact shape — none of that is true in a standalone deployment.
   **Decision:** replace the whole file with one generic pair,
   `ExampleIntegrationEvent`/`ExampleIntegrationEventHandler`, that demonstrates the
   same mechanism (an `INotification` record + a handler that builds and sends a
   `SendNotificationCommand`) without asserting any ERP domain knowledge.

2. **Three Kafka/RabbitMQ consumers** (`ERP.NTF.Infrastructure/BackgroundJobs/`) —
   `IdentityEventConsumer` (Kafka `identity-topic`, dispatches by matching a `$type`
   JSON discriminator substring against IdentityService's own domain-event class
   names), `NotificationKafkaConsumer` (Kafka, `payment-topic`/`shipping-topic`/
   `mst-topic`), `SalesOrderRabbitMqConsumer` (RabbitMQ exchange
   `sales_order_events`). All three are registered as real `IHostedService`s in
   `Program.cs` — genuinely wired and running in this ERP, not dead code, which is
   exactly why they can't just be deleted from the monorepo. But every one of them
   hardcodes a topic/exchange name and a payload shape belonging to a specific
   sibling service. **Decision:** replace all three with one generic
   `ExampleKafkaConsumer` (`IHostedService`, subscribes to a placeholder
   `"example-topic"`, parses incoming JSON into `ExampleIntegrationEvent`,
   `mediator.Publish`es it) — its own doc comment explicitly tells an external
   consumer to "add one `IHostedService` like this per topic you need to consume,"
   i.e. it's a worked pattern, not a stub to configure. Dropping the RabbitMQ
   consumer also let the `RabbitMQ.Client` NuGet package reference itself be removed
   from the exported `ERP.NTF.Infrastructure.csproj` (it was the only thing using
   it) and let the whole `docker-compose.yml`/README drop RabbitMQ as an
   infrastructure requirement entirely — one less moving part for an external
   consumer to stand up.

3. **`Program.cs`** — three specific things needed removing that don't fit the
   trim-a-file model above, since they're single lines/blocks inside an otherwise
   fully generic file: the `AddSingleton<IConnection>(...)` RabbitMQ connection
   factory (now-unused, per point 2); `AddHostedService<LicenseEnforcementMiddleware>`'s
   registration and `app.UseMiddleware<LicenseEnforcementMiddleware>()` (this
   platform's tenant-license/module-gating system — meaningless without this
   platform's IdentityService issuing `licensed_module` claims); and
   `AddSingleton(new ModuleIdentity(ModuleCodes.Notification))` (the module-code
   catalog `LicenseEnforcementMiddleware` reads from). **Decision:** ship a
   full replacement `Program.cs` (template-overlay, not a diff/patch) with those
   three things removed and the three old consumer registrations swapped for
   `AddHostedService<ExampleKafkaConsumer>()` — everything else (SignalR hub
   mapping, JWT auth, localization, Swagger, CORS) is byte-for-byte the same as the
   source.

4. **`PermissionConstants.cs`** (`ERP.Shared.Authorization`) — the source monorepo's
   copy hardcodes ~230 permission codes for all 13 other services (`purchase_order:*`,
   `sales_order:*`, `pos:*`, ...). NotificationService's own controllers reference
   exactly 2 of them by constant (`Notifications_Send`, `Notifications_View` — most of
   its other permission checks use raw string literals like `"notifications:webhooks:manage"`
   directly, not the constants class, which is itself informative: the RBAC pattern
   here never actually required the shared 230-code catalog to function). **Decision:**
   ship a 6-constant version — this service's own 4 `Notifications_*` codes (matching
   what its controllers check, including the 2 referenced only as raw strings, added
   back as real constants so the class is genuinely useful to keep using) plus a
   `Sample_WidgetsView`/`Sample_WidgetsManage` pair (mirroring Identity's own export,
   which added the same `Sample_*` pattern) demonstrating the `module:resource:action`
   convention for whatever an external consumer adds next.

5. **Two other `Shared/Authorization` files trimmed, but for a different reason than
   #4** — not "ERP-specific," genuinely **unused by this service at all**, confirmed
   by grep returning zero matches anywhere in `src/services/NotificationService`:
   `HeaderUserContextService.cs` (NotificationService uses `JwtClaimsUserContextService`
   instead) and `RoleDefinitions.cs` (dead code even in the source monorepo, same
   finding Identity's own export made independently).

6. **Database seed data** — `004_SeedDemoData.sql` (~1000 rows keyed off a
   deterministic customer-user GUID formula, `20000000-0002-0000-0000-{N}`, that only
   resolves to anything meaningful against IdentityService's own seeded demo users)
   and `005_SeedWorkflowTemplates.sql` (seeds `PO_PENDING_FINANCE_APPROVAL`/
   `PO_APPROVED`/`LEAVE_APPROVED`/`LEAVE_REJECTED` templates — named for, and only
   ever sent by, the now-removed PurchaseOrder/HRM consumer classes). **Decision:**
   drop both from the export; replace `005` with a generic
   `005_SeedGenericTemplates.sql` seeding one additional illustrative template,
   `PASSWORD_RESET` (on top of the two already-generic templates
   `001_CreateNotificationDB.sql` seeds by default, `WELCOME_EMAIL` and
   `TWO_FACTOR_CODE` — those two were left as-is rather than surgically edited,
   since `001` mixes schema DDL and seed `INSERT`s in one file and hand-editing
   around a tested `CREATE TABLE` block was judged riskier than the two rows being
   slightly e-commerce-flavored in wording only, not in any actual code dependency).

### What's kept, contrary to what Identity's own export trimmed

Identity's export deliberately removed `AuthenticationExtensions.cs` (`AddErpJwtAuthentication`),
`HubTicketStore.cs`, and `UserContextHeaderMiddleware.cs` from its own copy of
`Shared/Authorization` — reasoning that Identity *issues* tokens and has no SignalR
hub, so those "validate someone else's token" / "hub ticket" helpers were dead weight
for it specifically. NotificationService is the mirror-image case: it **validates**
tokens issued by a separate identity service (`AddErpJwtAuthentication` is exactly
what it calls to do that) and it **does** have a SignalR hub (`/hubs/notifications`,
for live in-app bell notifications), which is precisely what `HubTicketStore`/
`UserContextHeaderMiddleware` exist to support (the doc comment on `HubTicketStore`
in the source explains why: browser WebSocket clients can't attach an
`Authorization` header, so a short-lived ticket gets minted and passed as a query
param instead). All three are kept in NotificationService's export, unmodified.

---

## N3 — Self-contained Docker packaging ✅ Done

`deployment/scripts/export-templates/notification/{Dockerfile,docker-compose.yml,run-migrations.sh,.env.example}`,
closely modeled on Identity's own templates:

- **Dockerfile**: same two-stage SDK-build/ASP.NET-runtime pattern as Identity's,
  paths adjusted for `ERP.NTF.API.csproj`/`ERP.NTF.API.dll`.
- **docker-compose.yml**: its own single-purpose `sqlserver` (not the monorepo's
  shared 15-database instance) and `redis`. Kafka is behind the same
  `profiles: ["with-kafka"]` opt-in pattern Identity's export uses — the doc comment
  at the top of the file states why: the bundled `ExampleKafkaConsumer` and the
  Outbox publisher both log-and-continue rather than crash when no broker is
  reachable, so it's a real optional dependency, not a required one just being
  soft-pedaled. Unlike Identity's compose file, this one has **no RabbitMQ service**
  at all (see N2 point 2 — the only consumer that needed it was removed). The app
  container's environment block calls out explicitly (in the file's own header
  comment) that `Jwt__Key`/`Issuer`/`Audience` must match a *separate* token issuer,
  since — unlike Identity — this service never signs anything itself.
- **run-migrations.sh**: byte-identical in structure to Identity's version (wait for
  SQL Server to accept connections, then apply every `./Database/*.sql` in numeric
  filename order) — genuinely reusable without modification since it has no
  service-specific logic, just a database-name comment updated from `AuthDB` to
  `NotificationDB`.
- **.env.example**: `SA_PASSWORD` (same as Identity's), plus `JWT_SIGNING_KEY`/
  `JWT_ISSUER`/`JWT_AUDIENCE` (documented in-file as "must match your identity
  service's signing config, not a value you invent independently" — a deliberate
  difference from Identity's own `.env.example`, where those same-named variables
  *are* the source of truth since Identity issues the tokens), and
  `SETTINGS_ENCRYPTION_KEY` (AES key for at-rest SMTP/SMS credential encryption,
  new — Identity's export has no equivalent, since Identity doesn't store
  third-party provider credentials).

---

## N4 — The export script itself ✅ Done, verified via isolated build

`deployment/scripts/export-notification-service.ps1` — a manual, on-demand tool
(never run automatically), mirroring `export-identity-service.ps1`'s overall
structure and numbered-step layout exactly, so anyone who's read Identity's script
can follow this one without relearning the pattern. Output goes to
`export/notification-service-standalone/` (gitignored, never committed) by default,
or `-OutputPath` anywhere else.

**Step-by-step, matching the script's own numbered comments:**

1. **Copy the 4 NotificationService projects** (`ERP.NTF.Domain/Application/Infrastructure/API`)
   into `<output>/src/`, verbatim, then delete any `bin`/`obj` folders that came along
   with the copy (build artifacts, not source).
2. **Copy the 10 `ERP.Shared.*` sub-projects** N1 determined are actually needed
   (`Core`, `Contracts`, `Messaging`, `Authorization`, `Localization`, `Middleware`,
   `Observability`, `Extensions`, `Security`, `Versioning`, plus
   `Infrastructure/{Caching,Exceptions}` — not `Infrastructure/Data`, which only
   PurchaseOrderService uses in the source monorepo) into `<output>/Shared/`. The
   script's own comment notes *why* `Middleware`/`Observability` are included even
   though NotificationService's code never references them directly: `Extensions`
   has a hard `ProjectReference` to both and won't build without them.
3. **Trim dead-weight files from the copied `Authorization` folder** — the 4 files
   listed in N2 point 5/6 above (`ModuleCodes.cs`, `LicenseEnforcementMiddleware.cs`,
   `HeaderUserContextService.cs`, `RoleDefinitions.cs`), each removal accompanied by
   an inline comment in the script explaining *which* N2 reasoning applies to that
   specific file (ERP-license-coupling vs. genuinely-unused) — then overlays the
   trimmed `PermissionConstants.cs` from step N2 point 4.
4. **Delete the umbrella** `Shared/ERP.Shared.csproj` from the copy entirely — the
   export never ships it, only the specific sub-projects each `.csproj` references
   directly (this is what makes N1's retargeting load-bearing: without it, this step
   would break the build).
5. **Retarget `ProjectReference` paths** in the copied NotificationService
   `.csproj` files — a regex string-replace of the literal path segment
   `..\..\..\..\Shared\` (4 levels up, the source monorepo's folder depth from
   `src/services/NotificationService/src/<Project>/`) to `..\..\Shared\` (2 levels
   up, the flatter export layout's depth from `src/<Project>/`). Applied uniformly
   across every `.csproj` under `<output>/src/` rather than hand-listing which files
   need it, so it's correct even if a future retargeting adds more Shared references.
6. **Remove the now-unused `RabbitMQ.Client` `PackageReference`** from the copied
   `ERP.NTF.Infrastructure.csproj` via a targeted regex line-removal (see N2 point 2)
   — the one dependency-file edit that isn't a whole-file template overlay, since
   removing one XML line safely didn't need one.
7. **Swap in the generic event/consumer/Program.cs** — deletes
   `IntegrationEventHandlers.cs` and the 3 ERP-specific consumer files, copies in
   `ExampleIntegrationEvent.cs`/`ExampleKafkaConsumer.cs`, and overwrites `Program.cs`
   wholesale with the N2-described trimmed version.
8. **Copy database migrations**, excluding `004_SeedDemoData.sql`/
   `005_SeedWorkflowTemplates.sql` (an explicit exclusion list, same pattern
   Identity's script uses for its own excluded migrations), then copies in
   `005_SeedGenericTemplates.sql` from the templates folder. Unlike Identity's
   export, there was no filename-collision housekeeping needed here — Notification's
   migration folder never had one.
9. **Copy the Docker packaging + README** (N3's five files) into the export root.
10. **Write `export-manifest.txt`** (a plain-text summary of what's included/generated
    fresh/excluded — the same three-section shape Identity's manifest uses) and run
    a **negative-check grep** across every `.cs`/`.sql`/`.csproj` file in the output
    for monorepo-specific strings (`IdentityService`, `SalesOrderService`,
    `PurchaseOrderService`, `HRMService`, `ChatBot`, `sales_order_events`,
    `identity-topic`, `purchasing-topic`, `hrm-topic`) — a broader pattern list than
    Identity's own script, since this export had more services' names to watch for
    given how many ERP-specific consumers were being removed.

**Negative-check result when actually run:** 15 matches, every one of them confirmed
to be a *code comment explaining what was excluded and why* (e.g. `ExampleKafkaConsumer.cs`'s
own doc comment says "replacing the source monorepo's three ERP-specific consumers
(IdentityService's identity-topic, ...)" — that's the comment *describing* the trim,
not a functional leak) — the same class of false-positive Identity's own negative
check found (4 matches there, also all prose). None were an actual leaked topic
name, hardcoded permission code, or functional reference to a sibling service.

---

## Verification

**Isolated build** ✅ Done: ran the script for real; `dotnet build
src/ERP.NTF.API/ERP.NTF.API.csproj` from inside the generated
`export/notification-service-standalone/` folder (a location with zero relative-path
leak back to the monorepo — confirmed by the retargeted paths resolving entirely
within the export folder) — **0 warnings, 0 errors**, restoring only the 15 projects
physically present in that isolated copy.

**Docker-level runtime verification** ⏳ **Not done — genuinely blocked, not skipped.**
Attempted `docker compose -p ntf-verify build` (via a verification-only
`docker-compose.override.yml` remapping the bundled SQL Server off host port 1433,
since this environment's live K8s stack already binds it — same technique Identity's
own Docker verification used). The Docker Desktop engine this machine shares with
that live K8s stack (14+ pods, per [port-map.md](../../architecture/port-map.md))
started returning `500 Internal Server Error` on every API call — including a bare
`docker version` — and stayed that way for an extended period. Root-caused to memory
pressure: only ~1.6–1.7 GB of 16 GB system RAM was free at the time, all consumed by
the already-running live stack; stacking a second full SQL-Server-plus-app Compose
build on top of that pushed the engine into a bad state rather than queuing
gracefully. One orphaned `docker compose build`/`buildx bake` Windows process tree
(left running after the attempt was cancelled at the tool level, since stopping the
wrapping shell command didn't kill its child processes) was found via
`Get-CimInstance Win32_Process` and killed directly — but the engine had not
recovered by the time this was written up, and free memory hadn't moved either.

**Deliberately not pursued further**, to avoid risking the live K8s environment —
restarting Docker Desktop or freeing memory by scaling down the live stack are both
the kind of action that needs the user's own call, not something to do unprompted
mid-verification. What's still needed to close this out, whenever conditions allow:
`docker compose -p ntf-verify build && up -d`, confirm `db-migrate` exits 0 and
`GET /health` returns 200, query `NotificationTemplates` post-migration to confirm
only the 3 generic templates exist (no `PO_*`/`LEAVE_*` rows), and a functional
round-trip (`POST /api/v1/notifications` with `templateCode: "WELCOME_EMAIL"`,
confirm it lands in `Notifications` with `Status = Sent` against whatever SMTP
sandbox is available locally) — the same shape of check Identity's own Docker
verification ran.

## Related

- [../Identity/CommonServiceRoadmap.md](../Identity/CommonServiceRoadmap.md) — the
  proven pattern this document applies to NotificationService; §7 there in
  particular for the `ERP.Shared` restructuring this document's N1 built on top of.
- [../../architecture/common-services-roadmap.md](../../architecture/common-services-roadmap.md) —
  the platform-wide candidate analysis (all 13 non-Identity services), NotificationService's
  entry there stays in sync with this document's status.
- [module-21-notification.md](module-21-notification.md) — this service's business/
  domain design spec (entities, state machine, retry policy) — unrelated to
  packaging, but the authoritative source for what the Domain layer this document's
  N2 calls "genuinely generic" actually does.
