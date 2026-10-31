# Admin: Shipping returns, Notification webhooks and templates (P2-1b)

Pages added or extended 2026-10-31. Business description: `docs/business/Shipping/ReturnsBackOffice.md`, `docs/business/Notification/WebhooksAndTemplates.md`.

| Page (`src/pages/…`) | Route | Permission |
|---|---|---|
| `shipping/ReturnsPage.tsx` | `/shipping/returns` | list `shipping:view`; book again / cancel / received `shipping:update` |
| `settings/NotificationWebhooksPage.tsx` | `/settings/notification-webhooks` | `notifications:webhooks:manage` |
| `settings/NotificationTemplatesPage.tsx` (extended) | `/settings/notification-templates` | read `notifications:view`; change `notifications:templates:manage` |

## API clients
- `api/shipping.api.ts`: `listReturns`, `rebookReturn`, `cancelReturn` (cancels at the carrier first; a refusal is an HTTP 400 whose `detail` the page shows), `receiveReturn` (existing). Lists answer `{items, totalCount, pageNumber, pageSize}`.
- `api/notifications/webhooks.api.ts` (base `/ntf/api/v1/webhooks`): `list`, `eventTypes`, `create`, `update` (empty `secret` keeps it, `clearSecret` removes it), `remove`, `setActive`, `test`, `deliveries`. **The secret is write-only**: the API returns `hasSecret`.
- `api/notifications/notificationAdmin.api.ts`: `createTemplate` (channel is numeric: `TEMPLATE_CHANNELS`), `updateTemplate`, `setTemplateActive`, `overrideTemplate`, `deleteTemplate`, `preview`.

## Patterns worth reusing
- **Server messages:** `serverMessage(error, fallback)` reads the problem-details `detail` of an HTTP 400 business rule; every action of these pages shows it in a `role="alert"` box.
- **Write-only secret:** an input of type `password` with `autoComplete="new-password"`, a placeholder telling the secret is stored, and a separate "remove the stored secret" checkbox.
- **Live preview:** a 400 ms debounced call to `templates/preview` with the declared variables as sample values; placeholder chips insert `{name}` at the caret.
- **Platform vs shop rows:** `isPlatformDefault` rows are read-only for a shop (the action is *Override*); the platform tenant (`user.tenantId === 00000000-0000-0000-0000-000000000001`) edits them directly.
- **Status badges:** `Requested` and `Received` were added to `StatusBadge` (return parcels).

## i18n
`shipments.returns.*`, `nav.shippingReturns`, `nav.notificationWebhooks`, `notificationAdmin.webhooks.*`, `notificationAdmin.templates.*` in `en.json` and `vi.json`.
