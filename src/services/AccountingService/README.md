# Accounting Service

Financial management including General Ledger, Accounts Receivable (AR), and Accounts Payable (AP) for the ERP E-Commerce Solution.

## Overview

The Accounting Service manages all financial transactions and reporting:
- **General Ledger (GL)** — Chart of accounts, journal entries, posting
- **Accounts Receivable (AR)** — Sales invoices, payments, aging reports
- **Accounts Payable (AP)** — Vendor bills, payments, expense tracking
- **Fiscal Periods** — Month/quarter/year accounting periods with open/close workflow
- **Tax Rates** — Tax configurations for different jurisdictions

**Key Characteristic:** Pure consumer of events from SalesOrderService, PurchaseOrderService, WareHouseService, and PaymentService. Creates GL entries based on events published by other services.

## Core Domain Model

### Aggregates

**Account** (Aggregate Root)
- AccountId, AccountNumber, Name
- AccountType (Asset, Liability, Equity, Revenue, Expense)
- SubType
- NormalBalance (Debit/Credit)

**JournalEntry** (Aggregate Root)
- EntryId, JournalEntryNumber
- FiscalPeriodId
- Reference (e.g., Invoice#, Bill#)
- Description
- List<JournalEntryLine> (Debit/Credit lines)

**Invoice** (AR Aggregate Root)
- InvoiceId, InvoiceNumber
- CustomerId, OrderId (SalesOrderId - cross-service reference)
- Amount, DueDate, PaymentTerms
- InvoiceStatus (Draft, Sent, PartiallyPaid, Paid, Void)

**Bill** (AP Aggregate Root)
- BillId, BillNumber
- SupplierId, POId (PurchaseOrderId)
- Amount, DueDate, PaymentTerms
- BillStatus (Draft, Approved, PartiallyPaid, Paid, Void)

**FiscalPeriod** (Aggregate Root)
- PeriodId, PeriodName (e.g., "2026-Q1")
- StartDate, EndDate
- Status (Open, Closed)

**TaxRate** (Entity)
- TaxRateId, Name (e.g., "VAT 10%"), Rate
- EffectiveDate, ExpiryDate

## Key Business Rules

1. Every financial transaction creates a balanced JournalEntry (debits = credits)
2. Closed FiscalPeriods cannot receive new JournalEntries
3. Invoices follow state machine: Draft → Sent → PartiallyPaid → Paid → Void
4. Bills follow state machine: Draft → Approved → PartiallyPaid → Paid → Void
5. AR and AP aging reports calculated from due dates

## Database

**Database Name:** `AccountingDB`

**Key Tables:**
- Accounts
- JournalEntries, JournalEntryLines
- Invoices, InvoiceLines
- Bills, BillLines
- TaxRates, FiscalPeriods
- OutboxMessages

## Integration Events

### Published Events

**InvoiceCreatedIntegrationEvent**
- Published when AR invoice is created from SalesOrderApprovedIntegrationEvent
- Subscribers: NotificationService, ReportingService

**InvoicePaymentReceivedIntegrationEvent**
- Published when payment is matched to invoice
- Subscribers: SalesOrderService, ReportingService

**BillApprovedIntegrationEvent**
- Published when AP bill is approved for payment
- Subscribers: ReportingService

**JournalEntryPostedIntegrationEvent**
- Published when GL entry is posted
- Subscribers: ReportingService, AuditLog

### Consumed Events

**SalesOrderApprovedIntegrationEvent** (from SO)
→ Create AR Invoice + GL entries for revenue recognition

**SalesOrderCancelledIntegrationEvent** (from SO)
→ Void Invoice + reverse GL entries

**PurchaseOrderReceivedIntegrationEvent** (from PO)
→ Create AP Bill + GL entries for inventory cost

**PaymentTransactionCompletedIntegrationEvent** (from PAY)
→ Match payment to Invoice + create GL entries

**InventoryUpdatedIntegrationEvent** (from WH)
→ Update inventory asset valuation GL entries

## API Endpoints

Verified directly against the controllers (this section previously documented
several endpoints — `send`/`void` on invoices, a flat `/api/v1/reports/*`
path, an `ap-aging`/`ar-aging` pair on Reports — that don't actually exist;
corrected below). Every list/detail action requires
`accounting:{entity}:view`, every create/status-change action requires
`accounting:{entity}:manage` (see `docs/frontend/accounting-admin-guide.md`
for the Admin UI built against this API and the permission-vocabulary gap
that was fixed alongside it).

```
GET    /api/v1/accounts                              - Chart of accounts (no pagination)
POST   /api/v1/accounts                               - Create GL account
GET    /api/v1/accounts/{id}                          - Account detail
                                                         (no update/delete)

GET    /api/v1/journal-entries?fiscalPeriodId=&pageNumber=&pageSize=
                                                       - List journal entries (plain array, no total count)
POST   /api/v1/journal-entries                        - Create AND post an entry (one step, no draft state)
GET    /api/v1/journal-entries/{id}                   - Entry detail (with Lines[])
                                                         (no update/delete)

GET    /api/v1/invoices?customerId=&status=&pageNumber=&pageSize=
                                                       - List AR invoices (plain array, no total count)
POST   /api/v1/invoices                                - Create invoice
GET    /api/v1/invoices/{id}                          - Invoice detail
POST   /api/v1/invoices/{id}/mark-paid                 - Mark paid
GET    /api/v1/invoices/ar-aging?asOfDate=            - AR aging report
                                                         (no update/delete/send/void)

GET    /api/v1/bills?supplierId=&status=&pageNumber=&pageSize=
                                                       - List AP bills (plain array, no total count)
POST   /api/v1/bills                                   - Create bill
GET    /api/v1/bills/{id}                             - Bill detail
POST   /api/v1/bills/{id}/approve                      - Approve bill
POST   /api/v1/bills/{id}/mark-paid                    - Mark paid
GET    /api/v1/bills/ap-aging?asOfDate=               - AP aging report
                                                         (no update/delete)

GET    /api/v1/fiscal-periods                         - List periods (no pagination)
POST   /api/v1/fiscal-periods                          - Open a period
GET    /api/v1/fiscal-periods/{id}                    - Period detail
POST   /api/v1/fiscal-periods/{id}/close               - Close period
                                                         (no update/delete)

GET    /api/v1/tax-rates?activeOnly=                  - List tax rates (no pagination)
POST   /api/v1/tax-rates                               - Create tax rate
GET    /api/v1/tax-rates/{id}                         - Tax rate detail
PUT    /api/v1/tax-rates/{id}                          - Update (the only entity with a real update endpoint)
POST   /api/v1/tax-rates/{id}/deactivate               - Deactivate

GET    /api/v1/bank-accounts?activeOnly=              - List bank accounts (no pagination)
POST   /api/v1/bank-accounts                           - Create bank account (no update/delete)
GET    /api/v1/bank-accounts/{id}/statements           - List statements
POST   /api/v1/bank-accounts/{id}/statements           - Add a statement
POST   /api/v1/bank-accounts/{id}/statements/{statementId}/reconcile
                                                       - Reconcile a statement

GET    /api/v1/accounting/reports/trial-balance?fiscalPeriodId=&fromDate=&toDate=&hideZeroBalance=
GET    /api/v1/accounting/reports/profit-and-loss?fromDate=&toDate=   (both required)
GET    /api/v1/accounting/reports/balance-sheet?asOfDate=
                                                       - Read-only (accounting:reports:view only,
                                                         no manage variant). AR/AP aging live on the
                                                         Invoices/Bills controllers above, not here.
```

## Dependencies

**Services that depend on Accounting Service:**
- ReportingService (consumes GL entry events)

**Services Accounting depends on:**
- SalesOrderService (events)
- PurchaseOrderService (events)
- PaymentService (events)
- WareHouseService (events)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — AccountingDB
- **Kafka** — Event streaming
- **MediatR** — CQRS

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
