# ChatBot Service

AI chat assistant for both the Storefront (customers) and Admin (staff) frontends, backed
by live tool-calling against the platform's other REST APIs — not a local data copy, not
static RAG/vector search.

## Overview

- **Two channels**: Storefront (customer-facing, supports authenticated + anonymous
  guests) and Admin (internal staff, requires the `chatbot:assistant:use` permission).
- **Two LLM providers**: Anthropic Claude or OpenAI ChatGPT (free tier). Which one a
  conversation uses is decided once at start, purely from the caller's existing tenant
  license — see "Provider selection" below. There is no billing/subscription code in
  this service.
- **Answers via tool-calling, not RAG**: the model is given a small set of functions
  (`get_my_orders`, `get_order_by_id`, `search_products`, `get_stock_availability`,
  `get_shipment_by_order`, `get_active_promotions`, plus the built-in
  `escalate_to_human`) that each make one live REST call to another service **through the
  Gateway, forwarding the caller's own JWT**. The downstream service's own RBAC/tenant
  scoping decides what the caller can see — this service never duplicates that logic.
- **Streaming**: replies stream token-by-token over a SignalR hub (`/hubs/chat`), exposed
  directly on this service's own port (5017) rather than only through the Gateway — see
  "Real-time transport" below.
- **No Outbox/Kafka/RabbitMQ**: unlike most services in this platform, ChatBotService
  publishes no integration events and consumes none in this MVP. If a future need arises
  (e.g. feeding ReportingService with chat analytics), add the Outbox pattern then —
  it was deliberately left out to avoid unused infrastructure.

## Core Domain Model

**Conversation** (Aggregate Root)
- Id, TenantId, Channel (`Storefront`/`Admin`), Provider (`Claude`/`ChatGpt`, fixed for
  the conversation's lifetime), Status (`Active`/`Escalated`/`Closed`)
- CreatedBy (authenticated UserId) **or** GuestSessionId (unauthenticated Storefront
  guest) — never both
- StartedAt, ClosedAt, EscalatedCaseId (CRM case id, if escalated)
- Messages (owned collection)

**Message** (owned by Conversation, no independent identity lookup)
- Role (`User`/`Assistant`/`Tool`), Content
- ToolName/ToolCallId/ToolArgumentsJson (Tool role only — correlates back to the
  provider's tool_use block; see `SendMessageCommandHandler.MapHistory` for how a single
  stored Tool message is reconstructed into the provider's own request/response shape)

## Provider selection (Claude vs ChatGPT)

Reuses IdentityService's existing `TenantLicense`/`LicensePlan` mechanism instead of any
new billing/subscription code — see `docs/business/Identity/License.md` and
`ERP.Shared.Authorization.ModuleCodes`:

- `ModuleCodes.ChatBot` ("CHB") — base gate; a tenant needs this licensed to use the
  chatbot at all (`ModuleIdentity(ModuleCodes.ChatBot)` + `LicenseEnforcementMiddleware`,
  same as every other service).
- `ModuleCodes.ChatBotPremium` ("CHB_PREMIUM") — a tier flag, not a real service. Not
  checked by the shared middleware (which only gates one code per service); instead
  `ChatProviderResolver.ResolveProvider` reads `UserContext.HasModule("CHB_PREMIUM")`
  directly. STARTER plan tenants get `CHB` only (ChatGPT); PROFESSIONAL/ENTERPRISE get
  both (Claude). See `src/Database/Identity/017_AddChatBotLicenseModules.sql`.
- Unauthenticated Storefront guests always get ChatGPT (the free provider) — there is no
  JWT to read a license from.

## Real-time transport

Ocelot does not reliably proxy SignalR's negotiate/WebSocket-upgrade handshake, so
`ChatHub` is exposed directly on this service's own port (5017 — see docker-compose.yml
and deployment/k8s/deployments/chatbot.yaml, which sets `sessionAffinity: ClientIP` on
its Service) instead of only through the Gateway (port 5000). REST endpoints
(`/api/v1/chatbot/conversations...`) are reachable both ways, like any other service.

The browser client can't set an `Authorization` header on a WebSocket upgrade, so it
passes the JWT as `?access_token=...` on the hub URL instead (standard SignalR pattern).
`ERP.Shared.Authorization.AuthenticationExtensions.AddErpJwtAuthentication` was extended
with an `OnMessageReceived` fallback that reads this query param, but **only** under the
`/hubs` path prefix — every other service's existing header-based auth is unaffected.

## Escalation to a human (CRM integration)

The built-in `escalate_to_human` tool opens a case via CRMService's existing
`POST /api/v1/crm/cases`, using the **caller's own forwarded JWT** — not an elevated
service identity. This means escalation only succeeds for callers whose account already
holds `crm:contacts:view` + `crm:cases:manage` (typically staff on the Admin channel, or
any role a SuperAdmin has explicitly granted it to); an unprivileged Storefront customer
gets a graceful "couldn't open a case automatically" result the model can relay, instead
of a crash. Giving ChatBotService its own cross-tenant service identity would need a new
service-principal capability in IdentityService that doesn't exist today — deliberately
out of scope rather than faked. See `ICrmEscalationClient`'s doc comment.

CRMService gained one small additive endpoint for this:
`GET /api/v1/crm/contacts/by-user/{userId}` (resolves the CRM Contact linked to an
IdentityService user — the repository method already existed for
`SalesOrderCreatedEventHandler`'s auto-provisioning, this just exposes it), since
`OpenCaseCommand` needs a ContactId, not a UserId.

## Endpoints

| Route | Auth | Purpose |
|---|---|---|
| `POST /api/v1/chatbot/conversations` | `[AllowAnonymous]` (guest allowed on Storefront channel only) | Start a conversation |
| `GET /api/v1/chatbot/conversations` | Required | List the caller's own past conversations |
| `GET /api/v1/chatbot/conversations/{id}` | Required | Full message history |
| SignalR `/hubs/chat` | `[AllowAnonymous]` at the hub level; enforced per-command in `SendMessageCommandHandler` | `JoinConversation(id)`, `SendMessage(id, content)` → streams `textDelta`/`toolCallStarted`/`toolCallCompleted`/`completed`/`error` |

## Configuration

See `appsettings.json` — `ConnectionStrings:ChatBotDb`, `Jwt:*`, `Gateway:BaseUrl`,
`Anthropic:ApiKey`, `OpenAi:ApiKey`, `ChatBotSettings:{StorefrontModel,AdminModel,FreeModel}`,
`Cors:AllowedOrigins`.
