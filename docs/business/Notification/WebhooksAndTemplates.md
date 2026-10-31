# Notification webhooks and message templates in the Admin app (ROADMAP P2-1b, parts B and C)

Branch `feat/p2-1b-returns-webhooks-templates` (2026-10-31). Migration Notification 014. Builds on [CustomerNotifications.md](CustomerNotifications.md) (what is sent and when) and [module-21-notification.md](module-21-notification.md).

User guides for staff (Vietnamese): [notification-webhooks.md](../../user-guides/notification-webhooks.md), [notification-templates.md](../../user-guides/notification-templates.md).

## 1. Purpose and scope
**Webhooks.** A shop can have Notification POST a signed JSON message to its own HTTPS endpoint when a notification is sent or fails. The API existed but had no screen, kept the secret in plain text, wiped it whenever an edit left it out, accepted any address (including the platform's own internal network), recorded nothing about deliveries and could not be tested. **Templates.** The wording of every automatic message could be read but not changed. This task gives the shop a Webhooks screen and a Templates editor, and hardens the backend behind them.

Out of scope (§8): retry / auto-disable of failing endpoints, sending a template as a real test message, more webhook events than notification sent / failed, per-language templates, platform-tenant template editing screens beyond the same page.

## 2. Actors
| Actor | Does |
|---|---|
| Shop administrator (`notifications:webhooks:manage`) | Registers, edits, tests and deletes webhooks; reads the delivery log |
| Shop content editor (`notifications:templates:manage`) | Overrides platform templates, edits and switches off its own, creates new ones |
| Shop staff (`notifications:view`) | Reads the templates |
| Platform administrator (platform tenant + `notifications:templates:manage`) | Edits and switches off the platform's default templates directly |
| Receiving system of the shop | Verifies `X-Hub-Signature`, answers 2xx |

## 3. Requirements and acceptance criteria — webhooks
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-W1 | Only public HTTPS addresses. | Register / edit refuse `http`, `localhost`, loopback, private (10/172.16/192.168), link-local and cloud-metadata (169.254.169.254) addresses, single-label host names (Docker service names) and credentials in the URL, with HTTP 400. The same rule runs again when sending and, at connect time, against the addresses the host name resolves to; redirects are not followed. |
| FR-W2 | The secret is sealed and never shown. | Stored with `enc:v1:`; the API returns only `hasSecret`; an old plaintext row keeps working and is sealed on its next save. |
| FR-W3 | An edit must not wipe the secret. | PUT without a secret keeps the stored one; a new value replaces it; `clearSecret: true` removes it. |
| FR-W4 | Subscribe to known events only. | `GET webhooks/event-types` = `notification.sent`, `notification.failed`; an empty list means all; an unknown name is HTTP 400. `notification.failed` is now really sent (it used to be documented only). |
| FR-W5 | Test button. | `POST webhooks/{id}/test` sends a sample `webhook.test` event through the normal sender and returns `{success, statusCode, durationMs, error}` — also for an inactive endpoint. |
| FR-W6 | Delivery log. | Every attempt (event, success, HTTP status, error, duration) is stored; `GET webhooks/{id}/deliveries` pages it newest first (page size ≤ 100); the list shows each endpoint's last delivery; 30 days are kept. |
| FR-W7 | Clean errors. | Unknown id → 404 for get-related actions, update, delete, test and deliveries (delete used to answer 204 silently); bad name / URL / secret length → 400. |
| FR-W8 | Screen. | *Settings → Webhooks*: table (name, URL, events, signed, last delivery, active switch), create / edit dialog (secret field says "stored — leave empty to keep", *Remove the stored secret*, events from the catalog or all), *Send test* with a result dialog, *Deliveries* dialog, delete with confirmation. |

## 4. Requirements and acceptance criteria — templates
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-T1 | Edit a template. | `PUT templates/{id}` changes name, subject, body and variables (tidied: trimmed, no repeats); code and channel never change. |
| FR-T2 | A shop never changes the platform default; it overrides it. | Update / switch off / delete of a platform row by a shop → HTTP 400 "Override it"; `POST templates/{id}/override` copies the default into the shop's own template (same code); again → 400; the platform tenant edits platform rows directly. |
| FR-T3 | Delete = go back to the default. | Deleting the shop's own template removes it and the platform default is used again; a platform template is never deleted (switch it off). |
| FR-T4 | Switch a message off on purpose. | `activate` / `deactivate` on the shop's own template; a deactivated template is no longer used when sending: the message is **skipped** (status "Skipped"), not an error; if the shop's copy is off, the platform default is used. |
| FR-T5 | Create. | `POST templates`: code 2–100 of capitals, digits, `_` (lower case is upper-cased); name and text required; a code the shop already has → 400; a code only the platform has is allowed (it behaves as an override). |
| FR-T6 | Preview. | `POST templates/preview` renders subject and body with sample values (case-insensitive `{name}`) and lists the placeholders no sample covers. |
| FR-T7 | Screen. | *Settings → Message templates*: *New template*, row actions Edit / View, Override, Switch off / on, Delete (back to default); editor with code and channel locked on edit, clickable placeholder chips inserting `{name}`, live preview from the server and a warning for unknown placeholders. |

## 5. Business rules
1. Webhook payload of `notification.sent` / `notification.failed`: `{Id, RecipientId, RecipientEmail, Channel, Subject, Status, SentAt, Error, RetryCount}`; headers `X-Event-Type`, `X-Timestamp`, and `X-Hub-Signature: sha256=HMAC_SHA256(secret, "<timestamp>.<json>")` when a secret is set.
2. A failing endpoint never blocks sending a notification (the failure is only logged in the delivery log).
3. A template is "the shop's own" when its tenant is the shop; the shop's active row wins over the platform's.
4. Webhooks and templates are per shop; a shop never sees another shop's.

## 6. Use cases
**UC-W1 Connect a system (main).** The administrator adds `https://erp-hooks.shop.vn/ntf` with a secret and *All events*, clicks *Send test*, sees "HTTP 200, 80 ms" and the delivery in the log.
**UC-W2 Internal address (exception).** Someone types `https://10.0.0.5/x`: the dialog shows "must be a public https address".
**UC-W3 Rotate the secret (alternative).** Edit, type a new secret; the old one is replaced; the field never shows the old value.
**UC-T1 Change a message (main).** The editor opens *Order shipped* (platform default), clicks *Override*, edits the copy with the chips and sees the preview; the next shipped-order e-mail uses it.
**UC-T2 Go back (alternative).** *Delete / back to default* on the copy: the default wording returns.
**UC-T3 Turn a message off (alternative).** The shop does not want a message at all: only a template with no active row is skipped, and the platform default stays active, so the shop's own copy switched off only makes the default apply again. Switching a message off for good needs the platform tenant to switch the default off (R66).

## 7. Data and integrations
Tables: `WebhookEndpoints` (Secret now sealed), new `WebhookDeliveries` (tenant-owned, index `(TenantId, WebhookEndpointId, CreatedAt DESC)`, migration 014, purged after 30 days by `NotificationDispatchJob`); `NotificationTemplates` unchanged. `WebhookUrlRules` moved from Identity to `ERP.Shared.Security` and is used by both. Gateway prefix `/ntf/api/v1`. Permissions: `notifications:webhooks:manage`, `notifications:templates:manage` (reads `notifications:view`); both controllers now use attribute authorization (the in-action allowlist shrank by two).

## 8. UAT (all passed live on tenant `glcheck`; test data removed afterwards)
| # | Scenario | Result |
|---|---|---|
| W1 | Register `http://…`, `https://localhost`, `127.0.0.1`, `10.0.0.5`, `169.254.169.254`, `sqlserver`; unknown event | all 400 |
| W2 | Register `https://example.com/erp-hook` with a secret | 201, `hasSecret` true, the secret is not in the body, the stored value starts `enc:v1:` |
| W3 | Edit without a secret | 200, stored value byte-identical |
| W4 | *Send test* | 200 `{success:false, statusCode:405}` (example.com does not accept POST) and a delivery row; list shows `lastDelivery` |
| W5 | After one *Send test* click the log held 3 rows | the first is `webhook.test` (HTTP 405); the other two came from real notification events of the test orders that ran meanwhile (their event type was not examined) |
| W6 | `clearSecret`; deactivate / activate | `hasSecret` false; 200 / 200 |
| W7 | Delete; delete again; deliveries of the deleted one | 204; 404; 404; 0 log rows left |
| T1 | Edit / delete / switch off the platform `ORDER_SHIPPED` as a shop | 400 ×3 |
| T2 | Override; override again | 200 (own copy); 400 |
| T3 | Edit the copy with an undeclared `{oops}`; preview | 200; `unknownPlaceholders:["oops"]` |
| T4 | Switch the copy off and on | false / true |
| T5 | Create `p21b_gift`; duplicate; code `a` | 201 `P21B_GIFT`; 400; 400 |
| T6 | Delete the copy and the new one | 204 / 204; the list shows the platform default again |
Not done: a browser walk-through of the two screens; a delivery to an endpoint that answers 2xx (no public receiver available); the connect-time check against a name that resolves to a private address (the rule is unit-tested; the live address checks are the static ones).

## 9. Open issues
R61 (plaintext secrets until re-saved), R62 (no retry or auto-disable of a failing endpoint), R63 (no test-send of a template), R64 (the platform tenant edits platform templates in the same screen; no history of template edits), R65 (delivery log kept 30 days only), R66 (a switched-off template skips silently — no warning on the screen for codes the system needs).
