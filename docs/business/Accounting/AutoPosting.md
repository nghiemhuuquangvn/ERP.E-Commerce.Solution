# Automatic general-ledger posting

Status: phase 1 (ROADMAP P1-1, 2026-10-23) — foundation plus the invoice, payment, order-cancellation and bill flows.
Stock / COGS, POS, payroll and refunds are separate plans (P1-1b … P1-1e) and are **not** posted yet.

## How it works

Business documents create journal entries through one service, `IJournalPostingService`
(`AccountingService/src/ERP.ACC.Application/Posting`). A flow never names an account: it posts *posting keys* and the tenant's
**posting profile** (`PostingAccountMappings`) says which account each key is. Moving a tenant to another chart of accounts
(the Vietnamese VAS chart, ROADMAP P1-2) means editing the profile, not the code.

The entry is staged on the caller's unit of work, so the document, its journal entry and the outbox rows commit together.

### Posting keys and their default accounts (the shipped 14-account chart)

| Key | Default account | Used by |
|---|---|---|
| `Cash` | 1000 Cash & Cash Equivalents | payment by cash |
| `Bank` | 1000 (the shipped chart has no separate bank account) | every other payment, bill payment, manual invoice payment |
| `AccountsReceivable` | 1100 | invoice, payments |
| `AccountsPayable` | 2000 | bills |
| `SalesRevenue` | 4000 | invoice |
| `Inventory` | 1200 | bill (see "Known simplification") |
| `VatOutput` | 2200 Tax Payable | reserved: no flow posts VAT yet (P1-3) |

A tenant with **no accounts at all** gets the default chart and profile the first time something has to be posted. A tenant that
already has its own chart is never given extra accounts: if a key has no account it needs configuring
(`GET/PUT api/v1/accounting/posting-profile`, permissions `accounting:accounts:view|manage`).

### Rules per flow

| Trigger | Entry | Idempotency key |
|---|---|---|
| Invoice created (from `SalesOrderCreated`, or the API) | Dr AR / Cr SalesRevenue, gross amount | `Invoice:<invoiceId>` |
| `SalesOrderCancelled`, invoice unpaid | invoice → Cancelled, the entry above is reversed (mirror entry) | `InvoiceReversal:<invoiceId>` |
| `PaymentProcessed` | Dr Cash (method Cash = 4) or Bank / Cr AR; applied to the order's invoice, which is paid once `AmountPaid ≥ Amount` | `Payment:<paymentId>` |
| Invoice marked paid by hand | Dr Bank / Cr AR for the outstanding amount | `InvoicePayment:<invoiceId>` |
| Bill approved | Dr Inventory / Cr AP | `Bill:<billId>` |
| Bill marked paid | Dr AP / Cr Bank | `BillPayment:<billId>` |

* **Idempotent.** The key is stored in `JournalEntries.SourceKey` under a unique index per tenant. A redelivered event finds the
  entry and does nothing — for a payment that includes not crediting the invoice twice.
* A cancelled order whose invoice is paid (even in part) is left alone and logged: it needs a refund (P1-1e).
* The payment's `Method` is PaymentService's enum, a **number** on the Kafka wire (CreditCard 0 … Cash 4). Payments created by the
  order saga always say CreditCard, so they are booked to Bank.
* Single currency (VND). The document currency is not converted (P1-4).

### Dates and periods

* The entry date is the document date. Its fiscal period is the one containing the date; if there is none, a calendar-month
  period is opened.
* A document dated in a **closed** period is booked today, in the current open period (the books of a closed period do not change),
  with a warning in the log.
* Entry numbers are `JE-<year>-<000001>` per tenant (atomic counter; a rolled-back posting leaves a gap).

### Journal entry status (`JournalEntries.Status`)

`0 Draft`, `1 Posted`, `2 Voided` (ignored by every report; the demo data uses it), `3 Reversed` (still counted — its reversal entry
offsets it). Reports count Posted and Reversed entries, filtered on `EntryDate`.
Reverse an entry with `POST api/v1/accounting/journal-entries/{id}/reverse` (reason required).

### When something is wrong

An incomplete posting profile (a key with no account, an inactive account, no open period today) **never blocks the document**:
the invoice / bill / payment is saved and an error is logged (`No journal entry was posted for Invoice <id>: <reason>`), visible in
the log service. Fix the profile; entries for documents already saved are not back-filled automatically.
`Accounting:AutoPosting:Enabled = false` switches automatic posting off.

## Known simplifications (follow-up plans)

* The bill debits **Inventory** when approved. When warehouse receipts post (P1-1b, needs unit cost in the Warehouse events and the
  PO price passed to Warehouse — receipts from POs are booked at cost 0 today) the debit key moves to a goods-received-not-invoiced
  account.
* **No VAT split** (P1-3): Sales, Purchasing and Payment carry no tax fields. The engine and `VatOutput` key are ready for it.
* **Not posted yet:** stock receipt / issue and COGS (P1-1b), POS sales (P1-1c; the event lacks subtotal / tax / tender split),
  payroll (P1-1d; the event has only NetPay), refunds and returns (P1-1e; Payment publishes the refund event before the gateway
  confirms, Sales returns carry no lines).
* Existing invoices and bills (before this release) are not back-posted; demo data keeps its own legacy entries (`SourceKey` empty).

## Operations

* Run Accounting migration `009` (it also fixes the seeded chart's `AccountType` / `NormalBalance` numbering: the seed used 0-based
  values, the application 1-based, so the P&L and trial balance misread the seed accounts).
* Kafka offsets of `AccountingKafkaConsumer` are committed after the message was handled; a failing message is tried 3 times, then
  logged with its topic / partition / offset and skipped. The RabbitMQ consumer republishes up to 5 times, then drops with an error log.
