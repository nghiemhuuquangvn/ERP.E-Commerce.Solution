# Mobile apps (customer and staff) — connecting to a tenant

Both Expo apps (`src/frontend/mobile/customer`, `src/frontend/mobile/staff`) talk to the platform through the API Gateway.
**The Gateway decides the tenant from the host of the request** (CLAUDE.md, "Tenant = request host"): `<code>.shop.erp.local`
for a shop's storefront, `<code>.erp.local` for its back office, a registered custom domain, or the platform host.

## Setting the address

1. Copy `.env.example` to `.env` in the app folder.
2. Set `EXPO_PUBLIC_API_URL` to the tenant's host **with `/api`**, e.g. `http://glcheck.shop.erp.local/api` (customer app) or
   `http://glcheck.erp.local/api` (staff app). In production this is the shop's real domain, `https://shop.example.com/api`.
3. Restart Expo with a cleared cache (`npx expo start -c`) — `EXPO_PUBLIC_*` values are inlined when the bundle is built.

## Why `localhost` does not work

The default (`http://localhost:5000/api`) is only a placeholder: `localhost` is the host of no tenant, so the Gateway refuses
sign-in, ordering and payment calls. The apps print a console warning when the variable is missing.

## On a phone or an emulator

- The phone must resolve the tenant host to the computer running Docker. Android emulator: `10.0.2.2` is the host machine, but the
  `Host` header must still be the tenant's host — easiest is to map the tenant host to the computer's LAN address in the router's DNS
  (or the phone's hosts file) and use that host name in `EXPO_PUBLIC_API_URL`.
- Payment gateways (MoMo, ZaloPay, VNPay) return the customer to `Payment:PublicBaseUrl` / the request host. For a real end-to-end test
  with a provider's sandbox the stack must be reachable from the internet (tunnel) — see `docs/business/Payment/OnlinePayments.md`.

## Contract guard

`tests/unit/ERP.Architecture.Tests/ClientApiContractTests.cs` fails when the storefront or the customer app calls a `/v1/...` route that no
controller serves (wrong path or HTTP method). Add new calls through `apiClient` with a literal path so the guard can see them.
Field names inside requests/responses are not checked by it — keep the typed clients next to the backend DTOs.
