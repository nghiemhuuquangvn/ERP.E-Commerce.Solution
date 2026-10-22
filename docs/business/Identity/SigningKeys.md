# Token keys: RS256 user tokens, service-to-service tokens, no shared secret

Since 2026-10-21 the platform has **no shared symmetric token key**: `Jwt:Key` /
`JWT_SIGNING_KEY` no longer exist. There are three kinds of RSA keys, and each private key
lives in exactly one service:

| Key | Private key held by | Used for | Public part / verified by |
|---|---|---|---|
| **User token signing** (`SigningKeys`) | IdentityService | RS256 access tokens for the frontends (login), OpenIddict tokens | `/.well-known/jwks.json` of Identity → every service + the Gateway (`ErpJwtBearerOptions`) |
| **OpenIddict encryption** (`EncryptionKeys`) | IdentityService | RSA-OAEP encryption of OpenIddict refresh tokens / authorization codes | only Identity reads them |
| **Service token signing** (`ServiceAuth:SigningKeys`) | each *calling* service (SaleOrder, Gateway, ChatBot) | 60 s RS256 tokens for another service's `/internal/...` endpoints | `/internal/jwks.json` of that caller → the services that list it in `ServiceAuth:TrustedCallers` |

History: until 2026-10-20 every service held the HS256 `Jwt:Key` and could mint user tokens
(SecurityReview S6/M1). Phase 4 release 1 moved user tokens to RS256 + JWKS. On 2026-10-21 the
key was removed entirely, and service-to-service tokens were introduced.

## Certificate sources

All three use `ERP.Shared.Security.Certificates.CertificateLoader`, configured with
`<section>:Source`:

| Source | Where | Notes |
|---|---|---|
| `KeyVault` | production (k8s) | `KeyVault:VaultUri` and `KeyVault:CertificateName`, authenticated with `DefaultAzureCredential` (AKS Workload Identity: one ServiceAccount per service in `deployment/k8s/deployments/*.yaml`). The identity needs **certificates/list** and **secrets/get**, because the private key is the certificate's secret. Every enabled, currently valid version is loaded: the newest is current, older ones stay accepted. |
| `File` | local / Docker Compose | PFX at `File:Path`, optionally `File:Password` and `File:PreviousPaths[]`. `File:AutoCreate=true` generates a self-signed RSA 2048 certificate once (2 years). It must be on a persistent volume (`identity_signing` in compose), otherwise every re-deploy rotates the key. |
| `Ephemeral` | service keys only, **single replica** | A new in-memory key on each start. Fine for 60 s service tokens: receivers re-fetch the caller's JWKS on an unknown `kid`. Never use it with several replicas: each replica would have its own key, and receivers would only see one of them. |

Missing or unusable configuration **stops start-up**. There is never a silent throw-away key.
Generated certificates carry the key usage of their purpose: `DigitalSignature` for signing,
`KeyEncipherment` for encryption. OpenIddict refuses a signing-only certificate for encryption.

## User tokens (backend ↔ frontend)

- `TokenService` signs RS256 with the current `SigningKeys` certificate. Its `kid` is the one
  OpenIddict publishes in the JWKS.
- Every service (`AddErpJwtAuthentication`) and the Gateway (`GatewayJwtBearer`) validate through
  `ErpJwtBearerOptions`:
  - the JWKS comes from `Jwt:Authority` (internal URL) and is re-fetched on an unknown `kid`, with a retry every 30 s;
  - only **RS256** is allowed;
  - the issuer must be exactly `Jwt:Issuer` and the audience `Jwt:Audience`;
  - the phase-3 revocation check runs on every request.
- Only login tokens are accepted outside Identity. OpenIddict (`urn:`) tokens remain Identity-only,
  because client_credentials permissions don't go through `EffectivePermissions` yet.

## Service-to-service tokens (API ↔ API)

- **Caller:** `services.AddHttpClient<…>().AddServiceToken("<target>")`, plus `app.MapServiceJwks()`.
  - The token has `iss`/`sub` = `erp-svc:<own name>`, `aud` = `erp-svc:<target>`, a lifetime of 60 s, and `tenant_id` (the request's tenant).
  - It also carries `act_sub`: the user being served, if any.
  - **No tenant means the request is not sent**, unless the client is `tenantless: true` (the host → tenant lookup).
- **Receiver:** `services.AddErpServiceAuthentication(configuration)` and `[AuthorizeInternalService("caller", …)]` on
  `internal/...` routes.
  - Each trusted caller's keys only validate that caller's tokens.
  - User tokens are refused there, and service tokens are refused on user endpoints.
  - The token's tenant becomes the request tenant, so tenant filters apply.
  - The architecture tests enforce that `internal/` ⇔ the attribute, and that `ocelot.json` never routes `/internal`.
- **Current calls:**

  | Caller → receiver | Endpoint |
  |---|---|
  | SaleOrder → Warehouse | `GET internal/v1/inventory/{id}/availability` |
  | SaleOrder → Promotion | `POST internal/v1/promotions/validate-coupon` |
  | Gateway, ChatBot → Identity | `GET internal/v1/tenants/resolve` (tenant-less) |

- **Not a service token:** ChatBot → Gateway forwards the *user's* own JWT (acting for that user).
- **Configuration:**
  - `ServiceAuth:Name` on both sides;
  - `ServiceAuth:SigningKeys:*` on callers;
  - `ServiceAuth:TrustedCallers:<name>` = `http://<caller>:<port>/internal/jwks.json` on receivers;
  - `Jwt:RequireHttpsMetadata=false` for in-cluster http.
  - A service without trusted callers refuses every service token.

## Rotation

**Key Vault (any of the three keys).**
1. Create a new certificate version, or let Key Vault auto-renew it.
2. Restart the owning service (all replicas). Receivers pick up the new `kid` automatically.
3. After the tokens made with the old version have expired, **disable** it and restart again:
   - user tokens: `Jwt:Expired`, 60 min;
   - service tokens: 60 s;
   - encryption: the OpenIddict refresh-token lifetime.

**File.**
1. Put the new PFX at `File:Path` and list the old file in `File:PreviousPaths`.
2. Restart the service.
3. Remove the old file from `PreviousPaths` later.

**Emergency (private key leaked).**
1. Disable the leaked version **and** create a new one.
2. Restart the owning service.
3. Tokens signed with the leaked key stop validating once receivers refresh the JWKS.
4. A leaked *service* key only ever allowed acting as that one service, on the internal
   endpoints that trust it. It never allowed acting as a user or as another service.

## Consequences of removing `Jwt:Key` (2026-10-21)

- **OpenIddict refresh tokens and authorization codes** issued before the change can no longer be
  decrypted. Today only dev-seeded OIDC clients exist; they must re-authorize.
- **Login refresh tokens** (opaque database rows) are unaffected.
- **The `JWT_SIGNING_KEY` leaked in git history** (SecurityReview C3) no longer signs or validates
  anything.

## Verifying

- **JWKS:**
  - `curl http://identityservice:5001/.well-known/jwks.json` shows one key per signing certificate;
  - `curl http://salesorderservice:5002/internal/jwks.json` shows SaleOrder's service key.
- **Tokens:** a login token header shows `"alg":"RS256"`. Calling a `/internal` endpoint without a
  token, or with a user token, answers 401.
- **Automated tests:**
  - `ERP.Shared.UnitTests`:
    - `ErpJwtBearerOptionsTests`: HS256 refused even with a leftover `Jwt:Key`, algorithm confusion, issuer and audience;
    - `ServiceTokenTests`: TestServer end-to-end, including a caller signing for another caller, untrusted callers, user tokens on internal routes, expiry and tenant;
    - `CertificateSourceTests`.
  - `ERP.Identity.UnitTests` (`SigningKeyTests`): `kid` = JWKS, encryption certificate.
  - `ERP.Architecture.Tests`:
    - `InternalEndpointTests`;
    - `NoSharedTokenKeyTests`: the build fails if a `Jwt` key reappears in appsettings, compose, k8s or `.env` examples.
- **Not testable here:** the Key Vault source (no Azure in CI). Check the start-up log on the first
  deployment: "Certificate … current …".
