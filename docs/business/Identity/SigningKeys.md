# Access token signing keys (RS256 / JWKS)

Since phase 4 of the service authorization work (2026-10-20), IdentityService signs every access
token with an **RSA certificate whose private key only Identity holds**. Every other service and
the Gateway validate with the **public** keys Identity publishes at `/.well-known/jwks.json`.
Before this change every service held the shared HS256 key (`Jwt:Key`) and could mint tokens for
any user or tenant (SecurityReview S6, M1, M2).

## How it fits together

| Piece | Where | What it does |
|---|---|---|
| Signing certificates | Identity `Infrastructure/Security/Signing` | `SigningKeys:Source` = `KeyVault` (production) or `File` (local / Compose). Loaded once at start-up; missing or unusable configuration stops Identity. |
| Login tokens | Identity `TokenService` + `AccessTokenSigningCredentials` | RS256 with the **current** certificate; `kid` = the key id OpenIddict publishes. |
| OpenIddict tokens | Identity `Program.cs` | Signed with the same certificates. The current one is registered first, and **previous** ones too, so they stay in the JWKS. |
| Validation | `ERP.Shared.Authorization.ErpJwtBearerOptions` (every service via `AddErpJwtAuthentication`, the Gateway via `GatewayJwtBearer`) | Fetches the JWKS from `Jwt:Authority` (default `http://identityservice:5001`, internal network). The discovery document is **not** used. Keys are re-fetched on an unknown `kid` and retried every 30 s if the fetch fails. Only `RS256` is allowed (plus `HS256` while `Jwt:Key` is configured). Issuer must be exactly `Jwt:Issuer`; audience must be `Jwt:Audience`. The phase-3 revocation check is unchanged. |

**Only login tokens are accepted outside Identity** (issuer `ERP.Identity`). Tokens issued by
OpenIddict (issuer `urn:ERP.Identity`: client_credentials and authorization code) still work only
in IdentityService. The reason: client_credentials permissions come straight from the
application's properties and do not go through `EffectivePermissions`. Opening this up is
follow-up work.

## Configuration

Identity:

| Key | Production (k8s) | Local / Compose |
|---|---|---|
| `SigningKeys:Source` | `KeyVault` | `File` |
| `SigningKeys:KeyVault:VaultUri` / `CertificateName` | the vault / `identity-token-signing` | — |
| `SigningKeys:File:Path` | — | `/app/signing/identity-signing.pfx` (volume `identity_signing`); `signing/identity-signing.pfx` under the API project in Development |
| `SigningKeys:File:AutoCreate` | — | `true`: a self-signed RSA 2048 certificate valid for 2 years is generated once. It must be on a persistent volume, otherwise every re-deploy rotates the key. |
| `SigningKeys:File:Password`, `PreviousPaths[]` | — | optional |
| `Jwt:AccessTokenSigning` | `Hmac` during the cutover, then `Rsa` | `Rsa` |
| `Jwt:Key` | still required (OpenIddict encryption key, transition) | same |

Every other service and the Gateway:

- `Jwt:Authority`: the internal Identity URL.
- `Jwt:RequireHttpsMetadata`: `false` for in-cluster / Compose http; `true` by default.
- `Jwt:Issuer` and `Jwt:Audience`: unchanged.
- `Jwt:Key`: optional. Only during the transition.

Key Vault access uses `DefaultAzureCredential`. In AKS this is Workload Identity: the
`identity-service` ServiceAccount in `deployment/k8s/deployments/identity.yaml`. The managed
identity needs **certificates/list** and **secrets/get** on the vault, because the private key is
downloaded as the certificate's secret.

## Cutover from HS256 (two releases)

Access tokens live `Jwt:Expired` minutes (60). Refresh tokens are opaque database rows, so the
cutover does not affect them.

1. **Release 1 (this change), Identity on `Hmac`.** Deploy everything. Services now accept RS256
   *and* HS256, and Identity still signs HS256. Nothing changes for users. This order matters
   on k8s: a service that has not been updated yet would reject RS256 tokens.
2. **Switch Identity to `Rsa`** (`Jwt__AccessTokenSigning=Rsa`, restart Identity) once every pod
   runs release 1. New tokens are RS256. Old HS256 tokens remain valid until they expire.
3. **Release 2 (separate PR, not done yet)**, at least `Jwt:Expired` minutes after step 2:
   - remove `Jwt__Key` from every service and the Gateway (compose, k8s, `appsettings*.json`
     placeholders);
   - remove the HS256 path from `ErpJwtBearerOptions` and the `Hmac` mode;
   - only Identity keeps `Jwt:Key` (OpenIddict encryption key).

Docker Compose runs everything at once, so it uses `Rsa` from the start.

## Rotating the certificate

**Key Vault.**
1. Create a new version of the certificate, or let Key Vault auto-renew it.
2. Restart Identity (all replicas). The newest enabled, currently valid version signs, and older
   enabled versions stay in the JWKS.
3. Services pick up the new `kid` automatically: an unknown `kid` triggers a JWKS refresh.
4. After at least `Jwt:Expired` minutes, **disable** the old version and restart Identity again,
   so it disappears from the JWKS.

**File.**
1. Put the new PFX at `SigningKeys:File:Path`.
2. Add the old file to `SigningKeys:File:PreviousPaths`.
3. Restart Identity.
4. After `Jwt:Expired` minutes, remove the old file from `PreviousPaths` and restart again.

**Emergency (private key leaked).**
1. Disable the leaked version **and** create a new one.
2. Restart Identity.
3. Every token signed with the leaked key stops validating as soon as services refresh the JWKS.
   Users silently refresh to a new token, because refresh tokens are unaffected.

## Verifying

- `curl http://identityservice:5001/.well-known/jwks.json` (inside the network) shows one key per
  certificate.
- Decode a login token: the header shows `"alg":"RS256"` and a `kid` that is listed in the JWKS.
- `tests/unit/ERP.Shared.UnitTests` (`ErpJwtBearerOptionsTests`), `ERP.Identity.UnitTests`
  (`SigningKeyTests`) and `ERP.Gateway.UnitTests` (`GatewayJwtBearerTests`) cover:
  - algorithm confusion, unsigned tokens, foreign keys, wrong issuer and audience;
  - the `kid` matching the JWKS;
  - certificate selection and the File source.
- The Key Vault source has no automated test (no Azure in CI). Verify it on the first deployment:
  the Identity start-up log reads "Token signing certificate … current …".
