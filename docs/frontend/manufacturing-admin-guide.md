# Admin: Manufacturing screens (M5)

Added 2026-11-07. Business descriptions: `docs/business/Manufacturing/` (`PlantSettingsItemProfile.md`, `CalendarsWorkCenters.md`, `Bom.md`, `Routing.md`; each has a "Screens" section). Backend: ManufacturingService through the Gateway `/mfg/api/v1/manufacturing/...`.

## Pages (`src/pages/manufacturing/`)
| Page | Route | Permissions (read / change) |
|---|---|---|
| `BomPages.tsx` `BomListPage` | `/manufacturing/boms` | `manufacturing:bom:view` / `bom:manage` |
| `BomPages.tsx` `BomEditorPage` | `/manufacturing/boms/:id` | view / manage (edit draft, copy) / `bom:approve` (approve, obsolete) |
| `BomPages.tsx` `WhereUsedPage` | `/manufacturing/where-used?itemId=` | `bom:view` |
| `RoutingPages.tsx` `RoutingListPage`, `RoutingEditorPage` | `/manufacturing/routings`, `/:id` | `routing:view` / `routing:manage` / `routing:approve` |
| `PlantsPage.tsx` (plants + who may work there) | `/manufacturing/plants` | `masterdata:view` / `masterdata:manage`; access grants `settings:manage` |
| `SettingsPage.tsx` | `/manufacturing/settings` | `masterdata:view` / `settings:manage` |
| `ItemProfilesPage.tsx` | `/manufacturing/item-profiles` | `masterdata:view` / `item-profile:manage` |
| `CalendarsPage.tsx` (shifts, calendars, exceptions, month view, calculator) | `/manufacturing/calendars` | `masterdata:view` / `masterdata:manage` |
| `WorkCentersPage.tsx` (with capacity chart) | `/manufacturing/work-centers` | `masterdata:view` / `masterdata:manage` |
| `CatalogsPage.tsx` (tabs: machines, standard operations, reason codes, crews + members) | `/manufacturing/catalogs` | `masterdata:view` / `masterdata:manage` |
| `OrderListPage.tsx` `ProductionOrderListPage` (S15) | `/manufacturing/orders` | `order:view` / `order:create`, `order:release` (bulk release) |
| `OrderDetailPage.tsx` `ProductionOrderDetailPage` (S16) | `/manufacturing/orders/:id` | `order:view`; every action by its own permission (see `orderActions.ts`) |
| `StockDocumentsPage.tsx` | `/manufacturing/stock-documents` | `order:view` / `stock-document:retry`, `qc:disposition` |

The sidebar group **Manufacturing** (`MANUFACTURING_CHILDREN` in `components/layout/Sidebar.tsx`) shows each entry by its permission; like the other modules it is not hidden by the tenant's license.

## Building blocks
- `api/manufacturing/mfg.api.ts`: one typed `mfgApi` object (plants ... routing, CSV import) on `gatewayClient`; enums are strings both ways; `downloadTemplate` fetches a CSV template with the token.
- `components/manufacturing/shared.tsx`: `usePlantContext` / `PlantSelector` (the plant is kept in `localStorage` key `erp-mfg-plant`; every list is per plant; a user sees only the plants they may see), `NoPlant`, `PageHeader`, `VersionBadge`, `CsvImportDialog`.
- `components/manufacturing/BomTree.tsx`: the multi-level BOM as an ARIA tree (expand / collapse each branch or all, phantom badge, alternative-group badge, substitutes struck through).
- `components/manufacturing/reorder.ts`: `reorderSeqs(seqs, from, to)` - the order sent to `POST routings/{id}/renumber`.

## Production order screens (P3-6, 2026-11-10)
Business description: `docs/business/Manufacturing/ProductionOrderScreens.md`. Menu: *Production orders* and *Stock documents* at the top of the Manufacturing group (permission `manufacturing:order:view`).

Building blocks (`components/manufacturing/`):
- `orderActions.ts`: the pure function `orderActions(status, hasPermission)` that decides every button of S16 (TC-UI-001), the permission codes (`ORDER_PERMISSION`) and `errorCodeOf(e)` (the `errorCode` of a refusal). Add a new button here and in its table test first.
- `OrderBadges.tsx` (status badges on `STATUS_ROLE`, the Late flag as text + icon), `ReasonCodeDialog.tsx` (a reason **code** of one type + note), `StockDocumentsTable.tsx` (documents with lines, Send now / Abandon / quality release), `OrderFormModal.tsx` (new order / change a Draft), `IssueReturnDialog.tsx` (S18), `ReceiptDialog.tsx` (S19), `CloseOrderDialog.tsx`, `OperationDialogs.tsx` (start, add time, report), `OrderTabs.tsx` (Materials, Operations, Finished goods, Documents, History).
- `shared.tsx`: `useIdempotencyKey()` (one key per request body), `newIdempotencyKey`, `fmtDate`, `fmtDateTime`.
- `api/manufacturing/mfg.api.ts`: the order types and calls; `postDocument` returns `{ document, pending }` (pending = HTTP 202).

Patterns:
- **202 is not an error.** The document is stored and the Warehouse is told again later: the dialog closes with an info toast, the order header shows "N stock documents are being sent" (`pendingStockDocuments`), and the detail query refetches every 5 s only while `pendingStockDocuments > 0` or `reservationSyncPending`. An issue / return is *refused* (400 `MFG_INT_001`) while the Warehouse is down; a plain receipt is accepted as pending.
- **One Idempotency-Key per request body** (`useIdempotencyKey`): the same body keeps its key (double click, retry after a timeout), a changed body gets a new one (the server answers 409 `MFG_GEN_007` for one key and two bodies); a dialog opened again starts fresh.
- **React to codes only where the flow needs it:** `MFG_PO_012` (complete below the tolerance) opens the short-close reason dialog; `MFG_MAT_002` (over-issue) reveals the over-issue reason field. Every other refusal shows the server's text in the dialog (forms) or a toast (plain confirmations).
- **Lot field:** a typed or scanned number; Enter resolves it against the known lots and never submits (a scanner ends with Enter); *Suggest lots* fills rows from `GET orders/{id}/material-issues/suggest-lots`.
- **Bulk action:** the list releases the selected Draft orders one by one and shows a result per order.
- **Headings:** page title h1, section headings h2 (a table under an h2 uses `headingLevel={3}`), so axe's heading-order rule stays green.

API contract: `scripts/manufacturing-live/mfg-p36-contract-check.js` reads the interfaces of `mfg.api.ts` and checks every key against the real answers on both tenants - run it after changing a type or a DTO.

Tests: `orderActions.test.ts`, `pages/manufacturing/mfg.orders.pages.test.tsx` (list, detail, dialogs, documents, operations, history; faked HTTP client of `testKit.tsx`; axe on pages and dialogs).

## Patterns worth reusing
- **Drag and drop without a library:** native HTML5 (`draggable`, `onDragStart / Over / Drop`) on table rows, plus Up / Down icon buttons with names ("Move operation 20 down") as the keyboard and touch alternative. Only a Draft is draggable. The server renumbers 10, 20, 30 ... and answers the new routing, which replaces the cached one.
- **Confirm first** for approve / obsolete / copy / delete (`ConfirmDialog`); **server refusals** (cycle path, overlap version, `MFG_*` text) appear in an alert or toast through `serverMessage`; a delete refused because the record is used adds "deactivate it instead" (BR-GEN-02).
- **Warnings are not errors:** approving a BOM or routing succeeds and the BR-RT-08 warnings come back in the answer and are shown in a warning Alert.
- **CSV import dialog:** download template -> choose file -> *Check only* (dry run) -> the error table (line, key, problem) -> *Import* only after a clean check; the file is sent as `text/csv` (axios `transformRequest` leaves the string alone). The server writes nothing while there is an error.
- **Charts:** recharts with `colors` from `styles/tokens.ts`, plus a visually hidden table with the same numbers for screen readers.
- **Read-only users:** edit controls are hidden, not disabled; the settings form shows an info Alert.

## Reversals (P4-2)
`OrderTabs.tsx` (Operations tab: *Reverse report*; Finished goods tab) and `StockDocumentsTable.tsx` (*Reverse receipt*) use the shared `ReasonDialog` (free-text reason) and `useIdempotencyKey`; `orderActions` gained `reverseReport` / `reverseReceipt` (permissions `manufacturing:operation:reverse` / `receipt:reverse`). `mfgApi.reverseOperationReport` / `reverseReceipt`; types `ReversalAction`, `StockLineLot`, `reversalOfId`, `isReversed`. i18n `mfg.rev.*`, `mfg.docType.Reversal`.

## Quality (P4-3)
`InspectionPlanPages.tsx` (S21: list, editor with a characteristic modal and a versions tab, approve / obsolete / copy / delete through one `run` mutation like the BOM editor) and `InspectionPages.tsx` (S22: worklist, document with a grid of one value box per sample unit and an in range / out of range **text**, *Save draft*, *Submit* after a confirm, *Re-inspect*). `OrderTabs.tsx` has the `QualityTab`; `StockDocumentsTable` takes an `inspections` prop (the interim release button is hidden for a line that has an inspection; `inspectionOf` matches a line by receipt document + lot number). `ItemProfilesPage` and the routing operation modal have the plan pickers. `orderActions.viewQuality` = `manufacturing:qc:view`. API types `InspectionPlan`, `InspectionCharacteristic`, `Inspection`, `InspectionListItem`, `InspectionResultBody`; i18n `mfg.qc.*`, `mfg.nav.inspectionPlans|inspections`, `mfg.order.tabs.quality`.

## i18n
`nav.manufacturing`, `mfg.*` in `en.json` and `vi.json` (`mfg.order`, `mfg.opx`, `mfg.doc`, `mfg.receipt`, `mfg.close`, `mfg.issue` (also the Manual / Backflush labels), `mfg.orderStatus`, `mfg.opStatus`, `mfg.docStatus`, `mfg.docType`, `mfg.reasonDialog`, `mfg.nav`, `mfg.plants`, `mfg.settings`, `mfg.profiles`, `mfg.calendars`, `mfg.wc`, `mfg.catalogs`, `mfg.bom`, `mfg.routing`, `mfg.import`, status / day / exception / reason name maps).

## Tests
`pages/manufacturing/mfg.setup.pages.test.tsx`, `mfg.engineering.pages.test.tsx` (Vitest + Testing Library + axe, the HTTP client faked by `testKit.tsx`), `components/layout/Sidebar.manufacturing.test.tsx`. Not covered by a browser run: see DEFERRED-ISSUES R179 and R251.
