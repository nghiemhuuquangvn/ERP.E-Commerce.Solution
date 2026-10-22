# ChatBot Service — Technical Reference

**Service:** ChatBotService (Port 5017, Code: CHB)
**Database:** ChatBotDB
**Status:** MVP implemented 2026-08-08 — backend + Storefront widget + Admin panel built
and compiling/typechecking clean; not yet exercised against live Anthropic/OpenAI keys or
a running Kubernetes/docker-compose stack.

This doc is the deep technical reference for the AI ChatBot feature — architecture,
mechanism design, API/event contracts, configuration, and step-by-step deployment for
both docker-compose and Kubernetes. For the one-paragraph service summary see
`src/services/ChatBotService/README.md`; for how this fits the platform's domain
groupings see `docs/architecture/domain-map.md` (Cross-Cutting Concerns).

---

## 1. Why this design

Three architecture decisions drive everything else in this service:

1. **Tool-calling over RAG.** ERP data the bot answers with (order status, stock,
   shipment tracking) is live and mutates constantly — a vector-search/RAG approach would
   always answer from a stale snapshot. Instead, the model is given a small set of
   functions that each make one **synchronous REST call to another service through the
   Gateway**, matching this platform's existing "Pattern A" convention
   (`docs/architecture/service-map.md`). No local copy of order/product/inventory data is
   ever stored in ChatBotDB.
2. **Reuse the existing License mechanism instead of new billing.** The platform has no
   subscription/payment integration for feature tiers
   (`docs/business/Identity/License.md`, "Issuance" section). Rather than build one just
   for this feature, Claude-vs-ChatGPT selection is entirely driven by the caller's
   tenant's existing `LicensePlan` (see §3).
3. **Every tool call forwards the caller's own JWT — never an elevated identity.** This
   is what makes tool-calling safe by construction: a customer's "what's my order status"
   question can only ever return that customer's own data, because SaleOrderService's
   `GetMyOrders`/`GetOrderById` endpoints enforce that themselves on the forwarded token.
   ChatBotService does not duplicate that authorization logic anywhere.

---

## 2. Architecture

```
                    ┌─────────────────────┐        ┌─────────────────────┐
                    │  Storefront (Vue)   │        │   Admin (React)     │
                    │  ChatWidget.vue     │        │  AssistantPanel.tsx │
                    └──────────┬──────────┘        └──────────┬──────────┘
                               │ REST (via Gateway)            │ REST (via Gateway)
                               │ /api/v1/chatbot/conversations │
                               │                                │
                               │ SignalR (DIRECT to :5017, bypasses Gateway)
                               │ /hubs/chat                     │
                               ▼                                ▼
                    ┌──────────────────────────────────────────────────┐
                    │                 ChatBotService (5017)             │
                    │  API:  ConversationsController, ChatHub           │
                    │  App:  SendMessageCommandHandler (agentic loop)   │
                    │  Infra: AnthropicProvider | OpenAiProvider        │
                    │         IErpTool x6, ErpGatewayClient (JWT fwd)   │
                    └───────┬───────────────────────────┬──────────────┘
                            │ tool calls, forwarding      │ escalate_to_human
                            │ caller's own JWT             │ (also forwards caller's JWT)
                            ▼                              ▼
        ┌──────────┬──────────┬───────────┬──────────┬─────────────┐
        │SaleOrder │ Product  │ Warehouse  │ Shipping │  Promotion  │      CRM
        │ Service  │ Service  │  Service   │ Service  │   Service   │    Service
        └──────────┴──────────┴───────────┴──────────┴─────────────┘
                            all reached through the Ocelot Gateway (5000)

                    ┌──────────────────────────────────────────────────┐
                    │              Anthropic / OpenAI APIs               │
                    │   (raw HTTPS + SSE streaming, no vendor SDK)       │
                    └──────────────────────────────────────────────────┘
```

Two independent network paths exist between a frontend and ChatBotService:
- **REST**, through the Gateway (5000) like any other service — used only for starting a
  conversation and reading history.
- **SignalR**, direct to ChatBotService's own port (5017) — used for the actual
  send-message/streaming-reply loop. See §6 for why.

---

## 3. Domain model

**Conversation** (aggregate root, `ERP.CHB.Domain.Entities.Conversation`)
| Field | Notes |
|---|---|
| `Id`, `TenantId` | from `BaseEntity` |
| `Channel` | `Storefront` \| `Admin` |
| `Provider` | `Claude` \| `ChatGpt` — resolved once at `Start()` and fixed for the conversation's lifetime (a mid-conversation token refresh can't silently change providers under a user) |
| `Status` | `Active` \| `Escalated` \| `Closed` |
| `CreatedBy` / `GuestSessionId` | exactly one is set — authenticated UserId, or an unauthenticated Storefront guest's client-generated session id |
| `StartedAt`, `ClosedAt`, `EscalatedCaseId` | |
| `Messages` | owned collection, EF `OwnsMany` (same pattern as `Notification.Logs`) |

**Message** (owned entity, not independently queryable)
| Field | Notes |
|---|---|
| `Role` | `User` \| `Assistant` \| `Tool` |
| `Content` | text (User/Assistant) or the tool's JSON result (Tool) |
| `ToolName`, `ToolCallId`, `ToolArgumentsJson` | Tool role only |

A single stored `Tool` message is reconstructed into **two** provider-wire messages when
rebuilding history for the next model call (an assistant `tool_use` turn + the matching
`tool_result` turn) — see `SendMessageCommandHandler.MapHistory` and each provider's
`BuildMessages`. No Outbox/Kafka producer exists in this service (see §9) — it neither
publishes nor consumes integration events in this MVP.

---

## 4. Provider selection (Claude vs ChatGPT)

Fully documented in `docs/business/Identity/License.md`; summarized here:

- `ERP.Shared.Authorization.ModuleCodes.ChatBot` ("CHB") — base gate. A tenant needs this
  module licensed to use the chatbot at all. Enforced the same way every other service
  enforces its module: `ModuleIdentity(ModuleCodes.ChatBot)` + the shared
  `LicenseEnforcementMiddleware`.
- `ModuleCodes.ChatBotPremium` ("CHB_PREMIUM") — a **tier flag, not a real service**. The
  shared middleware only ever checks one module code per service, so this is instead read
  directly by `ChatProviderResolver.ResolveProvider`:
  ```csharp
  caller.HasModule(ModuleCodes.ChatBotPremium) ? AiProvider.Claude : AiProvider.ChatGpt
  ```
- Seed data (`src/Database/Identity/017_AddChatBotLicenseModules.sql`): `STARTER` plan
  gets `CHB` only (→ ChatGPT); `PROFESSIONAL`/`ENTERPRISE` get both `CHB` and
  `CHB_PREMIUM` (→ Claude). Existing tenants already issued against one of these three
  plans are backfilled by the same migration (License.md notes a plan edit does **not**
  retroactively change already-issued tenants — this migration's backfill step exists
  specifically to work around that for this one-time rollout).
- Both module codes ride the same `licensed_module` JWT claim mechanism as every other
  module — issued at login/refresh, ~1 minute cache TTL server-side on IdentityService,
  otherwise bounded by access-token lifetime (same eventual-consistency trade-off as the
  rest of the License system).
- Unauthenticated Storefront guests always get `ChatGpt` (no JWT to read a license from —
  see `StartConversationCommandHandler`).

Model per provider/channel (`ChatBotSettings` config, see §8):

| Provider | Storefront | Admin |
|---|---|---|
| Claude | `claude-haiku-4-5` (fast/cheap, high volume) | `claude-sonnet-5` (more complex internal queries) |
| ChatGPT | `gpt-4o-mini` (same model both channels — already the free tier) | `gpt-4o-mini` |

---

## 5. Tool-calling mechanism

`SendMessageCommandHandler` runs an agentic loop, capped at `MaxToolRounds = 6` per user
turn (guards against runaway cost if a model keeps requesting tools):

1. Append the user's message; resolve the available tool set for this caller
   (`IErpToolRegistry.GetAvailableTools`, filtered by channel + guest/permission — see
   table below) plus the always-available built-in `escalate_to_human`.
2. Call `IChatCompletionProvider.StreamAsync` with the full reconstructed history + tool
   definitions. Stream `TextDeltaEvent`s straight to the client via `IChatStreamNotifier`;
   buffer `ToolUseRequestedEvent`s.
3. On `TurnCompletedEvent`: if `StopReason == "tool_use"`, execute each requested tool
   (forwarding the caller's JWT — see §1.3), append the results as `Tool` messages, and
   loop back to step 2 with the updated history. Otherwise the turn is done — notify
   `completed`.

| Tool | Calls | Channels | Guest OK? |
|---|---|---|---|
| `get_my_orders` | `GET /api/v1/orders/my` (SaleOrderService) | Storefront | No |
| `get_order_by_id` | `GET /api/v1/orders/{id}` | Storefront, Admin | No |
| `search_products` | `GET /api/v1/products/search?q=` (ProductService) | Storefront, Admin | Yes |
| `get_stock_availability` | `GET /api/v1/inventory/{productId}/availability` (WareHouseService) | Storefront, Admin | Yes |
| `get_shipment_by_order` | `GET /api/v1/shipments/order/{orderId}` (ShippingService) | Storefront, Admin | No |
| `get_active_promotions` | `GET /api/v1/promotions/all` (PromotionService) | Storefront | Yes |
| `escalate_to_human` (built-in) | see §7 | Storefront, Admin | No (needs a UserId) |

A downstream 4xx (e.g. 403 from a permission the caller lacks) is returned to the model as
a normal JSON tool-result (`{"error": "..."}`), not thrown — the model can relay this to
the user in natural language instead of the turn crashing. See `ErpGatewayClient.SendAsync`
and `ExecuteToolCallAsync`'s catch block.

**Endpoint paths were copied from each service's live controller route attributes as of
2026-08** (`SalesOrdersController`, `InventoryController`, `ShipmentsController`,
`ProductsController`, `PromotionsController`) — if those routes ever change, this table
and `src/services/ChatBotService/src/ERP.CHB.Infrastructure/Tools/ErpTools.cs` go stale
silently (a normal-looking tool-result error at runtime, not a build error).

---

## 6. Real-time transport (SignalR)

`ChatHub` (`/hubs/chat`) streams `textDelta` / `toolCallStarted` / `toolCallCompleted` /
`completed` / `error` events, grouped per-conversation (`Groups.AddToGroupAsync`, group
name `conversation-{id}`) so `SignalRChatStreamNotifier` can push without tracking
connection ids.

**Why it bypasses the Gateway:** Ocelot does not reliably proxy SignalR's
negotiate/WebSocket-upgrade handshake. REST endpoints under `/api/v1/chatbot/...` are
reachable both via the Gateway (5000) and directly; only the Hub connection is exposed
directly on ChatBotService's own port (5017). The Kubernetes Service for this
deployment sets `sessionAffinity: ClientIP` for this reason (see §11).

**Auth over WebSocket:** browsers can't set an `Authorization` header on a WS upgrade
request, so the SignalR JS/TS client instead appends `?access_token=<jwt>` to the
connection URL (`accessTokenFactory` in `HubConnectionBuilder`). This required one shared
change: `ERP.Shared.Authorization.AuthenticationExtensions.AddErpJwtAuthentication` (used
by **every** service, not just ChatBotService) now has an `OnMessageReceived` fallback
that reads this query param — but **only** when the request path starts with `/hubs`, so
every other service's existing header-only Bearer auth is unaffected (only ChatBotService
has a path under `/hubs` today).

`ChatHub` itself is `[AllowAnonymous]` (guests must be able to connect at all) — the real
enforcement (guest channel must be Storefront, guest can't use premium provider, guest
can't call non-guest tools) lives in `StartConversationCommandHandler` and
`SendMessageCommandHandler`, not at the transport layer.

---

## 7. Escalation to a human (CRM integration)

The built-in `escalate_to_human` tool calls CRMService's existing
`POST /api/v1/crm/cases` — **using the caller's own forwarded JWT**, the same rule as
every other tool (§1.3), not a service-elevated identity.

**Consequence:** this only succeeds for callers whose account already holds
`crm:contacts:view` + `crm:cases:manage` (typically staff on the Admin channel, or any
role a SuperAdmin has explicitly granted it to — see CRMService's `CasesController.Open`).
An ordinary Storefront customer without those permissions gets a graceful
`{"error": "Could not open a support case automatically..."}` tool result the model can
relay, instead of a crash. Giving ChatBotService its own service identity that could act
across arbitrary tenants would require a new cross-tenant service-principal capability in
IdentityService that does not exist today — deliberately **out of scope** for this MVP
rather than faked with a workaround. See `ICrmEscalationClient`'s doc comment for the
full reasoning.

CRMService's `OpenCaseCommand` needs a `ContactId`, not a `UserId`, so one small additive
endpoint was added: `GET /api/v1/crm/contacts/by-user/{userId}`
(`GetContactByUserIdQuery`/`GetContactByUserIdQueryHandler`) — it just exposes
`IContactRepository.GetByUserIdAsync`, which already existed for
`SalesOrderCreatedEventHandler`'s auto-provisioning. A customer who has never placed an
order (and so has no auto-provisioned Contact) will get a 404 here, surfaced the same
graceful way.

---

## 8. API reference

### REST (through the Gateway: `http://<gateway-host>:5000`)

| Method & Path | Auth | Body / Notes |
|---|---|---|
| `POST /api/v1/chatbot/conversations` | `[AllowAnonymous]` | `{ channel: 1\|2, guestTenantId?: guid, guestSessionId?: string }`. `channel` 1=Storefront, 2=Admin. Guest fields required only for an unauthenticated Storefront caller (Admin always requires auth). Returns `ConversationDto`. |
| `GET /api/v1/chatbot/conversations` | Required | Optional `?channel=1\|2`. Returns `ConversationDto[]`, the caller's own conversations only. |
| `GET /api/v1/chatbot/conversations/{id}` | Required | Returns `ConversationHistoryDto` (403 if the conversation isn't the caller's own). |

```ts
// ConversationDto
{ id: string, channel: 1|2, provider: 1|2, status: 1|2|3, startedAt: string, closedAt: string|null, escalatedCaseId: string|null }
// ConversationHistoryDto
{ conversation: ConversationDto, messages: Array<{ id: string, role: 1|2, content: string, toolName: string|null, createdAt: string }> }
```
(`status`: 1=Active, 2=Escalated, 3=Closed. `provider`: 1=Claude, 2=ChatGpt. `role` in
history: 1=User, 2=Assistant — Tool-role messages are filtered out server-side.)

### SignalR Hub: `ws(s)://<chatbot-host>:5017/hubs/chat`

Client → server:
- `JoinConversation(conversationId: string)` — call once after connecting, before sending.
- `SendMessage(conversationId: string, content: string)` — the reply streams back via the
  events below; this does not return the answer directly, and does not echo the user's
  own message back (append it optimistically client-side).

Server → client:
| Event | Payload | Meaning |
|---|---|---|
| `textDelta` | `string` | One streamed chunk of the assistant's reply text |
| `toolCallStarted` | `string` (tool name) | A tool call has begun |
| `toolCallCompleted` | `string` (tool name) | That tool call finished |
| `completed` | — | The current turn is fully done |
| `error` | `string` (message) | The turn failed; show and stop any "typing" indicator |

---

## 9. Configuration reference

`src/services/ChatBotService/src/ERP.CHB.API/appsettings.json`:

| Key | Purpose |
|---|---|
| `ConnectionStrings:ChatBotDb` | SQL Server connection string |
| `Jwt:Authority` / `Jwt:Issuer` / `Jwt:Audience` | IdentityService's internal URL (JWKS, RS256 only) and the login token issuer/audience |
| `ServiceAuth:Name` / `ServiceAuth:SigningKeys:*` | this service's own key for its host → tenant lookups at Identity (`/internal`, see `docs/business/Identity/SigningKeys.md`) |
| `Gateway:BaseUrl` | e.g. `http://gateway:5000` — every tool call and escalation goes through this |
| `Anthropic:ApiKey`, `Anthropic:MaxTokens` | Claude provider |
| `OpenAi:ApiKey`, `OpenAi:MaxTokens` | ChatGPT provider |
| `ChatBotSettings:StorefrontModel` / `:AdminModel` / `:FreeModel` | model ids, see §4 |
| `Cors:AllowedOrigins` | must list the Storefront/Admin origins exactly (credentialed CORS — can't be `*`) |

No Outbox/Kafka/RabbitMQ configuration exists for this service — deliberately omitted in
this MVP (see §3). If a future need arises (e.g. feeding ReportingService with chat
analytics), add the Outbox pattern the same way `NotificationDbContext.SaveChangesAsync`
does, rather than building anything bespoke.

---

## 10. Database migrations

Two migrations are required, in either order:

1. **`src/Database/Identity/017_AddChatBotLicenseModules.sql`** against `AuthDB` — adds
   the `CHB`/`CHB_PREMIUM` `ModuleDefinitions` rows, wires them into the `STARTER`/
   `PROFESSIONAL`/`ENTERPRISE` `LicensePlanModules`, and backfills already-issued active
   `TenantLicenses` against those plans. **No IdentityService code changed** — the
   currently-running IdentityService container works with this data as soon as it's
   applied; no redeploy of IdentityService itself is required.
2. **`src/Database/ChatBot/001_CreateChatBotDB.sql`** — creates the new `ChatBotDB`
   database and its two tables (`Conversations`, `ConversationMessages`).

Both follow this repo's standard idempotent `IF NOT EXISTS (...) CREATE ...` pattern, so
re-running them is safe.

`deployment/sql/init-databases.sql` was also updated to include `ChatBotDB` in its
up-front "create all databases" pass.

---

## 11. Deploying — Docker Compose

Everything needed is already wired into the root `docker-compose.yml` and
`deployment/docker/run-migrations.sh` (which auto-discovers every folder under
`src/Database/*/`, so the new `ChatBot/` folder needs no separate wiring).

1. **Fill in secrets** — copy `.env.example` to `.env` if you haven't already, and set:
   ```
   ANTHROPIC_API_KEY=<your Anthropic key>
   OPENAI_API_KEY=<your OpenAI key>
   ```
   (alongside the existing `SA_PASSWORD`/`RABBITMQ_PASSWORD`).

2. **Rebuild the services that changed.** Everything else can be left running —
   `ERP.Shared` changes were additive/path-gated and don't affect other services'
   already-built images (see §12's note on why).
   ```bash
   docker compose build chatbotservice crmservice gateway
   ```

3. **Run migrations** (the `db-migrate` one-shot service re-runs safely — every script is
   idempotent):
   ```bash
   docker compose up db-migrate
   ```

4. **Start/restart the affected services:**
   ```bash
   docker compose up -d chatbotservice crmservice gateway
   ```

5. **Frontend:** rebuild Storefront/Admin with the new env vars set (see §13), then run
   them as usual (`npm run dev` or your existing build/deploy step for these apps — no
   docker-compose service exists for the frontend apps in this repo).

6. **Verify:**
   ```bash
   curl http://localhost:5017/health
   curl http://localhost:5000/api/v1/chatbot/conversations -X POST -H "Content-Type: application/json" \
     -d '{"channel":1,"guestTenantId":"<a real tenant guid>","guestSessionId":"smoke-test"}'
   ```
   A successful response is a `ConversationDto` JSON body with a new `id`.

---

## 12. Deploying — Kubernetes

Manifests: `deployment/k8s/deployments/chatbot.yaml` (Deployment + Service),
`deployment/k8s/secrets.yaml` (updated with `sql-chatbot-db`, `anthropic-api-key`,
`openai-api-key` keys), `configmap.yaml` (unchanged, ChatBotService reads the same
`erp-config` ConfigMap as every other service via `envFrom`).

1. **Fill in real secret values** in `deployment/k8s/secrets.yaml` before applying —
   `sql-chatbot-db` (host SQL Server connection string, same shape as the other
   `sql-*-db` keys), `anthropic-api-key`, `openai-api-key` currently hold
   `ChangeMe_...` placeholders:
   ```bash
   kubectl apply -f deployment/k8s/secrets.yaml -n erp
   ```

2. **Run the two SQL migrations (§10) directly against your SQL Server** — this platform
   runs SQL Server on the host (or an external instance), not as an in-cluster
   StatefulSet, so there is no in-cluster migration Job to trigger; apply them the same
   way you applied every other service's migrations for this environment (e.g.
   `sqlcmd -S <host> -U sa -P <password> -i src/Database/Identity/017_AddChatBotLicenseModules.sql`,
   then the ChatBot one).

3. **Build and push the ChatBotService image** (adjust registry/tag to your existing
   convention — this repo's Dockerfile takes `SERVICE_PATH`/`PROJECT_FILE`/
   `ASSEMBLY_NAME` build args, same as every other service):
   ```bash
   docker build -t erp/chatbot-service:latest \
     --build-arg SERVICE_PATH=src/services/ChatBotService/src/ERP.CHB.API \
     --build-arg PROJECT_FILE=ERP.CHB.API.csproj \
     --build-arg ASSEMBLY_NAME=ERP.CHB.API .
   docker push erp/chatbot-service:latest   # if using a remote cluster
   ```
   Also rebuild/push `erp/crm-service:latest` (new `by-user` endpoint) and
   `erp/gateway:latest` (new Ocelot route) the same way.

4. **Apply the new deployment:**
   ```bash
   kubectl apply -f deployment/k8s/deployments/chatbot.yaml -n erp
   ```

5. **Roll the two changed deployments** so they pick up the rebuilt images:
   ```bash
   kubectl rollout restart deployment/crm-service -n erp
   kubectl rollout restart deployment/gateway -n erp
   ```
   (IdentityService needs **no** rollout — see §10, migration-only.)

6. **WebSocket ingress for the Hub — the one manual step not included in the manifest**
   (host/Ingress-class naming is environment-specific, so this is deliberately left for
   you to wire alongside whatever Ingress already fronts the Gateway): route
   `/hubs/chat` on ChatBotService's port 5017 with session affinity, e.g. for an
   nginx-ingress:
   ```yaml
   apiVersion: networking.k8s.io/v1
   kind: Ingress
   metadata:
     name: chatbot-hub
     namespace: erp
     annotations:
       nginx.ingress.kubernetes.io/affinity: "cookie"
       nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
       nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
   spec:
     rules:
     - host: <your-host>
       http:
         paths:
         - path: /hubs/chat
           pathType: Prefix
           backend:
             service:
               name: chatbotservice
               port:
                 number: 5017
   ```
   The `chatbotservice` Service itself already sets `sessionAffinity: ClientIP` as a
   second layer of stickiness.

7. **Verify:**
   ```bash
   kubectl get pods -n erp -l app=chatbot-service
   kubectl logs -n erp -l app=chatbot-service --tail=50
   kubectl port-forward -n erp svc/chatbotservice 5017:5017
   curl http://localhost:5017/health
   ```

---

## 13. Frontend deployment notes

Both apps need two new env vars and a rebuild:

| App | New env vars | Notes |
|---|---|---|
| Storefront | `VITE_CHATBOT_WS_URL` (e.g. `http://localhost:5017`), `VITE_TENANT_ID` | `VITE_TENANT_ID` is the GUID counterpart of the existing `VITE_TENANT_CODE` — **currently left as a placeholder nil GUID** (`00000000-0000-0000-0000-000000000000`) in `.env`; must be set to this deployment's real tenant GUID before guest chat works, since guests have no JWT to resolve a tenant from. |
| Admin | `VITE_CHATBOT_WS_URL` | Documented in `.env.example`. |

Both apps added `@microsoft/signalr` as a new dependency (`npm install` picks it up from
`package.json`/`package-lock.json`, already committed).

`VITE_CHATBOT_WS_URL` must point at ChatBotService's **own** port (5017 directly, or
whatever Ingress host/path you set up in §12 step 6) — not the Gateway's port 5000 — for
the exact reason in §6.

---

## 14. Known limitations / deliberately out of scope for this MVP

- **No RAG/vector search** for policy/FAQ questions — only live tool-calling. Adding one
  would reuse Elasticsearch (already in the stack for product search) rather than a new
  vector DB, if ever needed.
- **Escalation requires the caller to already hold CRM permissions** (§7) — no
  cross-tenant service-principal identity exists to work around this for arbitrary
  Storefront customers.
- **License tier is per-tenant, not per-user** — every user in a `STARTER` tenant gets
  ChatGPT, every user in a `PROFESSIONAL`/`ENTERPRISE` tenant gets Claude. Per-user
  billing was explicitly ruled out for this pass (see the plan discussion this feature
  was built from) in favor of reusing the existing tenant-level License mechanism.
- **No Outbox/Kafka integration** — this service neither publishes nor consumes
  integration events today (§3, §9).
- **Endpoint paths for the 6 REST tools are hand-copied**, not generated or contract-
  tested against the upstream services (§5) — a route rename in SaleOrderService/
  ProductService/WareHouseService/ShippingService/PromotionService silently breaks the
  matching tool at runtime, not at build time.
- **Not yet load- or cost-tested** against real Anthropic/OpenAI traffic; `MaxToolRounds`
  and per-provider token limits are conservative defaults, not measured values.
