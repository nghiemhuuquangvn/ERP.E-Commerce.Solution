# Admin: Promotion and POS back office (P2-1a)

Pages added 2026-10-28. Business description: `docs/business/Promotion/AdminBackOffice.md`, `docs/business/POS/AdminBackOffice.md`.

| Area | Page (`src/pages/…`) | Route | Permission (menu / actions) |
|---|---|---|---|
| Promotion | `promotion/PromotionsPage.tsx` | `/promotion/promotions` | `promotions:manage` |
| Promotion | `promotion/FlashSalesPage.tsx` | `/promotion/flash-sales` | view `promotions:view`, write `promotions:manage` |
| Promotion | `promotion/LoyaltyPage.tsx` | `/promotion/loyalty` | view `promotions:view`, adjust `promotions:manage` |
| POS | `pos/PosTransactionsPage.tsx` | `/pos/transactions` | `pos:transactions` (also void) |
| POS | `pos/PosShiftsPage.tsx` | `/pos/shifts` | `pos:shifts:view` |
| POS | `pos/PosZReportPage.tsx` | `/pos/z-report` | `pos:reports:view` |
| POS | `pos/PosTerminalsPage.tsx` | `/pos/terminals` | `pos:terminals:manage` |

## Pattern
Same as `hrm/WorkShiftsPage.tsx`: `DataTable` + a modal form (`react-hook-form`) + `ConfirmDialog` / `ReasonDialog`, react-query keys per area (`['promotion', …]`, `['pos', …]`), buttons gated by `usePermission().hasPermission`.

## API clients
`api/promotion/promotion.api.ts` (base `/prm/api/v1`: promotions, flash sales, loyalty) and `api/pos/pos.api.ts` (base `/pos/api/v1`). Both use `gatewayClient` (root-relative gateway paths, no `/api` prefix).

- **Request enums are numeric** (`DISCOUNT_TYPES`, `PROMOTION_SCOPES` in `promotion.api.ts`); responses return the enum **names**. Keep the mapping in that file only.
- The POS lists (`transactions`, `shifts`) answer `{ items, total, page, pageSize }`; day filters are `YYYY-MM-DD` and the last day is inclusive. Status filters take the enum name.
- Promotion and flash-sale lists are plain arrays (no paging).
- A completed sale's receipt is plain text (`responseType: 'text'`).

## i18n
Top-level groups `promotion.*` and `pos.*`, plus `nav.promotion`, `nav.promotions`, `nav.flashSales`, `nav.loyalty`, `nav.pos`, `nav.posTransactions`, `nav.posShifts`, `nav.posZReport`, `nav.posTerminals`, in `en.json` and `vi.json`. Status names are looked up as `promotion.status.<Status>`, `promotion.flashSaleStatus.<Status>`, `pos.shiftStatus.<Status>`, `pos.transactionStatus.<Status>`.

## Not here (yet)
The `/settings` index, the Shipping returns page, Notification webhooks and template editing, and the shared permission constants are the other parts of ROADMAP P2-1 (separate branches).
