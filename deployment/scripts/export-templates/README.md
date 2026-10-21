# IdentityService — standalone

A JWT/OAuth2/OIDC identity service: authentication, RBAC (roles/permissions),
multi-tenancy, 2FA (SMS/Email), SSO (Google/Microsoft/Facebook/Twitter), API keys,
webhooks, and a security audit log. Exported from a larger monorepo — see
`export-manifest.txt` for exactly which files came from where and what was
generated fresh.

This guide assumes **no familiarity with the monorepo it was exported from** — it
is self-contained.

## Requirements

- Docker + Docker Compose (all instructions below use it), **or** .NET 10 SDK +
  a reachable SQL Server instance if you'd rather run it directly.
- SQL Server (bundled in the compose file — no separate install needed).
- Redis (bundled — optional at the app level; falls back to in-memory cache if
  `Cache__Type` isn't set to `Redis`).
- Kafka — **optional**. 2FA-code delivery and webhook firing both degrade
  gracefully (logged, not a crash) if no broker is reachable. Only needed if you
  want those two features live.

## Quick start

```bash
cp .env.example .env
# edit .env: set a real SA_PASSWORD (no JWT secret — see "Signing keys")

docker compose up --build -d
curl http://localhost:5001/health
```

Add `--profile with-kafka` to also start Kafka.

## Register a tenant and log in

```bash
curl -X POST http://localhost:5001/api/v1/auth/tenant-signup \
  -H "Content-Type: application/json" \
  -d '{
    "tenantCode": "ACME",
    "tenantName": "Acme Inc",
    "adminEmail": "admin@acme.example",
    "adminPassword": "ChangeMe123!",
    "adminFullName": "Acme Admin"
  }'

curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Code: ACME" \
  -d '{"email":"admin@acme.example","password":"ChangeMe123!"}'
```

## Two login surfaces

1. **Proprietary REST API** (`/api/v1/auth/login` + `/refresh`) — what the two
   examples above use. Requires the `X-Tenant-Code` header (or a subdomain-based
   Gateway in front, which this export doesn't include — you're expected to route
   tenant resolution yourself, or always pass the header). Returns a JWT signed
   RS256 with the service's signing certificate.

2. **Standards-based OAuth2/OIDC** (`/connect/authorize`, `/connect/token`,
   `/connect/userinfo`, `/.well-known/openid-configuration`,
   `/.well-known/jwks.json`) — for third-party apps that want a redirect-based
   login flow (Authorization Code + PKCE) or machine-to-machine auth
   (`client_credentials`), using any off-the-shelf OIDC client library. Tokens
   from this surface are signed with the same RSA certificate (RS256), published
   at the `jwks_uri` above.
   OpenIddict has no built-in admin UI for registering OAuth clients; you'll need
   to create `OpenIddictApplication` rows yourself via `IOpenIddictApplicationManager`
   (see `src/ERP.Identity.Infrastructure/OpenIddict/` for the pattern this service's
   own dev seeder uses) until a real client-management API exists.

## Validating a token issued by this service (as a downstream resource server)

Both surfaces sign RS256 with the same certificate: validate via the public JWKS document
at `/.well-known/jwks.json` — no shared secret exists. Login (surface 1) tokens carry issuer
`Jwt__Issuer` and audience `Jwt__Audience`; any library with remote JWKS resolution works:

```csharp
services.AddAuthentication().AddJwtBearer(options =>
{
    options.MetadataAddress = "http://<host>:5001/.well-known/openid-configuration";
    options.RequireHttpsMetadata = false; // only on a private network
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = "<same Jwt__Issuer value>",
        ValidAudience = "<same Jwt__Audience value>",
        ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
    };
});
```

### Signing keys

`SigningKeys` (RS256 token signing) and `EncryptionKeys` (RSA-OAEP, OpenIddict refresh
tokens / authorization codes) take `Source` = `File` (PFX; `AutoCreate` generates one on
first start — keep it on a persistent volume) or `KeyVault` (Azure Key Vault certificate,
`DefaultAzureCredential`). The newest valid certificate is used; previous Key Vault versions
(or `File:PreviousPaths`) stay accepted so a rotation doesn't invalidate issued tokens.

### Claim shape (both surfaces)

| Claim | Meaning |
|---|---|
| `sub` | User id (GUID) |
| `email` | User's email |
| `name` | Display name |
| `tenant_id` | Which tenant this token is scoped to (GUID) |
| `tenant_name` | That tenant's display name |
| `role` (or `ClaimTypes.Role`) | Assigned role name(s), e.g. `SuperAdmin` |
| `permission` | One claim per granted permission code, e.g. `identity:users:view` |
| `licensed_module` | One claim per licensed module code for the tenant (surface 1 only) |

## Rate limits

Two policies apply to the auth endpoints, keyed by client IP
(`X-Forwarded-For` if present, else the connection's remote address):

| Policy | Limit | Applies to |
|---|---|---|
| `auth-sensitive` | 10 requests/minute | Login, register, password reset, tenant signup |
| `auth-2fa-verify` | 5 requests/minute | 2FA code verification |

## Database migrations

Hand-written, idempotent SQL files in `./Database/`, applied in numeric filename
order by `run-migrations.sh` (already wired into `docker compose up` via the
`db-migrate` service). Not EF Core Migrations — running `dotnet ef database
update` will do nothing useful here. Each file is safe to re-run.

`013_SeedGenericRolePermissions.sql` seeds two illustrative permission codes
(`sample:widgets:{view,manage}`) so the RBAC pattern is demonstrable out of the
box. Replace it with your own application's permission codes, following the same
pattern (insert into `Permissions`, grant to roles via `RolePermissions`).

## What's not included / known gaps

- **OAuth client management UI** — see above; client registration is currently
  code-only.
- **Production certificate provisioning** — the RS256 signing certificate for
  surface 2 is a self-signed one generated on first run and persisted in the
  container's local certificate store, fine for evaluation but not a real
  production PKI story (no rotation policy, no HSM/Key Vault integration).
- **CSRF protection on the OIDC interactive login/tenant-picker pages** — those
  are plain HTML forms with no antiforgery token yet.
- **2FA is not enforced on the OIDC interactive login flow** — only on surface 1
  (the proprietary REST login).
