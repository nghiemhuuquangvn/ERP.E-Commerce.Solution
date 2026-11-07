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

The sidebar group **Manufacturing** (`MANUFACTURING_CHILDREN` in `components/layout/Sidebar.tsx`) shows each entry by its permission; like the other modules it is not hidden by the tenant's license.

## Building blocks
- `api/manufacturing/mfg.api.ts`: one typed `mfgApi` object (plants ... routing, CSV import) on `gatewayClient`; enums are strings both ways; `downloadTemplate` fetches a CSV template with the token.
- `components/manufacturing/shared.tsx`: `usePlantContext` / `PlantSelector` (the plant is kept in `localStorage` key `erp-mfg-plant`; every list is per plant; a user sees only the plants they may see), `NoPlant`, `PageHeader`, `VersionBadge`, `CsvImportDialog`.
- `components/manufacturing/BomTree.tsx`: the multi-level BOM as an ARIA tree (expand / collapse each branch or all, phantom badge, alternative-group badge, substitutes struck through).
- `components/manufacturing/reorder.ts`: `reorderSeqs(seqs, from, to)` - the order sent to `POST routings/{id}/renumber`.

## Patterns worth reusing
- **Drag and drop without a library:** native HTML5 (`draggable`, `onDragStart / Over / Drop`) on table rows, plus Up / Down icon buttons with names ("Move operation 20 down") as the keyboard and touch alternative. Only a Draft is draggable. The server renumbers 10, 20, 30 ... and answers the new routing, which replaces the cached one.
- **Confirm first** for approve / obsolete / copy / delete (`ConfirmDialog`); **server refusals** (cycle path, overlap version, `MFG_*` text) appear in an alert or toast through `serverMessage`; a delete refused because the record is used adds "deactivate it instead" (BR-GEN-02).
- **Warnings are not errors:** approving a BOM or routing succeeds and the BR-RT-08 warnings come back in the answer and are shown in a warning Alert.
- **CSV import dialog:** download template -> choose file -> *Check only* (dry run) -> the error table (line, key, problem) -> *Import* only after a clean check; the file is sent as `text/csv` (axios `transformRequest` leaves the string alone). The server writes nothing while there is an error.
- **Charts:** recharts with `colors` from `styles/tokens.ts`, plus a visually hidden table with the same numbers for screen readers.
- **Read-only users:** edit controls are hidden, not disabled; the settings form shows an info Alert.

## i18n
`nav.manufacturing`, `mfg.*` in `en.json` and `vi.json` (`mfg.nav`, `mfg.plants`, `mfg.settings`, `mfg.profiles`, `mfg.calendars`, `mfg.wc`, `mfg.catalogs`, `mfg.bom`, `mfg.routing`, `mfg.import`, status / day / exception / reason name maps).

## Tests
`pages/manufacturing/mfg.setup.pages.test.tsx`, `mfg.engineering.pages.test.tsx` (Vitest + Testing Library + axe, the HTTP client faked by `testKit.tsx`), `components/layout/Sidebar.manufacturing.test.tsx`. Not covered by a browser run: see DEFERRED-ISSUES R179.
