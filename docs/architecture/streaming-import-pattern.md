# Streaming import pattern (HTTP streaming + Server-Sent Events)

Added 2026-11-06 (ROADMAP M6). First users: the product import (ProductService) and the employee import (SharedMasterDataService). Business
documents: `docs/business/Product/ProductImport.md`, `docs/business/MasterData/EmployeeImport.md`.

## 1. What it is

A bulk import of a CSV file that **never holds the whole file in memory** and **tells the client what happens while it happens**:

| Part | Technology | Where |
|---|---|---|
| Upload | `POST .../import?importId=<guid>&dryRun=<bool>`, body = the raw CSV (`Content-Type: text/csv`, not multipart), read **row by row** from `Request.Body` | controller -> `CsvImportReader` |
| Result | the same response: `application/x-ndjson`, **one JSON line per row**, flushed as it is produced | `NdjsonResult` |
| Progress | `GET .../import/{importId}/events`: **Server-Sent Events** (`progress`, `completed`, `failed`), opened by the client *before* the POST | `SseResult` + `ImportProgressRegistry` |
| Template | `GET .../import/template`: the CSV header and two sample rows (UTF-8 with BOM, so Excel opens it correctly) | controller |

Building blocks live in `src/Shared/Streaming` (project `ERP.Shared.Streaming`, part of the `ERP.Shared` umbrella): `CsvImportReader`, `NdjsonResult`,
`SseResult`, `ImportProgressRegistry` / `ImportSession`, `ImportRun` (counters, lines, snapshots), `ImportBatcher`, `StreamingProxyMiddleware`.
A service adds: a MediatR `IStreamRequest<ImportLine>` + handler (the import rules), three controller actions, `AddStreamingImport(configuration)`.

## 2. The wire contract

NDJSON lines (the `type` field is the discriminator):

```json
{"type":"started","importId":"...","dryRun":false}
{"type":"row","line":2,"key":"TS-RED","status":"created","message":null}
{"type":"row","line":3,"key":"OLD-1","status":"skipped","message":"A product with this SKU already exists."}
{"type":"row","line":4,"key":"X","status":"error","message":"The name must be 3 to 255 characters."}
{"type":"summary","processed":4,"created":1,"skipped":1,"failed":2,"dryRun":false,"elapsedMs":406}
{"type":"error","message":"The import stopped: a batch could not be saved. ..."}   // only when it stopped; no summary then
```

Row status: `created`, `would-create` (dry run), `skipped` (a valid row that is not imported: it exists, it repeats an earlier row), `error` (the row is invalid).
SSE: `event: progress|completed|failed`, `data: {state, processed, created, skipped, failed, bytesRead, totalBytes, percent, elapsedMs, message}`.
The number of rows is unknown while the file arrives, so **percent = bytes of the body read / Content-Length** (null for a chunked upload).
A comment line `: connected` is sent first and `: ping` every 15 s (so proxies keep the connection).

Errors: a wrong header, a missing required column, an empty file, a missing `importId`, or a second import of the same user while one runs is a
`BusinessRuleException` = a normal **HTTP 400** ProblemDetails, raised **before the first byte of the response** (the handler reads the header and the
first row before it yields `started`; `NdjsonResult` awaits the first line before it writes the status). After the stream started the status can no
longer change: a failure becomes the final `error` line (the exception text of a server error is not sent, only a fixed sentence; it is logged).

## 3. Processing rules (every import)

* **Batches of 200 rows** (`Import:BatchSize`). Per batch: validate the rows (explicit parser, no pipeline behaviour), detect duplicates inside the file, look up
  existing keys and reference codes with **one query per kind** (`WHERE ... IN`), save, report the rows, publish a progress snapshot.
* **Valid rows are committed per batch**, invalid rows are skipped and reported. A database failure rolls the batch back and **stops** the import (error line);
  batches already committed stay committed (said in the message).
* **Dry run** (`dryRun=true`): the same validation and lookups, nothing is written; rows are `would-create`.
* Duplicates are skipped, never updated (owner decision 2026-11-06). Limits (`Import` configuration section): `MaxRows` 20 000, `MaxBytes` 25 MB, 30 MB request limit.
* The change tracker is cleared after each batch (a 20 000-row import must not keep every row in memory).
* Cancellation = the client closes the connection (`RequestAborted`): the import stops after the current batch; the SSE session is finished as `failed`.
* One running import per (tenant, user); at most 5 open sessions per user; a finished session is kept 10 minutes for a late subscriber.

## 4. The progress channel

`ImportProgressRegistry` is a **singleton in memory**, keyed by `(tenantId, userId, importId)`: another user or tenant never finds the session. The SSE request and
the POST both `GetOrCreate` the session, so the order does not matter; a subscriber that arrives late first receives the latest snapshot, one that arrives after the end
still receives the final one. Each subscriber has a small bounded queue (drops its oldest entry), so a slow reader never slows the import.
**Limit:** the two requests of one import must reach the same process (one replica today: R146).

## 5. Infrastructure - what had to change (measured 2026-11-06)

Streaming has to survive three proxies. What was found on the live stack and what was done:

| Layer | Finding | Change |
|---|---|---|
| nginx (edge `erp-nginx`, then the SPA container `erp-admin`) | buffered the whole request body before forwarding, buffered the answer, 1 MB body limit, 120 s read timeout | a regex location `^/(prd\|mst)/api/v1/(products\|masterdata/employees)/import` **before** the generic prefix block in `deployment/nginx/nginx.conf` and `src/frontend/admin/nginx.docker.conf`: `proxy_request_buffering off; proxy_buffering off; proxy_cache off; proxy_read_timeout/send_timeout 3600s; client_max_body_size 30m` |
| **Ocelot 20 (Gateway)** | **reads the whole downstream answer before it answers**: the SSE request got no headers for 90 s and then HTTP 499; the NDJSON rows all arrived at the end | `StreamingProxyMiddleware` (`ERP.Shared.Streaming`): placed in the Gateway **after** authentication and `GatewayAuthorizationMiddleware` (JWT validated, token tenant = host tenant, client `X-Tenant-*` / `X-User-*` replaced) and **before** `UseOcelot()`. It passes the request body as a `StreamContent`, sends with `ResponseHeadersRead`, and flushes every chunk of the answer. Routes are configuration (`StreamingProxy:Routes` in the Gateway `appsettings.json`). It refuses an unauthenticated call (401); the service still validates the JWT and the permission itself |
| .NET `HttpClient` over HTTP/1.1 | **half duplex**: the proxy reads the service's answer only after the request body was sent completely | accepted: the first NDJSON line (`started`) arrives when the upload is complete; everything after it (the long part: the database work) streams live, and **the progress (SSE) is live during the upload too**. A browser `fetch` with a file body is half duplex as well, so a browser would not see more |

A new import route must be added in **three** places: the service controller, `StreamingProxy:Routes` (Gateway) and the nginx regex location.

## 6. Client side (Admin web)

`src/lib/streamingFetch.ts` (`authorizedFetch` with the bearer token and one 401 refresh, `readLines`, `readSse`) and `src/lib/streamingImport.ts` (`runStreamingImport`):
open the SSE first, POST the `File` as the body, read the NDJSON answer line by line, call back for each row / progress snapshot. `EventSource` cannot send an
Authorization header and axios cannot read a response while it arrives, hence `fetch`. UI: `components/import/ImportDialog.tsx` + `components/ui/Progress.tsx`.

## 7. Security

Permissions are the existing ones (`products:manage`, `masterdata:employees:manage`): no new permission codes. The tenant is the request host (Gateway) and the JWT (service);
the progress session is private to (tenant, user). No file is stored. Problem rows echo only values of the uploaded file (shown as text; the "problem rows" download neutralises
cells that start with `=`, `+`, `-`, `@`). A client-sent `importId` is only a key: it is never trusted for authorization.
