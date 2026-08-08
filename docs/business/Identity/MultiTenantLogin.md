# Multi-Tenant Login (Subdomain-Based Tenant Resolution)

**Service:** IdentityService (Port 5001) + Gateway (Port 5000)
**Database:** AuthDB

## Purpose

Login previously required a manually-typed "Tenant Code" field alongside email/password — the
Admin app's `LoginPage.tsx` collected it and every anonymous auth endpoint took it as an explicit
body field or query parameter. This document describes the 2026-08 redesign that removes that
field: the Gateway now resolves which tenant a request belongs to from the request's subdomain
(or a client-supplied header) before it reaches IdentityService, so a human never has to type it.

## Why not resolve the tenant from email instead?

The first design considered was simpler: since `Users.Email` was globally unique at the time,
resolve the tenant by looking up the user by email alone. That approach was rejected for two
reasons found while investigating:

1. **Not realistic across independent tenants.** Requiring email to never collide across the
   *entire* platform doesn't hold for a multi-tenant ERP serving unrelated companies — two
   independent businesses can easily share a generic address (`info@...`), or the same person
   registers separately at two unrelated tenants using one personal email.
2. **It would have permanently broken an existing feature.** `UserService.InviteToTenantAsync`
   (`src/services/IdentityService/src/ERP.Identity.Infrastructure/Services/UserService.cs:199-219`)
   already lets a tenant's admin find an existing user by email and attach them to a *second*
   tenant via the `AuthUserTenant` join table. A user genuinely can belong to more than one
   tenant — resolving the tenant purely from `User.TenantId` (their original "home" tenant) at
   login would make that second tenant permanently unreachable through the login form.

Resolving the tenant from the URL (subdomain) instead of from user-supplied data solves both
problems: the tenant is known before login runs at all, and a multi-tenant user simply visits the
subdomain of whichever tenant they want to sign into.

## How it works

1. **Gateway resolves the tenant from the Host header**
   (`src/Gateway/Ocelot.API/Authorization/GatewayAuthorizationMiddleware.cs`). For anonymous
   requests to `/api/auth/login`, `/api/auth/external-login`, `/api/auth/login-options`, and the
   Twitter authorize-url endpoint, it strips the configured base domain
   (`Tenancy:BaseDomain`, e.g. `erp.local`) off the request host — `acme.erp.local` → tenant code
   `acme` — and injects an `X-Tenant-Code` header before forwarding to IdentityService.
2. **A client-supplied header always wins.** If the request already carries `X-Tenant-Code` (set
   by the client itself, not the browser's URL), the Gateway never overrides it. This is how
   Storefront works: it's deployed one instance per tenant, so it sends its own fixed tenant code
   from `VITE_TENANT_CODE` on every request (`src/frontend/storefront/src/api/client.ts`) rather
   than depending on subdomain routing.
3. **IdentityService reads the header, not the request body.** `AuthController`'s
   `TryGetTenantCode()` helper reads `X-Tenant-Code` and returns `400 Bad Request` if it's
   missing — there is no silent fallback to a default tenant.
4. **`AuthService` logic is otherwise unchanged**: it still resolves the `AuthTenant` first
   (`GetTenantByCodeAsync`), then the `User` within that tenant, exactly as before — only where
   the tenant code comes from changed.

## Affected endpoints

| Endpoint | Before | After |
|---|---|---|
| `POST /api/auth/login` | `{ email, password, tenantCode, userType }` | `{ email, password, userType }` + `X-Tenant-Code` header |
| `POST /api/auth/external-login` | `{ provider, providerKey, idToken, tenantCode, userType }` | `{ provider, providerKey, idToken, userType }` + `X-Tenant-Code` header |
| `GET /api/auth/login-options` | `?tenantCode=` query param | `X-Tenant-Code` header |
| `GET /api/auth/external-login/twitter/authorize-url` | `?tenantCode=` query param | `X-Tenant-Code` header |
| `POST /api/auth/register` | `{ ..., tenantCode }` | **Unchanged** — still an explicit body field |

`Register` deliberately did not move to header-based resolution: creating a brand-new account
must state which tenant it's being created in, which is not something a shared login subdomain
can imply on its own (Storefront's fixed `VITE_TENANT_CODE` continues to supply it in the body).

## `Users.Email` uniqueness: global → per-tenant

`Users.Email` moved from a single globally-unique index to a composite unique index on
`(Email, TenantId)` — migration
`src/Database/Identity/014_ChangeEmailUniqueIndexToPerTenant.sql`. Two independent tenants are
now allowed to register the same email address, matching how real multi-tenant SaaS platforms
behave. `AuthService.RegisterAsync`'s duplicate-email check moved with it: it now rejects a
duplicate only within the *target* tenant (`GetByEmailAsync(email, tenant.Id)`), not globally
(`GetByEmailGloballyAsync`, previously used for this check, is now only used by
`InviteToTenantAsync` — where "does this email exist anywhere on the platform" is exactly the
right question to ask).

## Fixed along the way: invited users couldn't log into the tenant they were invited to

`UserRepository.GetByEmailAsync(email, tenantId)` previously matched only on `User.TenantId` (the
user's original home tenant), ignoring the `AuthUserTenant` membership table entirely. A user
invited into a second tenant via `InviteToTenantAsync` therefore had roles and permissions
assigned there but could never actually log in — the lookup used by login only ever found them
in their home tenant. The query now matches via `AuthUserTenant` membership
(`x.UserTenants.Any(ut => ut.TenantId == tenantId)`), the same pattern already used by
`GetByIdInTenantAsync`/`ListByTenantAsync`/`IsInTenantAsync` — login now works on every tenant
the user is actually a member of, visited via that tenant's own subdomain.

## JWT/RBAC stays correctly scoped

Once the tenant is resolved (from the header, once, at the top of the request), `AuthService`
threads that single `AuthTenant` through every downstream call in `IssueTokensAsync` —
`GetRolesAsync(user.Id, tenant.Id)`, `GetPermissionAsync(user.Id, tenant.Id)`,
`GetEffectiveModulesAsync(tenant.Id)`, `GenerateAccessToken(user, tenant, ...)`,
`GenerateRefreshToken(user.Id, tenant.Id)`. Because every one of those calls is filtered by the
same `tenant.Id` value, a user with membership in multiple tenants always receives the JWT
claims (`tenant_id`, roles, permissions, licensed modules) for the one tenant they actually
signed into — never a mix of two tenants' data in the same token.

## Related

- [ExternalAuthAndTwoFactor.md](ExternalAuthAndTwoFactor.md) — SSO/2FA settings gated per tenant;
  its `login-options` endpoint now resolves the tenant the same way described here.
- [module-02-identity-security.md](module-02-identity-security.md) — `Tenant`/`User`/`Role`
  domain model this document assumes.
- `docs/architecture/database-map.md` — `Users` table shape, including the `(Email, TenantId)`
  index.
- `docs/architecture/end-to-end-request-flow.md` §2 — full UI → Gateway → Controller trace for
  login, updated for this change.
- `docs/deployment/manual-build-deploy-guide.md` — `Tenancy:BaseDomain` Gateway config and how to
  test multiple tenant subdomains locally.
