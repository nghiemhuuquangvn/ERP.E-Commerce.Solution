# LAN test kit (R68: run the mobile apps on a real phone)

Step-by-step guide for the owner (Vietnamese): [docs/user-guides/mobile-device-test.md](../../../docs/user-guides/mobile-device-test.md).

| File | What it does | Who runs it |
|---|---|---|
| `allow-lan-http.ps1` | opens inbound TCP 80 on the **Private** network profile only (`-Remove` closes it); changes Windows Firewall | the owner, elevated PowerShell |
| `check-from-lan.js` | prints this PC's LAN addresses and checks that `http://<ip>` with `Host: shop.glcheck.lan` answers as the same shop as `glcheck.shop.erp.local`. Run from this PC it cannot prove the firewall (local traffic skips it): open `http://shop.glcheck.lan` in the phone's browser for that | anyone |
| `make-barcode-page.js` | writes an HTML page with the test SKU as a Code 128 barcode and a QR code, to scan with the staff app (libraries from cdnjs at view time) | anyone |
| `db-snapshot.js` | `save` / `diff`: row counts of every table of every service database (read-only), to see exactly what one sale wrote | the assistant (and anyone) |

Notes
- The phone must resolve `shop.glcheck.lan` (a router DNS record). Do not use `*.local`: Android resolves it by mDNS.
- The tenant of `shop.glcheck.lan` is decided by a `TenantDomains` row (`POST id/api/v1/tenants/{id}/domains`, platform admin). The local docker-compose sets `Tenancy__FallbackTenantCode=SYSTEM`, so an **unknown host answers as the platform tenant, not 404** - always compare data, as `check-from-lan.js` does.
- Nothing here edits the Windows hosts file.
