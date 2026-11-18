# Browser security — CORS and response headers

Security review 2026-11, wave S4a (SEC-M14). Owner decision 2026-11-18: **CSP starts Report-Only**.

## CORS

- **Why none is needed:**
  - The web apps (Admin, Storefront) call the API on **their own host**: the SPA nginx proxies `/api`, `/<svc>/api` and `/hubs/*`, so no browser request is cross-origin.
  - The mobile apps are native and do not use CORS.
- **The rule:** every service registers the shared policy `AddErpCors(builder.Configuration)` / `UseErpCors()` (`src/Shared/Extensions/CorsExtensions.cs`).
  - It allows only the origins listed in `Cors:AllowedOrigins`, which is empty by default: no cross-origin call at all.
  - `NoServiceAllowsEveryOrigin` (`InfrastructureSecurityTests`) fails the build on `AllowAnyOrigin`, `WithOrigins("*")` or `SetIsOriginAllowed(_ => true)`.
- **Exceptions:**
  - IdentityService has its own allow-list (`IdentityCors`, same key).
  - The ChatBot / Notification hubs keep their scoped credentialed policies.
- **Adding an origin** (e.g. a partner site embedding a widget): put it in `Cors:AllowedOrigins` of the service, never a wildcard.

## Response headers (SPA nginx)

`src/frontend/{admin,storefront}/nginx.security-headers.conf`:
- the two copies are identical;
- the Dockerfile copies the file to `/etc/nginx/snippets/` and `nginx.docker.conf` includes it in the `server` block;
- `BothSpasSendTheBrowserSecurityHeaders` checks all of this.

| Header | Value | Why |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | No MIME sniffing of uploads / responses |
| `X-Frame-Options` | `DENY` (+ CSP `frame-ancestors 'none'`) | No clickjacking |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | No paths or queries leak to other sites |
| `Permissions-Policy` | `camera=(self), microphone=(), geolocation=(), payment=()` | The camera is for barcode scanning only |
| `Cross-Origin-Opener-Policy` | `same-origin-allow-popups` | Isolation, while Google / Microsoft popups still answer |
| `Content-Security-Policy-Report-Only` | see the file | Violations are reported in the browser console, nothing is blocked yet |
| `server_tokens off` | | No nginx version |

**Enforcing the CSP:**
1. Walk through the Admin and the Storefront (sign-in with Google / Microsoft / Facebook, product images, chat, notifications, imports) with the console open.
2. Add any missing source.
3. Rename the header to `Content-Security-Policy` in both copies.

A location that sets its own `add_header` must `include` the snippet again: nginx drops inherited `add_header` lines in such a location.

**HSTS** is set by the TLS ingress (`deployment/k8s/ingress.yaml`: ingress-nginx sends it once a `tls:` section exists), never by the SPA nginx, which also serves the plain-http local stack.
