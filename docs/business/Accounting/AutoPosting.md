# Automatic general-ledger posting

Status: phase 1 (ROADMAP P1-1, 2026-10-23) — foundation plus the invoice, payment, order-cancellation and bill flows; P1-1b
(2026-10-24) adds stock receipt / issue / adjustment and COGS; P1-1c (2026-10-24) adds POS sales, voids and cash-shift variance; P1-1d (2026-10-24) adds payroll
(pay slips, their payment and cancellation); P1-1e (2026-10-24) adds payment refunds and completed returns (the money side). Returned goods are
not restocked or costed back yet (P1-5).

## How it works

Business documents create journal entries through one service, `IJournalPostingService`
(`AccountingService/src/ERP.ACC.Application/Posting`). A flow never names an account: it posts *posting keys* and the tenant's
**posting profile** (`PostingAccountMappings`) says which account each key is. Moving a tenant to another chart of accounts
(the Vietnamese VAS chart, ROADMAP P1-2) means editing the profile, not the code — or applying a template (docs/business/Accounting/ChartOfAccounts.md).

The entry is staged on the caller's unit of work, so the document, its journal entry and the outbox rows commit together.

### Posting keys and their default accounts (the shipped 14-account chart)

| Key | Default account | Used by |
|---|---|---|
| `Cash` | 1000 Cash & Cash Equivalents | payment by cash |
| `Bank` | 1000 (the shipped chart has no separate bank account) | every other payment, bill payment, manual invoice payment |
| `AccountsReceivable` | 1100 | invoice, payments |
| `AccountsPayable` | 2000 | bills |
| `SalesRevenue` | 4000 | invoice |
| `Inventory` | 1200 | stock receipts, issues, adjustments |
| `GoodsReceivedNotInvoiced` (GRNI) | 2100 Accrued Liabilities | stock receipts (credit), bills (debit) |
| `CostOfGoodsSold` | 5000 | stock issues (ship, POS sale) |
| `InventoryAdjustment` | 5000 (a separate key so a VAS chart can use a shrinkage account) | stock gains / losses |
| `CashOverShort` | 5100 Operating Expenses | POS shift cash difference |
| `SalesReturns` | 4000 (the default chart has no contra-revenue account; VAS 521 later) | completed returns |
| `LoanPayable` | 2100 (Accrued Liabilities; Vas99: 341) | loan drawdown and repayment (P1-2j) |
| `InterestExpense` | 5100 (Operating Expenses; Vas99: 635) | interest of a loan: accrual and the part of a repayment not accrued (P1-2j / P1-2k) |
| `InterestPayable` | 2100 (Accrued Liabilities; Vas99: 335) | accrued loan interest (P1-2k) |
| `SalaryExpense` | 5200 Salaries & Benefits | pay slips (gross + overtime) |
| `SalariesPayable` | 2100 Accrued Liabilities | pay slips (net pay), their payment |
| `PayrollWithholdingsPayable` | 2100 (VAS, P1-2, splits insurance and personal income tax) | pay slips (deductions) |
| `VatOutput` | 2200 Tax Payable (Vas99: 3331) | VAT on sales invoices, returns and POS sales (P1-3) |
| `VatInput` | 2200 (shared with VatOutput; Vas99: 1331) | VAT on approved bills (P1-3) |

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
| Bill approved | Dr GRNI / Cr AP | `Bill:<billId>` |
| Payment refunded (Kafka `payment-topic`, only a gateway-confirmed refund) | Dr AccountsReceivable / Cr Cash (method Cash) or Bank; the invoice's `AmountPaid` goes down and a paid invoice is open again | `PaymentRefund:<refundId>` |
| Sales return completed (`SalesOrderReturned`) | Dr SalesReturns / Cr AccountsReceivable = the refund amount (credit note against the order's invoice) | `SalesReturn:<orderId>` |
| Loan registered (P1-2j, `CreateLoanCommand`, option `postDrawdown`, default on) | Dr the receiving account (default Bank) / Cr LoanPayable = the principal, dated the start date | `Loan:<loanId>` |
| Loan installment repaid (`RecordLoanRepaymentCommand`, only for a posted loan) | Dr LoanPayable = principal, Dr InterestPayable = interest accrued, Dr / Cr InterestExpense = interest paid − accrued / Cr the loan's receiving account (default Bank) = principal + interest paid, dated the day repaid | `LoanRepayment:<installmentId>` |
| Loan interest accrued (`AccrueLoanInterestCommand`, per posted active loan and unpaid installment, P1-2k) | Dr InterestExpense / Cr InterestPayable = the interest earned since the last accrual, dated the accrual date | `LoanAccrual:<id from installment and date>` |
| Loan interest accrued by the month-end job (`LoanInterestAccrualJob`, tenants with `AutoAccrueLoanInterest`, P1-2l) | The same entry, as of the last day of the previous month; skipped when that fiscal period is closed | the same `LoanAccrual:<…>` key (only the difference is posted) |
| Loan cancelled (only without repayments, only a posted loan) | Reversal of `Loan:<loanId>` | — |
| Pay slip generated (Kafka `hrm-topic`) | Dr SalaryExpense (gross + overtime) / Cr SalariesPayable (net) and Cr PayrollWithholdingsPayable (deductions); dated the end of the pay period, at most today | `PaySlip:<paySlipId>` |
| Pay slip paid | Dr SalariesPayable / Cr Bank, net pay; only if the slip's salary entry exists | `PaySlipPayment:<paySlipId>` |
| Pay slip cancelled (unpaid only) | mirror of the slip entry | `PaySlipReversal:<paySlipId>` |
| POS sale completed (Kafka `pos-topic`) | Dr Cash (cash tenders) and Dr Bank (all other tenders) / Cr SalesRevenue (subtotal − discount) and Cr VatOutput (tax) | `PosSale:<transactionId>` |
| POS sale voided | mirror of the sale entry | `PosSaleReversal:<transactionId>` |
| POS shift closed, counted ≠ expected cash | shortage: Dr CashOverShort / Cr Cash; overage the reverse | `PosShift:<shiftId>` |
| Stock received (PO goods receipt or manual receipt with a cost) | Dr Inventory / Cr GRNI, qty × unit cost | `StockMovement:<movementId>` |
| Stock issued (`Sold`: order shipped, POS sale) | Dr COGS / Cr Inventory, the cost Warehouse took the stock out at | `StockMovement:<movementId>` |
| Stock adjusted / damaged (approved adjustment, stocktake variance, POS void) | loss: Dr InventoryAdjustment / Cr Inventory; gain: the reverse, at the current cost | `StockMovement:<movementId>` |
| Bill marked paid | Dr AP / Cr Bank | `BillPayment:<billId>` |

* **Idempotent.** The key is stored in `JournalEntries.SourceKey` under a unique index per tenant. A redelivered event finds the
  entry and does nothing — for a payment that includes not crediting the invoice twice.
* A cancelled order whose invoice is paid (even in part) is left alone and logged: it needs a refund (P1-1e).
* The payment's `Method` is PaymentService's enum, a **number** on the Kafka wire (CreditCard 0 … Cash 4). Payments created by the
  order saga always say CreditCard, so they are booked to Bank.
* Single currency (VND). The document currency is not converted (P1-4).

### Stock movements (P1-1b)

Warehouse publishes one `StockMovementRecordedEvent` per movement on RabbitMQ `warehouse_exchange`
(`warehouse.StockMovementRecordedEvent`, queue `accounting-service.warehouse-events`). Two fields were added for the ledger:
`UnitCost` and **`ValueDelta`**, the signed change of the stock value that Warehouse computed (receipt +, issue −, gain +,
loss −). Accounting posts from `MovementType` + `ValueDelta` only, because the sign of `Quantity` differs between the movement
sources. `ValueDelta = 0` (transfer, a manual receipt without a price, a product with no cost yet) books nothing.
* A PO goods receipt is valued at the PO line's unit price (the `PurchaseOrderReceivedDomainEvent` line now carries
  `UnitPrice`, `CurrencyCode` and `GoodsReceiptId`). Warehouse keys the movement on the goods receipt, so a second partial
  receipt of the same product on the same order is stocked and posted (it used to be dropped as a redelivery).
* An issue is valued from the `CostConsumptionLog` rows of that movement (lot cost, or `Inventory.Cost` for products without
  lots); POS sales use `Inventory.Cost`. FIFO / LIFO products without lots therefore cost at the last receipt price; Average is
  weighted.
* GRNI nets out when the receipt and the bill match; its balance is "received, not billed" (credit) or "billed, not received"
  (debit). A manual receipt has no bill, so it leaves a credit on GRNI that accounting clears.
* New posting keys are added to existing tenants lazily: the first posting that finds a key without a mapping row creates it
  from the default account number (no migration). A tenant whose own chart lacks the default account only fails the flow that
  needs that key (`PostingConfigurationException`): the error is logged and the stock movement is acknowledged, not retried — fix the profile; nothing is back-posted.

### POS (P1-1c)

POSService owns the money of a sale (it does not call PaymentService), so the `pos-topic` events carry it: `TransactionCreated` /
`TransactionVoided` have `Amounts` (subtotal, discount, tax, tenders), the shift id and `ExternalPaymentId`; `CashShiftClosed` has the
opening float, counted cash, expected cash and variance. POS tax is exclusive and is **not** reduced by the discount, so the entry is
tenders = (subtotal − discount) + tax. A tender is cash when its method is `Cash` (case-insensitive); every other method name is Bank.
* An event whose tenders do not add up to the total books nothing and logs an error; an event published before the amounts existed
  is skipped, so replaying `pos-topic` from the beginning does not back-post old sales.
* A sale with `ExternalPaymentId` (click-and-collect, paid online) is skipped: the payment flow already booked that money.
* The cost of goods is **not** posted here: Warehouse's `Sold` movement books Dr COGS / Cr Inventory, and a void's stock return books the
  offsetting adjustment. Do not add COGS to the POS entry.
* POS keys `pos-topic` messages by transaction / shift id, so a sale and its void reach Accounting in order.
* Expected cash = opening float + cash tenders of the shift's completed (not voided) sales. A sale voided or synced after the shift was
  closed reverses its ledger entry but is not part of the shift variance already booked (the closed event is final).

### Refunds and returns (P1-1e)

PaymentService now raises `PaymentRefundedEvent` only when the gateway confirmed the refund (`Payment.CompleteRefund`); before, it was raised when the
refund was *requested*, so a declined refund still published it. The event carries the `RefundId` (the ledger's idempotency key), the method, currency and
the confirmation time; events published earlier have no `RefundId` and are not booked. Payment events share a Kafka key per payment, so a refund cannot
overtake its capture.
* The three entries are order-independent. Example for an order of 1000: invoice (AR +1000), payment (AR −1000), return 1000 (AR −1000, i.e. the customer
  is owed 1000), refund 1000 (AR +1000) → 0. Cancelling an order **after** payment now cancels the invoice and reverses the revenue like an unpaid one; the
  cash stays booked and the refund entry arrives with the Payment refund, in either order.
* A return is posted only when the order has an invoice and the refund amount is within the invoice amount (`CompleteReturn` also refuses a refund above the
  order total). Sales allows one return per order, so `SalesReturn:<orderId>` is unique. No VAT is reversed (P1-3).
* **Not done (P1-5):** nothing triggers a Payment refund from `SalesOrderReturned` (a refund is requested by hand through `POST payments/{id}/refund`), returned
  goods are not put back into stock and their cost is not reversed (revenue is reversed, COGS stays), and Sales has no return lines.
* The gateway's refund answer is synchronous (VNPay); a timeout is recorded as a failed refund even if the gateway processed it, and there is no refund IPN.
  An over-refund is now answered with 409 instead of 500.

### Payroll (P1-1d)

HRMService's `PaySlipGeneratedEvent` now carries `GrossSalary`, `OvertimePay`, `Deductions` and `PeriodEnd` next to `NetPay`; a pay slip can be
paid (`POST hrm/payslips/{id}/pay` → `PaySlipPaidEvent`) or, while still pending, cancelled (`…/cancel` → `PaySlipVoidedEvent`). A cancelled slip
frees its period (filtered unique index, HRM migration 007), so the month can be generated again. Events of one slip share a Kafka key so a cancel
cannot overtake its generation.
* HRM's figures are still the stub of ROADMAP P1-7 (5000 gross, 30 % lump deduction, no insurance / PIT split, no employer contributions), so the
  ledger shows whatever HRM computes; the lump deduction goes to one account until P1-7 splits it. Events published before this release have no
  amounts and are skipped (no back-posting); paying such a slip books nothing because its salary entry does not exist.
* A paid slip cannot be cancelled (it needs a payroll correction, not built yet). No cost centre / department dimension: all expense is one account.

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
`Accounting:AutoPosting:Enabled = false` switches automatic posting off (documents only: a person's manual reversal and the period-end closing entry still need the engine, see ChartOfAccounts.md).

## Known simplifications (follow-up plans)

* Bills for goods received before this release stay on the old Dr Inventory rule; new bills debit GRNI.
* Stock that was in the warehouse before the release has no entries, so account 1200 starts below the Warehouse valuation until an
  opening-balance entry is booked by hand. Movements before the release are not back-posted.
* **VAT** (P1-3): Sales, Purchasing and Payment still carry no tax fields; Accounting splits invoices and bills with the tenant's rate (`TenantTaxSettings`, `VatCalculator`). Invoice: Dr AR gross / Cr Revenue net / Cr VatOutput; bill: Dr GRNI net / Dr VatInput / Cr AP gross; return: Dr SalesReturns net / Dr VatOutput / Cr AR gross; rate correction `InvoiceVatAdjust`. With rate 0 every entry is as before.
* **Not posted yet:** restock and cost of returned goods and the payment refund triggered by a return (P1-5), POS refund tenders and cash paid-in / paid-out,
  employer payroll contributions (P1-7).
* Existing invoices and bills (before this release) are not back-posted; demo data keeps its own legacy entries (`SourceKey` empty).

## Operations

* Run Accounting migration `009` (it also fixes the seeded chart's `AccountType` / `NormalBalance` numbering: the seed used 0-based
  values, the application 1-based, so the P&L and trial balance misread the seed accounts).
* Kafka offsets of `AccountingKafkaConsumer` are committed after the message was handled; a failing message is tried 3 times, then
  logged with its topic / partition / offset and skipped. The RabbitMQ consumer republishes up to 5 times, then drops with an error log.
