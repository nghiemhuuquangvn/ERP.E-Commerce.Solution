# Deferred issues

Problems found while working through the ROADMAP that were **not fixed on purpose** (out of the plan's scope, needs a decision, or needs another roadmap item first).
Handle them after the main roadmap tasks; each plan that touches one of them should update its row. Last updated: 2026-10-24 (P1-2i done).

How to read: **Source** = where it was found; **Home** = the roadmap item or kind of work that should pick it up; **Impact** = why it matters.

## 1. Security and operations

| # | Issue | Source | Home | Impact |
|---|---|---|---|---|
| S1 | The local `sa` password is in git history and every service connects as `sa`. | P0-8 | P0-10 (least-privilege SQL logins) | Rotate the password; one login per service database. |
| S2 | `POST license/tenant/{id}/issue` returns 500: the validator has async rules but is run synchronously by ASP.NET. | P1-1 verification | Identity fix | A tenant cannot be licensed through the API (licence rows were inserted by SQL in tests). |
| S3 | The ADMIN role created by tenant sign-up holds 51 permissions and none for accounting, sales or payment. | P1-1 verification | Identity / onboarding | A new tenant cannot use those modules until roles are granted by hand. |
| S4 | Kubernetes `ForwardedHeaders__TrustedHops` is left at the default 1. | P0-8 | Deployment | Must equal the number of proxies in front of the Gateway, otherwise client IPs (rate limit, audit) are wrong. |
| S5 | Local `deployment/k8s/secrets.yaml` makes `NoSharedTokenKeyTests.NoDeploymentConfigurationSetsASharedTokenKey` fail (Architecture 53/54). | P0-8 | Clean the local file / template | The only failing architecture test; it hides real regressions. |
| S6 | `AutoMapper 12.0.1` has advisory NU1903 in PurchaseOrder (documented, intentional). | P0-8 | Dependency upgrade | Known vulnerability warning on every build. |
| S7 | `ICacheService` keys are global. Accounting's account queries used a tenant-less key (fixed in P1-2: no longer cached). Other services may cache tenant data the same way. | P1-2 | Audit all `ICacheService` call sites | Possible cross-tenant data exposure. |
| S8 | The admin SPA has no ESLint configuration (`npm run lint` fails); only `tsc -b` guards it. | P1-2 | Frontend tooling | No lint rules enforced. |
| S9 | Untracked file `src/frontend/admin/src/api/logs/logsClient.ts` has been sitting in the working tree. | since P0 | Owner decision | Commit it or delete it. |
| S10 | Test data left in the local databases: tenants `glcheck` (reset to a clean Vas99 chart in P1-2) and `p01check`, many orders / invoices / journal entries / pay slips in SYSTEM (pay periods 2025-03, 2025-04). | all verifications | Local clean-up | Pollutes local reports; do not carry to a shared environment. |

## 2. General-ledger posting (P1-1 follow-ups)

| # | Issue | Source | Home | Impact |
|---|---|---|---|---|
| G1 | Stock on hand before go-live and all documents before the posting releases are not back-posted. | P1-1, P1-1b | Opening-balance entry (manual) or an import tool | Account 1200 starts below the warehouse valuation. |
| G2 | A manual stock receipt (no PO, no bill) leaves a credit on GRNI that accounting must clear. | P1-1b | Accounting procedure | GRNI never reaches zero by itself. | **P1-8:** a supplier invoice now clears GRNI at the received value and the price difference goes to the variance account; goods invoiced beyond what was received, and manual receipts without an order, still leave a balance.
| G3 | FIFO / LIFO products **without lots** cost at the last receipt price (`Inventory.Cost`); only `Average` is weighted. | P1-1b | Warehouse costing | COGS differs from true FIFO. |
| G4 | Partial shipments appear to issue stock (and COGS) only when the order is fully shipped; unverified. | P1-1b (agent report) | Verify live, then Warehouse | COGS may lag for partially shipped orders. |
| G5 | POS: tax is not reduced by the discount (POS pricing rule). | P1-1c | POS pricing | Customer is charged tax on the undiscounted amount. |
| G6 | POS: a void or an offline sync after the shift is closed is not in the shift figures already booked. | P1-1c | POS shift handling | Cash variance can drift from the drawer. |
| G7 | POS: every tender other than "Cash" goes to the Bank key (no separate card / e-wallet clearing). | P1-1c | Posting keys per tender | Card and wallet settlements are not distinguishable in the ledger. |
| G8 | ~~Payroll figures are a stub~~ **Closed in P1-7** (salary terms, insurance, PIT with deductions, employer cost, split posting). Still open: §4q. | P1-1d | P1-7b | — |
| G9 | ~~Returned goods not restocked, no refund from a return~~ **Closed in P1-5** (whole-order return: restock at the issue cost, Dr Inventory / Cr COGS, Payment refund). Still open: partial returns by line (§4p). | P1-1e | P1-5b | Revenue is reversed but cost is not; the refund must be requested by hand. |
| G10 | A Payment refund has no idempotency key; a gateway timeout is recorded as a failed refund (no refund IPN); two concurrent refunds can both pass the over-refund check; `RefundStatus.Processing` is unused. | P1-1e | Payment hardening | Possible double refund or a refund the gateway did process shown as failed. |
| G11 | ~~No VAT on sales, returns, bills~~ **Closed in P1-3** (invoices, returns, bills; POS already posted VAT). Remaining: §4m. Was: No VAT on sales, returns, bills or POS (the `VatOutput` key exists but no flow fills it). | P1-1 | P1-3 | Tax payable is not tracked. |
| G12 | ~~Single currency (VND)~~ **Closed in P1-4** for invoices and bills (currency, rate, realized and period-end differences). The Payment service still defaults to USD in a VND stack and its events are booked unconverted (§4o). | P1-1 | P1-4b | Wrong amounts for foreign-currency tenants. |
| G13 | ~~Reporting marks every refunded payment 'Refunded' and reads no NetPay~~ **Closed in P1-6** (refunds per payment, PartiallyRefunded, net pay and breakdown on the pay slip fact). | P1-1d, P1-1e | — | — |
| G14 | ~~Notification does not consume PaymentRefunded~~ **Closed in P1-6** (refund message to the customer). | P1-1e | — | — |
| G15 | `SalesOrder.CompleteReturn` refusals are `InvalidOperationException`, which the error middleware does not map (HTTP 500 instead of 400/409). | P1-1e | Sales error handling | Misleading status code. |

## 3. Verified by unit tests only (not run live)

Ship-time COGS through a real sales order; POS sale and POS COGS; redelivery (same message twice) of stock, POS, payroll and refund events; cancel-after-payment; click-and-collect skip;
`PurchaseOrderReceivedConsumer` (no unit test at all). **Home:** a verification pass before the first real tenant.

## 4. Chart of accounts and closing (P1-2 follow-ups)

| # | Issue | Home |
|---|---|---|
| C1 | The `Vas99` account list and posting map were written from summaries of Circular 99/2025/TT-BTC, not the official text. An accountant must review them. | Accountant review (before any real use) |
| C2 | SYSTEM and demo data stay on the 14-account chart (seed joins by number). | Seed rewrite, optional |
| C3 | A tenant that already has journal entries cannot switch to another chart (apply-template refused). | Migration tool, if ever needed |
| C4 | Closing runs every period (monthly) and moves the result to retained earnings each time; year-end-only closing is not offered. | P1-2c |
| C5 | `FiscalPeriodClosed` has no consumer. | When a consumer needs it |
| C6 | Class 0 (off-balance) accounts are not supported. | P1-2c |

## 4b. Reports (P1-2b follow-ups)

| # | Issue | Home |
|---|---|---|
| R1 | The older `cash-flow` report is by account; the official form is `statements/cash-flow` (B03, P1-2d). Default categories are suggestions; no foreign-currency effect until P1-4. | Accountant review |
| R2 | The account ledger computes the running balance in memory over the whole range before paging; a very busy account over a very long range should use date-bounded snapshots. | Performance, if a tenant needs it |
| R3 | The cash-flow report needs accounts flagged `IsCash`; a tenant with its own chart must flag its cash / bank accounts (the screen says so when none is flagged). | Onboarding |
| R4 | The trial-balance list route changed content (roll-up on by default, opening-only accounts now appear); clients that sum the list would double count parents — they should use `trial-balance/full`. | API notes |
| R5 | Local DB `glcheck` now holds P1-2 / P1-2b / P1-2d verification entries on top of a clean Vas99 chart (October 2026 closed under Monthly; November and December 2026 periods open; voided closing entries and a voided November test entry). | Local clean-up |
| R6 | Excel files are built in memory; a ledger export with a very large number of rows is big. | Performance, if a tenant needs it |

## 4c. Statements (P1-2c follow-ups)

| # | Issue | Home |
|---|---|---|
| T1 | The B01 / B02 rows and account groups are a **draft** written from the Circular 200 forms; they are not checked against the official Circular 99/2025 forms and need an accountant's review before filing. | Accountant review |
| T2 | B01 splits trade receivables / payables by due date (P1-2h) and borrowings by the loan ledger (P1-2i); bonds (343), deposits (344) and prepaid expenses (242) stay classified by account. | P1-2j |
| T3 | Layouts exist for the Vietnamese and the 14-account chart; a tenant on its own chart still sees what the layouts cannot place under "unmapped". (P1-2j: the tenant can map accounts to the rows of B01 / B02; B03 and the notes still use code layouts, AA4.) | P1-2k |
| T4 | B03 exists by indicator code, direct and indirect; B09 notes exist (P1-2i) but the non-cash-transactions table is a text the accountant writes, not generated (P1-2j generates part of it from the journal, AA5). | Accountant review |
| T5 | The comparison column is whatever date the user picks (no automatic same-period-last-year). Amounts can now be shown in thousand / million VND (P1-2e). | P1-2j |
| T8 | Production-cost accounts 621 / 622 / 627 are not on B01 / B02 by design; a tenant that posts to them directly sees them under "unmapped" until the cost flow to 154 / 632 exists. | Manufacturing, if ever |

## 4d. B03, closing policy, Excel (P1-2d follow-ups)

| # | Issue | Home |
|---|---|---|
| U1 | The B03 row mapping (prefix → row) is a draft: e.g. withholdings paid (3382 … 3384, 3388) and VAT remitted (3331 out) land in "other payments" (07); salary paid straight from expense accounts lands in row 02. Needs the accountant's review. | Accountant review |
| U2 | No preview of what the year-end entry will close, no warning when a month of the year has no fiscal period (the SYSTEM demo tenant has no September 2026). The year-end entry simply closes whatever is still open. | P1-2j |
| U3 | The closing policy has no change history (only the row's updated timestamp); the policy is per tenant, not per legal entity. | Audit, if needed |
| U4 | The year-end entry is built from cumulative balances up to the period end; backdated prior-year corrections in a closed year are impossible (periods are locked) — they must be booked in the current year. | Policy decision |

## 4e. Amount unit and header (P1-2e follow-ups)

| # | Issue | Home |
|---|---|---|
| V1 | Rounding is per statement row with totals added from the rounded rows; a one-unit rounding difference between totals (e.g. 270 vs 440) is normal and reported in a note, but the filed form may need the difference pushed onto a chosen row — the accountant decides the convention. | Accountant review |
| V2 | The unit applies to B01 / B02 / B03 only; the trial balance, ledger, profit and loss and the older cash-flow report stay in exact VND. | Decision, if needed |
| V3 | The report profile is edited with `accounting:accounts:manage` (no dedicated permission); one profile per tenant (one legal entity); no history of changes. | Permissions work |
| V4 | The header has no logo, no filing date / place line ("Lập, ngày … tháng … năm …") and no signature images. | P1-2f, if wanted |

## 4f. PDF export (P1-2f follow-ups)

| # | Issue | Home |
|---|---|---|
| W1 | PDF exists for B01 / B02 / B03 only; no digital signature, not PDF/A, A4 portrait only (a wide statement with a prior column fits; landscape / other sizes are not offered). | If needed |
| W2 | The PDF header has no logo and no "Lập, ngày … tháng … năm …" line (see V4); PDF text is Vietnamese only (the screens have English / Vietnamese). | P1-2g, if wanted |
| W3 | The embedded font adds about 1.2 MB to the Accounting image; PDFs are generated per request in memory (a very long statement with many rows is fine, a bulk batch job would need queuing). | Performance, if needed |
| W4 | The PDF was checked by rendering pages to images and reading the extracted text; there is no automated visual-regression test. | QA |

## 4g. Indirect-method B03 (P1-2g follow-ups)

| # | Issue | Home |
|---|---|---|
| X1 | The indirect rows are a draft by account prefix; depreciation / provisions use the change of the balance-sheet account (a disposal also lowers it), and "gain / loss from investing" (05) removes interest income (515) only — not gains on disposals (711 / 811). Needs the accountant's review. | Accountant review |
| X2 | Non-cash investing / financing transactions (equipment or stock on credit, debt converted to capital) are not separated, so the indirect total can differ from the direct one (reported, not forced). The form's note on them belongs to B09. | P1-2h (B09) |
| X3 | Trading securities (121) are in investing, so indirect row 13 is not derived; direct investing rows have no line for them (a 121 movement shows as unmapped). | Accountant review |
| X4 | Rows 16 / 17 (other operating cash) are not derived; 335 (accrued expenses) and 3334 (income tax payable) are left out of row 11 by design, so accrued-expense changes other than interest appear as a difference. | Accountant review |
| X5 | The tenant `glcheck` test data does not exercise depreciation, provisions or payables; those rows were verified by unit tests (consistent ledger) only. | QA, if wanted |

## 4h. Maturity split (P1-2h follow-ups)

| # | Issue | Home |
|---|---|---|
| Y1 | Payments are stored as a total on the invoice, not by date, so the long-term split of a **past** balance-sheet date uses today's open amounts (approximate, stated in the statement). A payment history per document would make it exact. | P1-2i / payment history |
| Y2 | Receivables with no invoice (hand-written entries, 138 / 141 / 244) and bills still Pending are never split; the 12-month threshold is fixed; instalment plans and the order's payment terms are not modelled. | Accountant review |
| Y3 | `Invoices` / `Bills` have an `IsDeleted` column that the repositories do not filter (a soft-deleted test document still counted until it was deleted for real); the maturity split relies on these lists. | Data hygiene |
| Y4 | The tenant `glcheck` test run created and removed throw-away invoices, bills and journal entries (P12H-*); entries remain as Voided. | Local clean-up |

## 4i. 14-account layouts, off-balance, loans, B09 (P1-2i follow-ups)

| # | Issue | Home |
|---|---|---|
| Z1 | ~~The loan ledger posts nothing~~ **Closed in P1-2j** (drawdown and repayments post, reconciliation report). Remaining: loans created before P1-2j are "by hand"; the interest rate is information only (no accrual, see §4j). | — |
| Z2 | Off-balance accounts exist in the Vietnamese template only; a tenant that already has entries creates them by hand; foreign currency (007) is a register amount in VND with no exchange-rate logic (P1-4). | P1-4 |
| Z3 | The 14-account layouts are a simplification (no VAT, income tax, interest, depreciation); B09 for such a tenant has fewer tables. | Accountant decision |
| Z4 | ~~Texts not versioned~~ **Closed in P1-2k** (per fiscal year); no history of edits within a year (§4k). | — |
| Z5 | B09 structure and wording follow Circular 200 and are a draft for the accountant's review; section V.2 / V.7 use the invoice / bill split, so a past-date notes have the same approximation as B01 for trade items. | Accountant review |
| Z6 | `glcheck` test data: account 001 (inactive) with voided entries, saved note texts and company profile; `LoanInstallments` / `Loans` rows of the check were deleted. | Local clean-up |

## 4j. Loan posting, payment history, mapping, English notes (P1-2j follow-ups)

| # | Issue | Home |
|---|---|---|
| AA1 | ~~No accrual, scheduled amounts only~~ **Closed in P1-2k** (accrual, interest actually paid). Remaining: see §4k. | — |
| AA2 | ~~Always the Bank key~~ **Closed in P1-2k** (receiving account per loan). On the 14-account chart LoanPayable and InterestPayable still share account 2100 with accrued liabilities (the reconciliation says so). | — |
| AA3 | Payment dates are exact for payments recorded after migration 017; earlier ones are estimated (the last update of the document). Cancelled bills do not exist as a status. The register is not shown on any screen yet. | Accountant decision |
| AA4 | ~~Mapping covers B01 / B02 only~~ **Closed in P1-2k** for B03 and the notes; the maturity caps of B01 keep the standard prefixes, and a mapping is per statement (an account mapped on B01 is not automatically in B03 or the notes). | — |
| AA5 | The non-cash table is derived from the accounts of the entries (fixed assets bought on a liability, a liability moved into equity); other non-cash transactions are still written by the accountant. | Accountant review |
| AA6 | Notes exist in Vietnamese and English; the language is explicit (`lang`), not taken from Accept-Language. The admin screens added in P1-2j were type-checked, not clicked through in a browser. | — |
| AA7 | `glcheck` test data left: the 2,000 credit on account 341 from the P1-2i check (so the loan reconciliation there shows a 2,000 difference), the 9001 account was removed, estimated payment applications created by the migration. | Local clean-up |

## 4k. Loan interest, mapping, versions (P1-2k follow-ups)

| # | Issue | Home |
|---|---|---|
| AB1 | ~~No automatic job / no actual/360~~ **Closed in P1-2l** (opt-in job, day count per loan). Was: Accrual is run by the accountant (button / API) for a date; there is no automatic month-end job, and the convention is calendar days of the installment's period (no actual/360, no rate changes). | P1-2l |
| AB2 | The interest check compares today's accruals (not kept by date) with the account balance on the date; the receiving account of a loan cannot be changed after the drawdown is posted. | Accountant decision |
| AB3 | ~~No history of text edits~~ **Closed in P1-2l** (`NoteTextRevisions`). Was: Note texts are versioned per fiscal year; an edit overwrites the year's text (no history, no who / when beyond the row's update time). | P1-2l |
| AB4 | The mapping has no unmapped list for B03 (read on the report) and its side is ignored for B03 / B09; a mapping is per statement. | Accountant decision |
| AB5 | The new admin screens (loan account / accrual / interest paid, mapping for B03 / B09, year of texts) were type-checked, not clicked through in a browser. | — |
| AB6 | `glcheck` test data left: the 2,000 credit on 341 from P1-2i; throw-away loans, entries, accounts and mappings of the checks were removed. | Local clean-up |

## 4l. Day count, job, history, languages (P1-2l follow-ups)

| # | Issue | Home |
|---|---|---|
| AC1 | One rate for the life of a loan (no rate changes / floating rates); interest paid is typed, not read from the bank statement. | P1-2m |
| AC2 | The Japanese and Chinese notes have no PDF (no CJK font embedded; ~10–20 MB of fonts). | P1-2m |
| AC3 | ja / zh / fr wording is a draft translation, not reviewed by a translator or accountant. | Owner / translator |
| AC4 | The job runs once per service uptime day, only while the service runs on the 1st or later; no per-tenant run log or "last accrued" screen, only the entries and the log. A closed period is skipped silently for the user (log only). | P1-2m |
| AC5 | Revisions keep the user name text at the time; no restore button (the editor copies the old text into the draft), and no history for other settings (report profile, mappings). | P1-2m |
| AC6 | The new admin pieces (day count select, closing-policy switch, History list, language select) were type-checked, not clicked through in a browser. | — |

## 4m. VAT and e-invoice (P1-3 follow-ups)

| # | Issue | Home |
|---|---|---|
| AD1 | ~~One VAT rate per document~~ **Closed in P1-3b** (VAT lines, goods not subject to VAT); the lines still come from a person, Sales / Purchasing send no tax (§4n). Was: One VAT rate per document, taken from the tenant's settings (Sales / Purchasing send no tax); no per-line rates, exempt or non-taxable lines; with "prices exclude tax" the invoice exceeds the order total paid via Payment. | P1-3b |
| AD2 | The e-invoice provider is a sandbox: no real connector (VNPT / Viettel / MISA / BKAV), no tax-authority code, no signing, no adjustment / replacement e-invoices, no auto-issue on order. | P1-3b |
| AD3 | The VAT return 01/GTGT is a simplified draft (codes 22–25, 35, 40, 43 and rate groups), not the HTKK filing file; there is no VAT settlement entry (Dr 3331 / Cr 133) and a closed fiscal period is not reopened by it. | P1-3b / Accountant review |
| AD4 | The listing and the ledger can differ when a document is cancelled after the period, an entry is dated differently from its document, or VAT is booked by hand; the difference is reported, not corrected. | Accountant decision |
| AD5 | ~~Return at one rate~~ **Closed in P1-3b** (the invoice's VAT share). Was: A return takes the VAT at the invoice's rate; a partial return of mixed-rate goods is not possible. The live check did not cover a return (unit tests do). | P1-3b |
| AD6 | ~~No warning~~ **Closed in P1-3b** (`postedToLedger` / warning, posting gaps and repost). Was: A document created while no fiscal period is open exists without a journal entry (logged, as before P1-3); the invoice and bill screens do not warn. | P1-5 |
| AD7 | The new admin pieces (VAT columns, Adjust VAT, e-invoice dialog, VAT tab, provider in Settings → Tax) were type-checked, not clicked through in a browser. | — |

## 4n. VAT lines, settlement, e-invoice kinds (P1-3b follow-ups)

| # | Issue | Home |
|---|---|---|
| AE1 | The e-invoice provider is still the sandbox (the real VNPT / Viettel / MISA / BKAV connector needs the owner's provider account and API documents); no authority invoice code, no signing; replacements keep the original's series. | P1-3c |
| AE2 | The XML of the return is a reconstruction of the HTKK layout with this application's simplified indicator codes; it must be validated against the tax authority's current schema before filing. | P1-3c / Accountant |
| AE3 | The settlement does not post the payment of the VAT payable (Dr VAT output / Cr bank) and does not cope with a document dated later except through a reversal and a new round. | P1-3c |
| AE4 | VAT lines come from a person; orders and purchase orders still carry no tax (a per-line tax in the Sales / Purchasing events would remove the manual step). Auto-issue sends every invoice to a retail customer (no buyer tax code). | P1-3c |
| AE5 | If the provider fails after `Replace` applied a new VAT split, the invoice VAT is already changed and the replacement must be retried (no new split). The failing e-invoice of an adjustment / replacement is not stored. | Accepted |
| AE6 | The return pro rata, the auto-issue event path and the settlement of a period with late documents were covered by unit tests, not run live; the new admin pieces (lines editor, adjust dialog, e-invoice chain, gaps bar, settlement panel, XML button, auto-issue switch) were type-checked, not clicked through in a browser. | — |

## 4o. Foreign currency and bank reconciliation (P1-4 follow-ups)

| # | Issue | Home |
|---|---|---|
| AF1 | Payments that reach Accounting from the Payment service (`PaymentProcessed`, refunds) are booked unconverted: their currency is ignored. A foreign document is settled through *Mark as paid* with a rate. | P1-4b |
| AF2 | Only receivables and payables are revalued. The foreign balance of a bank / cash account is not tracked (no currency on journal lines), so the cash-flow FX effect (row 61) stays 0 and a foreign bank account is reconciled on base amounts. | P1-4b |
| AF3 | A revaluation cannot be redone for a period (one run; reverse its two entries by hand to run again). A foreign document carries one VAT rate (no VAT lines). A partial payment of a foreign document is not offered in the UI (mark-paid settles the rest). | P1-4b |
| AF4 | Exchange rates are read from the master data and typed on documents by a person; no automatic rate import. The tenant admin of `glcheck` has no master-data currency permission, so the live check entered the rates by SQL. | P1-4b |
| AF5 | A statement line is matched one-to-one: a bank line that settles several ledger lines (or the reverse) needs the ledger entries to be combined by hand. Interest of a loan is not read from the statement (AC1 stays open). | P1-4b / P1-2m |
| AF6 | The default accounts of the new keys (FxGain 515, FxLoss 635, BankFee 6427, BankInterestIncome 515) are suggestions for an accountant to review. The admin screens (currency fields, revaluation panel, reconciliation dialog) were type-checked, not clicked. | Accountant review |

## 4p. Saga, shipping and returns (P1-5 follow-ups)

| # | Issue | Home |
|---|---|---|
| AG1 | The saga publishes its commands after saving its own state, not through the outbox: a crash between the two is only repaired by the timeout retry. | P1-5b |
| AG2 | A return is of the whole order (every shipped line); partial returns by line need return lines in Sales (schema, API, admin). | P1-5b |
| AG3 | Timeouts are service-wide (`Saga:*`); no per-tenant setting. A manual push counts as a retry. | P1-5b |
| AG4 | Messages dropped after 5 attempts (Warehouse, Payment, Shipping consumers) exist only in the log; there is no dead-letter screen or replay. | P1-5b |
| AG5 | ✅ **Closed 2026-10-28 (P1-11d):** an order of several parcels is Delivered with its last parcel. Was: `DeliveryConfirmed` marks only a Shipped order Delivered; an order with several partial shipments needs every shipment confirmed (not modelled). A failed delivery attempt is only displayed. | P1-5b |
| AG6 | Restocking at the FIFO valuation overwrites the product's current cost with the returned cost, like any receipt. | Accountant review |
| AG7 | The order GET endpoint is cached for a short while, so the page may show the previous status for a few seconds after an event-driven change. The admin screen was type-checked, not clicked. | P1-5b |

## 4q. Payroll (P1-7 follow-ups)

| # | Issue | Home |
|---|---|---|
| AH1 | The seeded rate set (insurance rates, caps, deductions, brackets) is the project's understanding of the rules from 2026-01 and is DRAFT until an accountant confirms it; nothing blocks payroll on an unconfirmed set. | accountant review |
| AH2 | No PIT year-end finalisation, no tax-exempt part of overtime premium, no non-resident rate, no tax on a second job. | P1-7b |
| AH3 | Standard days are Monday–Friday; no public-holiday calendar, no shifts-based days. Approving a leave does not mark the attendance days, so Absent and unpaid leave on the same day count twice. | P1-7b |
| AH4 | Annual leave: fixed days per year from the rate set; no seniority accrual, no carry-over, a request is not split across years; sick, maternity and paternity leave have no balance. | P1-7b |
| AH5 | One contract per employee and month; no regional minimum wage check against the base salary; salary changes are entered by hand (no event from master data). | P1-7b |
| AH6 | Payroll cost is booked on one account (6421); no cost centres, no split of sales / admin staff. No payslip PDF, no bank payment file. | P1-7b |
| AH7 | ~~Reporting reads only gross / overtime / deductions of the pay slip~~ **Closed in P1-6**. | P1-7 | — |
| AH8 | Leave days are counted when the request is saved; a rate set change after approval does not recalculate balances. | P1-7b |

## 4r. Customer notifications and consumers (P1-6 follow-ups)

| # | Issue | Home |
|---|---|---|
| AI1 | Mail delivery is not tested end to end: the local stack has no SMTP server, so every message ends Failed with "Failure sending mail." (content, recipient, dedup and retries are checked; a real provider needs `Email` settings). | deployment |
| AI2 | Templates cannot be edited or deleted from the screen (read-only list); no resend of a failed message. | P1-12 / P1-6b |
| AI3 | The recipient of an order is the e-mail given when it was created; orders created before P1-6 have none, so their later messages are skipped. Payment and Shipping events still carry no e-mail (they use the order projection). | P1-6b |
| AI4 | Loyalty points are earned when the order is created (even before payment) and only reversed on cancel / return; no notice when points are taken back; no tier-change message. | P1-6b |
| AI5 | `warehouse-topic` is consumed by Reporting (`InventoryEventConsumer`) but nothing publishes to it (Warehouse uses RabbitMQ `warehouse_exchange`): `FactInventoryMovements` is never fed. `notification-topic` and `sale-order-settings-topic` still have no consumer; `LeadQualified`, `CaseOpened`, `PaymentInitiated`, `FlashSaleStarted`, `TrackingUpdated` remain unconsumed. | P1-6b |
| AI6 | Messages that fail 5 times are dropped with an error log (no dead-letter screen); a Kafka message is retried in process, so a restart in the middle of the retries loses it. | P1-5b / P1-6b |
| AI7 | Message wording is in English; the customer's language is not used. Wording needs the owner's review. | P1-6b |

## 4s. Purchasing: RFQ, supplier invoices, returns to vendor (P1-8 follow-ups)

| # | Issue | Home |
|---|---|---|
| AJ1 | Behaviour change: an approved purchase order no longer books a bill; bills already created stay. A tenant that never records supplier invoices gets no payable for its orders. | P1-8 (owner's choice) |
| AK1 | An RFQ is not e-mailed to suppliers and has no supplier portal; award is for the whole RFQ to one supplier. (Starting an RFQ from purchase requests is done in P1-9.) | P1-8b |
| AK2 | A supplier invoice in another currency than the order is held; an approved one in another currency than the books becomes a Pending bill with no rate (no automatic conversion). | P1-8b |
| AK3 | The match tolerances are per tenant, not per supplier or product; there is no tolerance on the invoice total / tax. | P1-8b |
| AK4 | Warehouse refuses a vendor return when the stock is not available (reserved or sold): after 5 attempts the return stays Shipped in Purchasing with an error in the Warehouse log and no stock movement (no screen). Returns issue at the product's current cost, not the lot cost; no lots / serials. | P1-8b |
| AK5 | A supplier credit is applied only to the bills of the same order; a credit larger than the open bills is booked but not applied. B01's maturity split and the foreign revaluation still read the bill amount, not the open balance. The Bills screen does not show the supplier invoice number or the credited amount yet. | P1-8b |
| AK6 | The goods receipt has no lines (header only); the match uses the order line's received quantity. Rejected quantities are not stocked or returned by the system. | P1-8b |
| AK7 | The Kafka consumer of Warehouse retries a vendor return in process (5 attempts); a restart in between loses the attempt (as in §4r AI6). | P1-5b / P1-8b |

## 4t. Replenishment (P1-9 follow-ups)

| # | Issue | Home |
|---|---|---|
| AL1 | Minimum and maximum are typed by hand; no demand forecast, average usage or lead-time based safety stock. | P1-9b |
| AL2 | No minimum order quantity or pack size; lines created from proposals use the unit "pcs". | P1-9b |
| AL3 | An RFQ made from proposals asks only suppliers that have a supplier item for one of its products; a product nobody supplies is not covered when others are supplied. | P1-9b |
| AL4 | One policy per product (Inventory is one row per product), not per warehouse or location. | P1-9b |
| AL5 | A draft order counts as "on order": a draft left unapproved hides the need until it is cancelled or approved. | P1-9b |
| AL6 | The old *Convert* button still only marks a proposal as done by hand. | P1-9b |
| AL7 | The generated draft order is not submitted or e-mailed; there is no notification that new proposals exist. | P1-9b / P1-6b |

## 4u. Online payments (P1-10 follow-ups)

| # | Issue | Home |
|---|---|---|
| AM1 | Only VNPay, MoMo and ZaloPay (VND). No Stripe, no cards in other currencies; the order has no currency (online methods and COD are sent as VND). | P1-10b |
| AM2 | Only the providers' **sandboxes** were used; no merchant (production) account. Production hosts must be configured (`MoMo:ProductionBaseUrl`, …) or the call is refused. | P1-10b |
| AM3 | A ZaloPay refund the gateway is still processing stays `Processing`; nothing polls its final status. | P1-10b |
| AM4 | A pending online payment the customer abandons waits for the saga timeout (about an hour with retries), then the order is cancelled and the payment closed; there is no shorter payment expiry. | P1-10b |
| AM5 | A customer who cancels at the gateway: a failed return cancels the order at once (as VNPay always did); no "try again" for a cancelled order. | P1-10b |
| AM6 | No e-mail / push when a payment fails or when money arrives for a cancelled payment (it is only flagged on the Payments screen). | P1-10b / P1-6b |
| AM7 | No reconciliation of captured payments against the gateway's own statements; no settlement import per gateway. | P1-10b |
| AM8 | The shop's gateway accounts have no history of changes and no test button ("create a 1 VND payment"). | P1-10b |
| AM9 | Mobile still sends no shipping address and shows amounts without the shop's currency formatting. | P1-10b |

## 4w. Real carrier sandbox run (owner-parked, 2026-10-28)

Get sandbox accounts at GHTK (staging), Viettel Post (partnerdev) and GHN (dev), expose the Gateway through a tunnel for the webhooks, and run one real booking → tracking → delivered cycle per carrier. Correct `ViettelPostCarrierProvider` first (written without its docs). Related: R26, AN4. The owner will do this later.

## 4v. Carriers (P1-11 follow-ups)

| # | Issue | Home |
|---|---|---|
| AN1 | The customer is not shown or charged a shipping fee; the order has no shipping line (the shop pays the carrier). Needs Sales, tax, payment and invoice to carry it. `DefaultShippingFee` / `FreeShippingThreshold` are still unused. | P1-11b |
| AN2 | Product weights do not reach Shipping: the parcel weighs items × the shop's default item weight. Needs an internal ProductService → Shipping call (service auth) or weights on the order line. | P1-11b |
| AN3 | The address has no ward (Sales keeps street / district / province): GHTK and Viettel Post want one and refuse without it; Viettel Post ward ids stay 0. | P1-11b |
| AN4 | Viettel Post is implemented without its official documentation; J&T Express is a placeholder; GHN cannot quote (needs district ids) so it is reached only through rules. | P1-11b |
| AN5 | ✅ **Partly closed 2026-10-28 (P1-11d):** a person can split a parcel into several; automatic splitting and a shipment per warehouse remain. Was: One parcel per order: partial shipments, several parcels and a shipment per warehouse are not modelled (see also AG5). | P1-11b |
| AN6 | ✅ **Closed 2026-10-28 (P1-11d):** approving a return books the way back at the carrier and tracks it. Was: Returns (`ReturnShipment`) do not go through the carrier: no pickup is booked, no return tracking. | P1-11b |
| AN7 | The cash a carrier says it collected is only compared with the order and flagged; there is no reconciliation of carrier cash remittances. | P1-11b / finance |
| AN8 | Carrier labels come from the local PDF generator, not from the carrier. | P1-11b |

## 6. Risk register — to handle after the main roadmap tasks

Latent risks found while working (owner's rule 2026-10-26: record them, fix them after the main tasks). Each task appends its own.

| # | Risk | Impact / trigger | Suggested fix | Found in |
|---|---|---|---|---|
| R1 | Warehouse consumer of vendor returns: after 5 attempts only a log line; the return stays Shipped, no screen, no retry queue. | A return of unavailable stock is silently stuck. | Dead-letter table + admin screen, retry button. | P1-8 |
| R2 | The purchase order line's product code is assumed equal to the warehouse SKU. | A mismatch hides on-order quantity (duplicate proposals) or the order lines point to nothing. | Carry the product id on order lines; map once. | P1-9 |
| R3 | Alert consumer used to requeue a failing message for ever. | Poison message loops. | **Mitigated in P1-9** (5 attempts then log + ack; the daily scan resends). Same pattern still to check in other RabbitMQ consumers. | P1-9 |
| R4 | B01 maturity split and FX revaluation read the bill amount, not the open balance (credits ignored). | Slightly wrong split after a supplier credit. | Use `Outstanding` there. | P1-8 |
| R5 | Kafka / Rabbit retries are in process; a restart between attempts loses the message. | Lost return / alert / e-mail. | Put retries on the outbox / a retry table. | P1-8, P1-6 |
| R6 | Admin screens of P1-6 … P1-9 are type-checked and API-tested but were never clicked in a browser. | Layout or interaction bugs only a user sees. | **Resolved 2026-10-27 (risk clean-up).** Walked in a real browser (Playwright + Chrome, 102 admin routes + storefront sign-up / orders / result page): no page errors, empty pages or error banners on the P1-6…P1-10 screens; the walk-through found R19 (six real defects, all fixed). Click-through of create / edit flows is still manual (see R20). | P1-6…P1-9 |
| R7 | The other services' test projects (SO, SHP, PAY, NTF, PRM, RPT, CRM, HRM) are not re-run per task. | A shared change could break one unnoticed. | **Resolved 2026-10-27 (risk clean-up).** All 16 unit projects run: ACC 580, CRM 4, Gateway 44, HRM 42, Identity 259, NTF 19, PAY 84, PO 61, POS 10, PRM 8, RPT 7, Shared 100, SHP 9, SO 52, WH 44, Architecture 59 - all green (the obsolete `jwt-signing-key` in the local, git-ignored `deployment/k8s/secrets.yaml` was removed, which cleared `NoSharedTokenKeyTests`). Still to do per merge train. | P1-8, P1-9 |
| R8 | Low stock now means available ≤ minimum (was total ≤ reorder point): existing tenants get more "low" products and alerts, and the list filter changes meaning. | Alert / proposal volume jumps after deploy. | Tell tenants; review default minimums. | P1-9 |
| R9 | The alert is sent over RabbitMQ with no per-product dedupe at the consumer; a burst of movements is handled one by one with a 1–4 s delay per retry inside the consumer. | Backlog if the Purchasing database is slow. | Batch / debounce per product. | P1-9 |
| R11 | The providers cannot call a local Docker stack, so their server notifications were only **simulated** (signed with the stored secrets); only create and refund were real sandbox calls. A real notification may differ in a detail (extra fields, retries, IPv6 source). | A surprise on the first real payment. | Test once with a public tunnel (`Payment:PublicBaseUrl`) and a real sandbox account of each provider before going live. | P1-10 |
| R12 | A ZaloPay sandbox key I remembered (app 2553) was wrong; the signing code was right (app 2554 worked at once). Keys for MoMo/ZaloPay in this repo's docs and tests are the providers' public sandbox values. | Confusion about "invalid signature". | Keep the sandbox accounts documented in one place; never commit real keys. | P1-10 |
| R13 | Accounting could not post the captured payments of the live check because the tenant's October 2026 period was closed (handler logs, no entry). Cash collected on delivery can fall into a closed period the same way. | Payments without a ledger entry. | **Partly resolved 2026-10-27.** The payment event is now idempotent without a ledger entry (a redelivery does not credit the invoice twice and retries the posting). `posting-gaps` still lists only invoices and bills, not payments (R22). | P1-10 |
| R14 | The storefront order calls were out of sync with the backend (`lines` vs `lineItems`, `id` vs `orderId`, `orderNumber` vs `orderNo`) — placing an order from the storefront / mobile could never have worked. Fixed for create / confirmation / history; other client calls may have the same drift. | Silent breakage of clients. | **Resolved 2026-10-27 (risk clean-up).** `ClientApiContractTests` fails when the storefront or the customer app calls a `/v1/...` route (or HTTP method) no controller serves. Fixed the drift it and the walk-through found: mobile search (wrong route and response shape), mobile user name, order lists that read `lineItems` the list never returns (the page crashed), coupon result and product status types. Field names are not checked by the guard. | P1-10 |
| R15 | Mobile defaults to `http://localhost:5000/api`: the Gateway takes the tenant from the **host**, and `localhost` belongs to no tenant. | The app cannot sign in outside a prepared setup. | **Resolved 2026-10-27 (risk clean-up).** `.env.example` in both apps, a console warning when the variable is missing, `docs/frontend/mobile-apps.md`. | P1-10 |
| R16 | Registering a customer fails on a tenant that has no `CUSTOMER` role (the test tenant `glcheck`); the role must be seeded for every tenant. | New customers cannot sign up. | **Resolved 2026-10-27 (risk clean-up).** `DefaultTenantRoles` seeds CUSTOMER / STAFF / USER on `POST /tenants` and tenant-signup; migration Identity 030 repaired the existing tenants; a missing role is a 400. | P1-10 |
| R17 | The Payments list (admin) has no filter by provider and no screen for payments flagged for review (the flag is shown as a note). | A refund-needed payment can be missed. | **Resolved 2026-10-27 (risk clean-up).** `GET payments?provider=&needsReview=` and the two controls on the admin Payments screen. | P1-10 |
| R18 | Sales cancels an order by hand (admin) without telling Payment: its pending payment stays open until paid or forgotten (the saga path now closes it). | A late payment for a manually cancelled order is captured, not flagged. | **Resolved 2026-10-27 (risk clean-up).** Payment consumes `SalesOrderCancelled`: a Pending / Authorized payment is cancelled (a Captured one is never refunded by it). | P1-10 |
| R19 | **Browser walk-through findings (all fixed 2026-10-27):** (a) the admin image was built with the old Kubernetes hub hosts from a git-ignored `.env` (`notifications.erp.local`), so the bell / chat hub negotiation failed with 401 on every page; (b) even on the right host the SignalR negotiate carries the ticket in the `Authorization` header, which only the query string was read for (`HubTokenResolver`); (c) storefront registration always failed: `TenantCode` was implicitly required although the host decides the tenant; (d) a second customer with the same display name was a 500 (account name was the display name, globally unique; now the e-mail, unique per tenant, migration Identity 031); (e) `crypto.randomUUID` does not exist on a plain-http host (chat store threw on every page); (f) a customer got 403 on their own order list and confirmation (the CUSTOMER role holds no permissions). | These were invisible to unit tests; (a)-(f) broke the storefront and the admin bell for everyone. | Fixed and covered by tests; keep the browser walk-through as part of every merge train (`pw/smoke.js` idea: sign in, open every route, collect console / network errors). | risk clean-up |
| R20 | The walk-through opened pages and did the sign-up / order-list flow, but did **not** click create / edit flows (RFQ, supplier invoice match, payroll run, refund modal) nor place a real storefront order (the glcheck catalog is empty). Mobile was type-checked only. | Defects in forms and the mobile app remain possible. | Seed a small catalog on the test tenant and script the checkout end to end; run the mobile app on an emulator. | risk clean-up |
| R21 | **CUSTOMER / STAFF / USER roles hold no permissions in any tenant**, SYSTEM included. Customer features work only where an endpoint checks "own resource" (create order, my orders, order by id, payment status). Any new customer-facing endpoint guarded by a permission will answer 403. | Repeats R19(f). | Rule: customer endpoints use the own-resource check (or `[AuthorizeAnyAuthenticatedUser]`), never a permission; or define a CUSTOMER permission set. | risk clean-up |
| R22 | `posting-gaps` finds invoices and bills with no ledger entry but not payments; a payment received while no period is open has an invoice credit and no entry, and only a log line says so. | Cash without a ledger entry stays invisible. | Add a Payment type to `PostingGapFinder` (applications whose `payment:{id}` has no `Payment` entry) and a repost. | risk clean-up |
| R23 | The Payment Settings screen shows the older **VNPay** form (enable + secret) above the new VNPay card (TMN code + hash secret): two places for one account. | Confusion about which one counts (the card's keys win, the old fields are a fallback). | Remove the old form after a release, or fold it into the card. | risk clean-up |
| R24 | The shop admin of a tenant created by sign-up holds only Identity / System permissions (by design); on `glcheck` the walk-through shows 403 on master-data and identity pages for that admin. | A new tenant admin sees empty / forbidden business pages until roles are granted. | A "business administrator" role template applied at tenant creation. | risk clean-up |
| R25 | The shared `HubTokenResolver` change was deployed only to NotificationService and ChatBotService (the services with hubs); `deploy-changed.ps1` will rebuild all other services next time because their Shared hash changed. | One long deploy. | Expected, harmless. | risk clean-up |
| R26 | **No real carrier was called**: the live check used a fake carrier server and simulated notifications; GHTK and GHN were read from their docs, Viettel Post was written without them. The first real sandbox run may show a field, a status code or a webhook detail that differs. | Bookings refused or statuses misread on day one. | Get sandbox accounts (GHTK staging, Viettel Post partnerdev, GHN dev), run through a tunnel, then correct `ViettelPostCarrierProvider` first. | P1-11 |
| R27 | A carrier outage longer than about 10 seconds makes the consumer give up (5 attempts) and the order is cancelled. | Orders lost to a short outage. | Longer back-off or park the command and retry through the saga timeout. | P1-11 |
| R28 | Webhook authentication is a shared secret in the callback address (GHTK and GHN do not sign); it can appear in a carrier's logs or a proxy log. | A leaked secret lets someone report fake statuses (e.g. "delivered" → COD payment captured). | Rotate through the Carriers tab; consider also checking the carrier's source network where published. | P1-11 |
| R29 | A delivered notification captures the COD payment at the order's amount even if the carrier says it collected less (the shipment is only flagged). | Books cash that was not collected. | A finance step that confirms carrier remittances before capture. | P1-11 |
| R30 | The Viettel Post status is read from its Vietnamese name; a renamed status or a wording not listed is only a tracking line (never a delivery). | Deliveries not applied until the table is checked. | Replace by the official code table when the documentation is available. | P1-11 |
| R31 | The polling job asks only about parcels silent for 6 hours, 50 per shop per pass, and Viettel Post cannot be polled. | A lost webhook is noticed late. | Shorter interval for COD parcels; a per-carrier poll for Viettel Post. | P1-11 |
| R32 | Selection by the cheapest quote quotes every usable carrier on every order (one call each) and GHN never quotes. | Slower bookings, carrier rate limits. | Cache quotes per province / weight band. | P1-11 |
| R34 | The order created event used to carry the total BEFORE the coupon (it was built before `ApplyDiscount`), so invoices and reports of coupon orders were too high. Fixed in P1-11b (the event is kept equal to the saved order); invoices of older coupon orders stay as booked. | Old coupon orders overstate revenue. | Find orders with `DiscountAmount > 0` before 2026-10-28 and compare with their invoices. | P1-11b |
| R35 | ✅ **Closed 2026-10-28 (P1-11c).** The delivery fee is booked on its own key `ShippingRevenue` and shown on the invoice (`ShippingNetAmount`); the delivery share of the VAT follows the share of the amount. Left: one VAT rate for goods and delivery (R40). | | | P1-11b |
| R36 | ✅ **Corrected and closed 2026-10-28 (P1-11c).** The register said a return did not refund the fee; in fact the admin sent the order total, so the fee came back by default. Now the approver decides (`RefundShippingFee`, default no); the goods are the cap otherwise. Cancelling a paid order still refunds everything. | | | P1-11b |
| R37 | Partly closed 2026-10-28 (P1-11c): the price list prices by province and weight, so a heavy far parcel costs more. Still the shop's price, not a live carrier quote; nothing compares it with the carrier's cost. | Shipping margin loss on badly priced rows. | A report of fee charged vs carrier fee per shipment (the carrier fee is already stored on the shipment). | P1-11b |
| R38 | ✅ **Fixed 2026-10-28 (`fix/r38-variants-and-service-token-keys`).** (1) `POST products/{id}/variants` was HTTP 500: the variant has a client-assigned Id, so EF tracked it as Modified, never stamped its tenant and `EnforceTenantOnSave` refused it; the handler now adds it explicitly (as the image handlers already did; the audit found no other handler with the flaw), and a duplicate SKU is a 400 business error instead of 500. (2) A restarted caller signs service tokens with a new ephemeral key and the receiver's library refreshed its cached keys in the background, so the first call after a redeploy got 401 (a live order was refused by Warehouse): `TrustedCallerKeys` now fetches the callers' keys again before a token with an unknown key id is judged, at most once per 2 s per caller. Still open: the Identity-issued user-token keys (`ErpJwtBearerOptions`) refresh the same way, only matters when Identity rotates its signing key (R39). | | P1-11b |
| R39 | User-token signing keys fetched from Identity's JWKS by every service and the Gateway are refreshed in the background by the library: after an Identity key rotation the first token signed with the new key may be refused once per service. | A short burst of 401s right after a key rotation. | Reuse the TrustedCallerKeys approach (refresh before judging an unknown kid). | R38 |
| R40 | Goods and delivery share the tenant's single VAT rate; a different rate for delivery (or an exempt one) would need VAT lines on the invoice. A document with VAT lines or in a foreign currency books its delivery in sales revenue. | Delivery VAT may not match the tax rules of every shop. | Give the delivery its own VAT line when VAT lines are used. | P1-11c |
| R41 | Provinces in the price list are matched by name (without case / diacritics / "Tỉnh / TP."); a province typed with another spelling ("Sài Gòn", an abbreviation) matches no row and pays the flat fee. The storefront's province field is free text. | A customer is charged the flat fee instead of the row. | A province picker from MasterData geography at checkout. | P1-11c |
| R42 | The fee request needs ProductService for weights; during its outage default weights are used and a lighter band may apply (the answer says "estimated"). | Under- or over-charged delivery during an outage. | Store the unit weight on the order line when the order is placed. | P1-11c |
| R43 | ✅ **Closed 2026-10-28 (`feat/r43-per-parcel-cod`).** Cash on delivery is captured per parcel (Payment `PartiallyCaptured`, `PaymentCaptures`, one `PaymentProcessed` per parcel, Accounting / Reporting / Notification keyed per capture). Was: captured once with the last parcel. | — | — | P1-11d |
| R44 | "Last parcel delivered" is judged when each parcel is applied (the others' state is read at that moment): two parcels delivered at the same instant may each see the other as pending, and the order stays Shipped (visible, never wrongly delivered). | Order stuck at Shipped. | Re-check on a job, or on the order screen. | P1-11d |
| R54 | A parcel's cash is the shop's EXPECTED cash (`Shipment.CodAmount`), not what the carrier reports collecting; a mismatch only flags the shipment for review (as before). | Books say 100 000 collected while the carrier remitted 90 000. | Book the carrier-reported amount and the difference separately. | R43 |
| R55 | An order cancelled or returned after only some parcels were paid refunds what was collected (`Payment.Collected`); the domain refuses a plain cancel of a `PartiallyCaptured` payment, and the not-yet-delivered rest is never captured. A payment left `PartiallyCaptured` because a parcel is never delivered stays open. | Payments stuck in PartiallyCaptured. | A job / screen listing partly collected payments older than N days. | R43 |
| R56 | Reporting keeps one fact row per payment: the amount accumulates and `ProcessedAt` stays that of the FIRST part, so the cash of later parcels is reported on the first parcel's day. `PaymentAuthorized` no longer creates a fact (it used to, with the whole amount). | Daily cash slightly early for split COD orders. | One fact row per capture. | R43 |
| R57 | Before this fix `JDoc.GetString` threw on a numeric payment method, so the Reporting fact of every `PaymentProcessed` with `Method` as a number was lost (found in the R43 live check, fixed). Facts missing for past payments are not rebuilt. | Reporting payment totals low for the past. | Rebuild FactPayments from PaymentDB. | R43 |
| R58 | The glcheck fiscal period October 2026 is closed on purpose: the live check had to open it for a moment (SQL Status 0) and restored it afterwards. A shop with a closed period gets the cash booked on the invoice but no ledger entry until a period is open (existing R13 behaviour). | — | — | R43 |
| R59 | Cancelling a return at the carrier does not tell Sales: the order stays *return requested* and its refund keeps waiting for goods that will not come (staff must receive by hand or handle the order). | An order stuck waiting. | A `ReturnCancelled` event Sales consumes. | P2-1b |
| R60 | The Returns screen has no bulk actions and no export. | Slow with hundreds of returns. | Selection + bulk book again, CSV export. | P2-1b |
| R61 | Webhook secrets saved before P2-1b stay in plaintext until the endpoint is saved again. | A database reader sees old secrets. | A one-off job sealing them. | P2-1b |
| R62 | A failing webhook endpoint is retried only by the next event; there is no retry queue and no auto-disable after repeated failures. | Lost events, log noise. | Retry with backoff, disable after N failures. | P2-1b |
| R63 | No "send a test message" for a template and no per-language templates; the preview uses placeholder names as samples. | Wording is checked by eye only. | Test-send to the editor's e-mail, language column. | P2-1b |
| R64 | The platform tenant edits the platform defaults in the same screen with no history of changes; a bad edit hits every shop that did not override. | Hard to roll back. | Revision history like the note texts. | P2-1b |
| R65 | The webhook delivery log keeps 30 days and the connect-time SSRF check (resolved address) is unit-tested but only the static address rules were exercised live. | — | A live test against a name that resolves to a private address. | P2-1b |
| R66 | A shop cannot switch a message off for good: its own copy switched off only makes the platform default apply again; only the platform tenant can switch a default off, and the screen does not warn when a code the system needs is off. | Customers keep getting a message a shop wants off. | A per-shop "do not send" flag. | P2-1b |
| R45 | The reverse parcel's carrier fee is paid by the shop and not booked; a return refund is held for goods only when a pickup was booked (a carrier that cannot reach the customer's area refuses and the return goes on by hand). | Missing expense; a refund can wait for goods that never come (use *Goods received* / cancel the return parcel). | Book the fee as a return-shipping expense; a return timeout. | P1-11d |
| R46 | Splitting is manual and only before pickup; there is no automatic split by weight / warehouse, and the storefront shows neither parcels nor tracking. | Staff effort; customer cannot see parcels. | Auto-split rule, storefront tracking page. | P1-11d |
| R47 | Demo orders that already had two live shipments were renumbered by migration Shipping 010 (ParcelNo 2…) while their ParcelCount stays 1. The order read cache was removed (Sales) because event-driven changes never evicted it and its key had no tenant. | Old demo data inconsistent. | Clean the demo data. | P1-11d |
| R48 | The Admin cannot list loyalty accounts or see a customer's point history; staff must know the user id. | Customer service cannot browse; adjustments leave no readable trail in the UI. | List / history endpoints and a customer picker. | P2-1a |
| R49 | Coupons cannot be exported or searched across promotions; generated codes are only visible per promotion. | Codes for a mailing must be copied by hand. | CSV export, code search. | P2-1a |
| R50 | Promotion and flash-sale lists are not paged or filtered on the server; an active promotion cannot be edited and a flash sale cannot be edited at all. | Slow list with thousands of rows; fixes need cancel + recreate. | Paged list, controlled edit of a live promotion. | P2-1a |
| R51 | A promotion's product / category ids are typed as text in the form. | Typos give a promotion that applies to nothing. | Product / category pickers. | P2-1a |
| R52 | POS screens show the cashier as a short id (POS holds no user names); the cash difference uses the shift summary's Cash line. | Hard to know who to ask. | Name lookup through Identity. | P2-1a |
| R53 | No refund of a completed POS sale from the back office (a void is the only correction), and no export of the transaction / shift lists. | Partial returns at the till need another route. | POS refund flow, exports. | P2-1a |
| R33 | One `ERP.Shared.UnitTests` test failed once during a full sequential run of all 16 projects (99/100) and passed in the next three runs (100/100); it was not identified (the output was not captured). Probably a timing-dependent test. | A flaky guard hides real failures. | Re-run the Shared tests with the logger on a few times and fix or quarantine the unstable one. | P1-11 |
| R10 | Several Database runner entries were missing (Warehouse 007–008, Purchasing 009–013 until now): a fresh database built by `RunAllScripts.ps1` depended on someone remembering to add them. | Fresh install missing columns. | **Resolved 2026-10-27 (risk clean-up).** `DatabaseRunnerTests` (Architecture): every `.sql` is in `RunAllScripts.ps1`, every listed file exists, a service's migrations run in order. 29 more unlisted files were found and added (Identity 021-031, POS 005-008, Product 015-018, Shipping 005-007, MasterData 007-008, CRM 005-006, ChatBot 002, PromotionService 003-005), and the wrong `Promotion\002_SeedDemoData` entry fixed. `RunAllScripts_sqlcmd.ps1` is marked legacy (lists only the first scripts). | P1-8, P1-9 |

## 5. Roadmap items these issues are waiting for

P0-10 least-privilege SQL · P1-2l automatic accrual job, actual/360, history of text edits · P1-3 VAT and e-invoice · P1-4 foreign currency and bank reconciliation ·
P1-5 saga completion and returns · P1-6 notifications · P1-7 real payroll.
