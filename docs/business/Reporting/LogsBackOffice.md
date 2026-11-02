# Logging / Reporting — Admin screen: System logs

Business analysis of the **System logs** screen as shipped on 2026-11-02 (ROADMAP P2-9, U3 batch 8). The log pipeline (Serilog Kafka sink → LoggingService → Elasticsearch `service-logs*`, anonymous `POST logs/client` for frontend apps) is in the Logging Service section of CLAUDE.md. Vietnamese user guide: [docs/user-guides/payments-shipping-sales-logs.md](../../user-guides/payments-shipping-sales-logs.md).

## 0. Which services write here (R101, 2026-11-02)
Every backend service writes one **request line** per HTTP request ("HTTP GET /x responded 400 in 12 ms") through the shared `UseErpRequestLogging()` and ships its logs to Kafka `system-logs-topic` with `AddSerilogLogging` (or the equivalent host setup). The line is written outside the exception handler, so it carries the status the client received. Levels: 5xx **Error**, the container `/health` probe is not logged, everything else **Information**. Before R101 Identity, Product, Payment, Shipping, POS, Promotion, Notification, ChatBot, Reporting, Sales and Purchasing wrote no request line (nine of them did not ship logs at all).

## 1. Purpose and scope
Let a **platform administrator** search what every service and frontend app logged and read one entry in full (message, exception, stack trace, properties). Read-only. Out of scope: export, saved searches, live tail, tenant-level logs.

## 2. Actors and permissions
Platform administrators only: `logs:view` is a platform permission (logs carry no tenant — they contain every tenant's requests and traces). Without it the page redirects to the dashboard.

## 3. Rules
- Filters: service (suggestions from a fixed list), level (Information, Warning, Error, Critical), kind (Trace, Exception), from / to (local date-time converted to UTC), keyword (message or exception, applied on Enter), request id (deep link from Monitoring, shown as a removable banner). **Filters live in the URL** so a link can be shared. 25 rows per page; changing a filter returns to page 1.
- **Detail** now opens as a centred dialog (it was a right-hand drawer without focus handling): level badge, time to the millisecond, service and environment, exception type, correlation id, message, stack trace, properties. Esc / Close return to the list.
- Rows are opened with a **View** button (keyboard reachable); the whole-row click of the drawer version is gone.

## 4. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| LG-1 | Platform only | No `logs:view` → redirect to the dashboard |
| LG-2 | Detail is a real dialog | Focus trap, scroll lock, Esc closes, axe clean; long stack traces scroll inside |
| LG-3 | Filters are labelled and shareable | Every filter has a label; the URL query holds them; **Clear** (empty result) resets all of them |
| LG-4 | Level is not colour only | Level shown as a badge with its name |

## 5. Use cases
**UC-1 Investigate an error:** Monitoring → deep link with `requestId` → View the entry → read exception and stack trace. **UC-2 Browse a time window:** set From / To, level Error.

## 6. Data
LogEntry(Id, ServiceName, Environment, Level, Kind, Message, ExceptionType, StackTrace, CorrelationId, Properties, OccurredAt).

## 7. Open issues (DEFERRED R95)
The service suggestion list is hard-coded; no export; the "no result" Clear button was exercised only by the automatic test (the walk script's selector skipped table buttons).

## 8. UAT scenarios
1. As a user without `logs:view` open /logs → dashboard. 2. As platform admin open /logs → rows → **View** → dialog → Esc. 3. Level Error + keyword with no hit → "Không có kết quả" + Xóa lọc → all filters reset and the URL query is empty.
