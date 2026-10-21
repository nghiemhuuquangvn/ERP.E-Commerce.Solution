# NotificationService — standalone

A multi-channel notification service: Email (SMTP), SMS (Twilio or a null stub),
Push (FCM or a null stub), in-app "bell" notifications delivered live over SignalR,
templates with variable substitution, per-tenant/system delivery preferences,
webhooks (HMAC-signed outbound event delivery), and a queryable send history.
Exported from a larger monorepo — see `export-manifest.txt` for exactly which files
came from where and what was generated fresh.

This guide assumes **no familiarity with the monorepo it was exported from** — it
is self-contained. It also assumes **no familiarity with any specific identity
provider** — this service validates JWTs but does not issue them (see "Authentication"
below).

## Requirements

- Docker + Docker Compose (all instructions below use it), **or** .NET 10 SDK +
  a reachable SQL Server instance if you'd rather run it directly.
- SQL Server (bundled in the compose file — no separate install needed).
- Redis (bundled — optional at the app level; falls back to in-memory cache if
  `Cache__Type` isn't set to `Redis`).
- Kafka — **optional**. The bundled `ExampleKafkaConsumer` and the outbound Outbox
  publisher both degrade gracefully (logged, not a crash) if no broker is
  reachable. Only needed if you want event-driven delivery live.
- A separate identity/auth service issuing JWTs this service can validate (see
  "Authentication" below) — not bundled. Any standards-compliant JWT issuer works.

## Quick start

```bash
cp .env.example .env
# edit .env: set SA_PASSWORD, and JWT_AUTHORITY/JWT_ISSUER/JWT_AUDIENCE to match
# your identity provider's token-signing configuration

docker compose up --build -d
curl http://localhost:5013/health
```

Add `--profile with-kafka` to also start Kafka.

## Authentication

**This service validates JWTs — it does not issue them.** Tokens must be RS256; the
public keys are fetched from `Jwt__Authority` + `/.well-known/jwks.json` (set
`JWT_AUTHORITY` in `.env`), and `Jwt__Issuer`/`Jwt__Audience` must exactly match what
your identity/auth service puts in the token, or every authenticated request 401s.
Shared-secret (HS256) tokens are not accepted. This platform's own IdentityService
export is one worked example of a compatible issuer.

### Claim shape expected on incoming tokens

| Claim | Meaning | Required for |
|---|---|---|
| `sub` | User id (GUID) | All authenticated endpoints |
| `tenant_id` | Which tenant this token is scoped to (GUID) | Multi-tenant settings/preference endpoints |
| `permission` | One claim per granted permission code, e.g. `notifications:view` | Permission-gated endpoints (see below) |

## Sending a notification

```bash
curl -X POST http://localhost:5013/api/v1/notifications \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token with notifications:send>" \
  -d '{
    "recipientId": "11111111-1111-1111-1111-111111111111",
    "recipientEmail": "someone@example.com",
    "channel": 0,
    "templateCode": "WELCOME_EMAIL",
    "templateVariables": { "firstName": "Ada", "lastName": "Lovelace" }
  }'
```

`channel`: `0`=Email, `1`=Sms, `2`=Push, `3`=InApp. Either pass `templateCode` (looked
up from `NotificationTemplates`, with `{variable}` placeholders substituted from
`templateVariables`) or pass `subject`/`body` directly.

## Reacting to events from your own upstream services

`ExampleKafkaConsumer` (`src/ERP.NTF.Infrastructure/BackgroundJobs/`) demonstrates the
full pattern end-to-end: subscribe to a Kafka topic → deserialize into a typed event
→ `mediator.Publish(event)` → a matching `INotificationHandler<T>`
(`ExampleIntegrationEvent`/`ExampleIntegrationEventHandler` in
`src/ERP.NTF.Application/EventHandlers/`) builds and sends a `SendNotificationCommand`.
Add one event + handler pair and one `IHostedService` consumer per upstream event you
actually need to react to, following that same shape, then register the consumer in
`Program.cs` alongside `ExampleKafkaConsumer`.

## Permission-gated endpoints

Authorization is inline (`UserContext.HasPermission("some:code")`) rather than
attribute-based — see any controller under `Controllers/` for the exact codes each
endpoint checks (e.g. `notifications:send`, `notifications:view`,
`notifications:templates:manage`, `notifications:webhooks:manage`,
`settings:notification:{system,tenant}:{view,manage}`). `PermissionConstants.cs`
defines this service's own codes plus a `Sample_*` pair — your JWT issuer is
responsible for putting the matching `permission` claims on tokens for users who
should pass these checks.

## Webhooks

`POST /api/v1/webhooks` registers a tenant-scoped URL + comma-separated event-type
filter + HMAC secret (`notifications:webhooks:manage`). Delivery is fire-and-forget,
HMAC-SHA256 signed (`X-Event-Type`/`X-Timestamp`/`X-Hub-Signature` headers) —
deliberately no persisted retry/dead-letter queue; a failed delivery is logged, not
retried.

## Database migrations

Hand-written, idempotent SQL files in `./Database/`, applied in numeric filename
order by `run-migrations.sh` (already wired into `docker compose up` via the
`db-migrate` service). Not EF Core Migrations — running `dotnet ef database update`
will do nothing useful here. Each file is safe to re-run.

`001_CreateNotificationDB.sql` seeds three illustrative templates (`WELCOME_EMAIL`,
`TWO_FACTOR_CODE`, plus a fourth added by `005_SeedGenericTemplates.sql`,
`PASSWORD_RESET`) so the template mechanism is demonstrable out of the box. Replace
them with your own application's templates, following the same pattern (insert into
`NotificationTemplates`, reference by `Code` from `SendNotificationCommand`).

## What's not included / known gaps

- **No bundled identity/auth service** — see "Authentication" above; you must supply
  your own JWT issuer.
- **No client-side rate limiting** on the notification-send endpoint — add your own
  if this service is exposed to untrusted callers directly rather than behind an
  internal gateway.
- **Webhook delivery has no retry/dead-letter queue** — best-effort only, see
  "Webhooks" above.
- **Push (FCM) and SMS (Twilio) both fall back to a null/no-op sender** if their
  respective config keys (`Fcm:ServerKey`, `Twilio:AccountSid`) aren't set — useful
  for local evaluation, but means those channels silently no-op until configured.
