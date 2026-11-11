# Manufacturing - production order screens in the Admin (Phase 3, P3-6)

Added 2026-11-10. Source: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §6 (screens S15, S16, S18, S19), `02_PRODUCTION_DEV_TASKS.md` T3.12.1-T3.12.4, `03_PRODUCTION_TEST_CASES.md` TC-UI-001. The rules behind every button are in `ProductionOrders.md` (create, release, hold, cancel, change quantity), `MaterialIssueReturn.md` (S18), `OperationsAndBackflush.md` (operations tab) and `FinishedGoodsAndClosing.md` (S19, complete, close). Developer notes: `docs/frontend/manufacturing-admin-guide.md`.

## 1. Purpose, scope and actors
The screens let the people of a plant run a production order from the browser: create and release it, feed it with material, report the work, receive the finished goods and close it - and see at any moment what is still on its way to the Warehouse.

In scope: S15 order list, S16 order detail (tabs Overview, Materials, Operations, Finished goods, Stock documents, History), S18 issue / return dialog, S19 finished-goods receipt dialog, the close dialog and a plant-wide list of stock documents that need attention. **Out of scope (owner decision 3, 2026-11-10):** printing (PDF, labels, QR), Gantt (S14), material requests (S17), the shop-floor terminal (S20), the Quality and Cost tabs of S16, tablet / mobile variants.

| Actor (role) | Uses | Permissions (all `manufacturing:` ...) |
|---|---|---|
| Production planner | S15, creates and changes Draft orders, releases (also several at once), holds, changes the quantity, cancels | `order:view`, `order:create`, `order:release`, `order:hold`, `order:change-qty`, `order:cancel` |
| Storekeeper | Issues and returns material (S18), receives finished goods (S19), follows and retries stock documents | `material:issue`, `material:return`, `receipt:post`, `stock-document:retry` |
| Shop-floor supervisor | Starts work, adds time, reports operations, completes / reopens an operation by hand, completes or short-closes the order | `operation:execute`, `operation:complete-manual`, `order:short-close` |
| Production manager | Reopens, closes | `order:reopen`, `order:close` |
| Quality inspector | Releases a lot from quality hold (interim) | `qc:disposition` |
| Any viewer | Reads everything | `order:view` |

A user sees only the plants granted to them (the plant selector lists those); an order of another plant does not exist for them (404).

## 2. Process (what a user does)
1. **Plan.** On *Production orders* choose the plant, press *New order*, pick the item, quantity, due date (not before today), priority and, if wanted, a BOM / routing version and the output warehouse. The order is created as a Draft with its materials and operations copied from the BOM and routing; warnings of the engineering data are shown, not blocking.
2. **Release.** *Release* checks availability, reserves the stock and makes the first operation ready. When some material cannot be covered the order is still released and the shortages are listed; with the plant switch *block release on shortage* the release is refused and the order stays Draft. Several Draft orders can be released from the list in one go; each gets its own result.
3. **Feed.** On the *Materials* tab *Issue material* (or *Issue the rest* on a line) opens S18. Lots are typed or scanned; *Suggest lots* proposes first-expiry-first lots. Unused material goes back with *Return material* (also possible after completion, before the close).
4. **Work.** On the *Operations* tab an employee *starts work* (pause, resume, stop), a supervisor may *add time* afterwards, and *Report output* books good, scrap and rework quantities. Backflush materials are booked with the report. A supervisor can complete or reopen an operation by hand.
5. **Receive.** On *Finished goods* press *Receive finished goods* (S19): quantity per product, lot optional. A lot of an item with final inspection goes on quality hold; *Release from quality hold* moves it to the finished-goods warehouse. Receiving the whole quantity completes the order by itself.
6. **Finish.** *Complete* (supervisor) when fewer pieces will be made; below the tolerance a ShortClose reason is asked. *Reopen* undoes a completion. *Close order* shows the checklist and closes the order for good.
7. **Follow.** Every document the Warehouse has not yet confirmed shows as *Pending* with its attempts; the page refreshes by itself. A person can *Send now* or *Abandon* it.

## 3. Requirements and acceptance criteria
| ID | Requirement | Acceptance (test) |
|---|---|---|
| UI-1 | S15 lists the plant's orders with number (link), item, progress (completed / quantity with a bar), status badge, priority, due date with a **Late** flag (text + icon), planned period; paged by the server. | `mfg.orders.pages.test` list; contract script list envelope. |
| UI-2 | Filters: free text (order no / item), status, priority, late only, due from / to; each change goes to the server and returns to page 1. | Test "sends the filters to the server". |
| UI-3 | Draft orders can be selected (max 20) and released one by one; the result dialog lists for each order: released (with shortages) or the server's refusal with its code. Needs `order:release`. | Test "only Draft rows can be selected ...". |
| UI-4 | *New order* / *Edit* (Draft only) validates quantity > 0 and due date before sending; the item can be changed only on a new order; BOM / routing default to "by date". | Contract: PUT with nulls accepted. |
| UI-5 | **TC-UI-001**: the buttons on S16 follow status then permission, hidden (not disabled) when not allowed - matrix in §4. | `orderActions.test.ts` (every status x full / view-only / no permission), page tests (Draft, Closed, viewer). |
| UI-6 | Every server refusal is shown with the server's text: in the open dialog when it has a form, otherwise as a toast; never swallowed. The screen reacts to codes only where the flow needs it: `MFG_PO_012` opens the short-close reason dialog, `MFG_MAT_002` asks for the over-issue reason. | Tests "refusal of a plain confirmation", "Complete below the tolerance", "over-issue reason". |
| UI-7 | Cancel, Hold and short-close ask for a reason **code** of the right type from the plant's catalog; with no active reason the dialog says so and links to the catalog. | Test "cancel asks for a reason code of type Cancel". |
| UI-8 | A document stored but not yet in the Warehouse (HTTP 202) is shown as *saved and waiting*, never as an error; the order header counts them, the detail page refreshes every 5 s while any exists or the reservation is being updated, and the *Stock documents* tab offers Send now / Abandon (`stock-document:retry`, abandon asks first). | Tests "202 answer", "pending document ...", banners. |
| UI-9 | One request = one `Idempotency-Key`: the same body keeps its key (double click, retry), a changed body gets a new one; a dialog opened again starts with a new key. | Test "keeps one Idempotency-Key ..."; contract: same key + same body = same document, other body = 409. |
| UI-10 | S18: rows of material + quantity + lot; the lot field takes a typed or scanned number (Enter picks the matching lot and never submits); an unknown lot blocks the submit with a message; *Suggest lots* fills rows with the lots the server proposes (or says the order's reservation covers it). | Tests in "Materials tab and the issue / return dialog". |
| UI-11 | S19: one row for the main product and one per co- / by-product with received / planned / still to go; empty lot = generated by the plant's format; the upper limit is shown when the user may read the plant settings; the server's limit and lot refusals (`MFG_FG_001`, `MFG_FG_002`) are shown in the dialog. | Tests in "Receipts tab and the receipt dialog". |
| UI-12 | The quality release button appears only on a posted receipt with a lot still on hold, for `qc:disposition`, and asks first. | Tests "offers the quality release ...", "no quality release button ...". |
| UI-13 | The close dialog lists every condition with Met / Not met (text, not colour alone), keeps *Close order* disabled until all are met, and the option "carry the issued material to consumption" reloads the list as the server would see it. | Test "Close dialog". |
| UI-14 | Operations tab: time logs per operation with the actions the log's state allows; start needs an employee (and a machine of the work center if any); a report needs a reason for scrap and for rework quantities; a report answered 202 says it counts when its material booking reaches the Warehouse. | Tests in "Operations tab". |
| UI-15 | *Stock documents* (menu): documents of the plant, default view Pending + Failed, filters status / type / text, each linked to its order. | Test "the plant list asks for Pending and Failed". |
| UI-16 | Accessibility: dialogs trap focus and close on Esc, tables have captions, tabs use arrow keys, no information by colour alone; axe finds no violation on the pages and dialogs tested. | axe in the page tests. |
| UI-17 | The TypeScript types match the API: every key the types declare is present in the real JSON and enum values belong to the declared unions. | `scripts/manufacturing-live/mfg-p36-contract-check.js` on both tenants. |

## 4. Buttons by status and permission (TC-UI-001)
`orderActions(status, hasPermission)` is the single source; the server re-checks everything.

| Status | Buttons (each needs its permission) |
|---|---|
| Draft | Edit (`order:create`), Release (`order:release`), Cancel order (`order:cancel`) |
| Released | Back to draft (`order:release`), Put on hold, Cancel order, Change quantity, Issue material, Return material, Receive finished goods, per operation: Start work / Add time / Report output / Complete (`operation:complete-manual`) |
| InProgress | Put on hold, Change quantity, Issue, Return, Receive, Complete (`operation:complete-manual` or `order:short-close`), operation actions as above |
| OnHold | Resume (`order:hold`) only |
| Completed | Reopen (`order:reopen`), Close order (`order:close`), Return material |
| Closed, Cancelled | none (read only); documents can still be seen, Send now only with `stock-document:retry` |

Quality release (`qc:disposition`) shows on a receipt with a held lot in any status except Draft and Cancelled. A viewer sees no button at all.

**Reversals (P4-2, `Reversals.md`):** *Reverse report* (Operations tab, per Posted live report; `operation:reverse`; Released / InProgress only) and *Reverse receipt* (Finished goods and Stock documents tabs, per Posted receipt without a released lot; `receipt:reverse`; Released / InProgress / Completed). Both open a reason dialog (confirm first, shows what will be undone), send one `Idempotency-Key`, treat a 202 as "saved and waiting", show the server's refusal (`MFG_OP_007`, `MFG_FG_004`, `MFG_REV_001/002`) inside the dialog. Reversed reports / receipts show a **Reversed** badge; reversal rows show "reversal of ...".

## 5. Screens and fields
**S15 list.** Columns: *Order* (link), *Item* (code + name), *Progress* (completed / quantity, bar), *Status*, *Priority*, *Due date* (+ Late), *Planned* (start -> end). Selection column for users with `order:release`. Empty state: "No production orders match."

**S16 detail.** Header: order number, item, status badge, "was X before the hold" (OnHold), Late flag, action buttons. Banners: documents on their way (count), reservation being updated, reservation error, shortages of the last release, engineering warnings.
- *Overview:* item, quantity, completed, scrapped, priority, due date, planned / actual start and end, number of operations and materials, notes.
- *Materials:* line, component, operation, issue method, required, reserved, issued, returned, consumed, issued share (bar), *Issue the rest*; *Check availability* (required, still needed, available, shortage as text "short n").
- *Operations:* per operation: number, name, work center, status, planned period, good / scrap / rework, progress bar, time logs (employee, state, from, to, minutes, Pause / Resume / Stop); below, the operation reports (document, status, quantities, time, failure text).
- *Finished goods:* products of the order (main + co- / by-products, planned, received), the receipt documents with lot, expiry, hold / released marks.
- *Stock documents:* every issue, return, backflush and receipt of the order, lines expandable as a table, status, attempts / next attempt, error.
- *History:* when, from, to, reason (name), note, by.

**S18 dialog.** Per row: material (the order's lines **except Backflush materials, which are never issued by hand (R234, `MFG_MAT_013`)**; a return offers lines with net issued > 0), quantity (defaults to what is still to issue / return), lot (typed / scanned, with the known lots as suggestions), *Suggest lots* (issue), remove; line figures (required, issued, returned, reserved); note; over-issue reason after `MFG_MAT_002`.

**S19 dialog.** Rows as above; quantity received, lot number (placeholder "automatic"); note.

**Close dialog.** Conditions (logs, quality hold, settled material, pending documents, ...), carry-over checkbox, *Close order* (danger).

## 6. Use cases
**UC-1 Release with shortages (main).** Planner opens a Draft, presses *Release*. The order becomes Released; a warning lists BTP-CHAN01 short 100. *Alternative:* the plant blocks release on shortage -> the toast shows `MFG_PO_007` text and the order stays Draft. *Alternative:* the Warehouse is unreachable -> `MFG_INT_001` text, order unchanged.

**UC-2 Issue with lots (main).** Storekeeper opens *Issue material*, adds SON 52.5, presses *Suggest lots*: two rows (lot A 30, lot B 22.5, never an expired lot). *Issue* -> 201, toast "document MI-... posted", the Stock documents tab opens. *Exception:* quantity above the tolerance -> `MFG_MAT_002` shown, an over-issue reason field appears (needs the over-issue permission). *Exception:* the Warehouse is down -> the issue is refused with `MFG_INT_001` (issue / return read the Warehouse first, R227); nothing is stored, try again later. *Exception:* an unknown lot text blocks the submit with a message.

**UC-3 Receive finished goods (main).** *Receive finished goods*, quantity 40, lot empty -> 201, lot generated, on quality hold if the item needs final inspection; when the quantity is complete the order becomes Completed by itself. *Alternative:* the Warehouse is down -> a plain receipt (no lot, no backflush) answers 202: toast "saved and waits", the document shows Pending and posts by itself later; an order with a pending document cannot be completed (`MFG_PO_013`). *Exception:* quantity above the limit -> `MFG_FG_001` with the maximum; a typed lot that exists -> `MFG_FG_002`.

**UC-4 Complete below the tolerance.** Supervisor presses *Complete* at 90 of 100 -> `MFG_PO_012` opens the short-close dialog -> chooses the ShortClose reason -> completed (needs `order:short-close`).

**UC-5 Close.** Manager opens *Close order*: conditions show "Issued material of NVL-SON01 is not settled" (Not met). Ticks "carry to consumption": the list reloads, all Met, *Close order* enabled -> Closed, read only.

**UC-6 A stuck document.** A document stays Pending; the banner says N documents are on their way. The storekeeper opens *Stock documents*, sees attempts and the last error, presses *Send now* (still waiting -> info toast) or *Abandon* (confirm; whatever was booked is reversed, document Failed).

**UC-7 Several releases.** Planner ticks three Draft orders, *Release selected (3)*; the result dialog shows two released (one with shortages) and one refused with its code.

## 7. Integrations
All calls go through the Gateway `/mfg/api/v1/manufacturing/...` (ManufacturingService), plus: MasterData `employees` (active employees for time logs), Warehouse `warehouses` (names for the output warehouse), Warehouse `inventory/lots/product/{id}` (lot numbers for the lot field; if the user may not read it the lot field still works from *Suggest lots*), Product picker (item of a new order). Calls by screen are listed in `docs/frontend/manufacturing-admin-guide.md`.

## 8. Data shown and states
Order status: Draft, Released, InProgress, OnHold, Completed, Closed, Cancelled. Operation status: Pending, Ready, Running, Paused, Completed. Document status: Pending (on its way), Posted, Failed; line status Pending, Posted, Refused, Compensated (shown "Reversed"). Time log state: Running, Paused, Stopped. Statuses are shown as text in a badge whose colour role comes from `STATUS_ROLE` (success / info / warning / danger / muted) - never colour alone.

## 9. Reports and prints
None in this task (printing dropped by decision). CSV / PDF export of orders is not offered.

## 10. Assumptions
- A1 One plant is worked at a time (the selector remembers the last).
- A2 Times are shown in the browser's zone; the plant's zone is not applied (R254).
- A3 The employee for a time log is chosen by the person at the screen; Manufacturing has no link from the signed-in user to an employee.
- A4 The browser stays open while a document is pending only for the refresh; the Warehouse posting does not depend on it (the retry job does it).
- A5 Bulk release is a loop of single releases, not one transaction.

## 11. Open issues
- Not verified in a real browser (no automation here): layout, focus order and the 5 s refresh were checked with jsdom, axe and the API contract only (R179, R251).
- Employee list is the first 200 active employees (R252).
- Receipt upper-limit hint needs `masterdata:view` (R253).
- No badge in the menu for stuck documents (R255).
- Lint is not configured in the Admin project (no `eslint.config.*`); `tsc -b`, Vitest, axe and the token check are the gates.

## 12. UAT scenarios (walk TC-E2E-001 in the browser)
1. Sign in as a planner. *Production orders* -> *New order*: TP-BAN01, 100, due Friday -> order MO-... in Draft with 7 materials and 3 operations.
2. *Release*: Released, shortage warning for BTP-CHAN01 (need 400, have 300); operation 10 is Ready.
3. *Materials* -> *Issue material* (the screws, a backflush material, are not offered): KEO01 10 -> document posted; try 10.31 of a line whose tolerance is 10.30 -> refusal text appears; give the over-issue reason as a user with the permission.
4. *Operations* -> start operation 10 as an employee, pause, resume, stop; *Report output*: good 60, scrap 2 with reason Cracked -> VIT consumed 992 in *Materials*; the report appears in the list.
5. Report the rest of operations 10, 20, 30 until each is completed.
6. *Finished goods* -> *Receive finished goods* 100 -> lot on hold in the quarantine warehouse; order Completed. *Release from quality hold*.
7. *Materials* -> *Return material* for unused kit parts on the Completed order.
8. *Close order*: conditions turn Met after the carry-over; Closed; every action button is gone.
9. Stop the Warehouse container, receive 10 more on another order: "saved and waits", document Pending; start it again: Posted within a minute, page updated without reload.
10. Sign in as a viewer: no action button anywhere.
