# POS Service

Point of Sale system for retail operations including terminals, transactions, inventory sync, and cash management for the ERP E-Commerce Solution.

## Overview

The POS Service manages all in-store retail operations:
- **POS Terminals** — Terminal registration, configuration, offline support
- **POS Transactions** — Sales transactions, payments, returns, exchanges
- **Product Sync** — Real-time product catalog and pricing sync from ProductService
- **Inventory Sync** — Live inventory levels from WareHouseService
- **Promotions** — Apply coupons, discounts, loyalty points
- **Cash Drawer** — Cash management, shift management, reconciliation

## Core Domain Model

### Aggregates

**POSTerminal** (Aggregate Root)
- TerminalId, TerminalNumber, TerminalName
- StoreId, RegisterName
- TerminalStatus (Active, Offline, Maintenance, Suspended)
- OfflineMode (supports offline transactions)
- HardwareConfig (receipt printer, payment terminal, scanner)
- LastSyncTime

**POSTransaction** (Aggregate Root)
- TransactionId, TransactionNumber
- TerminalId, CashierId (UserId from Identity)
- TransactionType (Sale, Return, Exchange)
- TransactionStatus (Draft, Completed, Voided, Refunded)
- TotalAmount, TaxAmount, DiscountAmount
- PaymentMethod (Cash, Card, Mobile, Mixed)
- TransactionTime, CompletedTime
- List<LineItem> children
- List<Tender> children (payment breakdown)

**LineItem** (Entity, child of POSTransaction)
- ItemId, TransactionId
- ProductId, ProductName, Sku
- Quantity, UnitPrice, LineTotal
- Discount, Tax

**Tender** (Entity, child of POSTransaction)
- TenderId, TransactionId
- PaymentType (Cash, CreditCard, DebitCard, GiftCard, Loyalty)
- Amount, ReferenceNumber

**CashShift** (Aggregate Root)
- ShiftId, TerminalId
- CashierId, CashierName
- OpeningBalance, ClosingBalance
- ShiftStart, ShiftEnd
- ShiftStatus (Open, Closed, Reconciled)
- TransactionCount, TotalSales

**LoyaltyTransaction** (Entity, child of POSTransaction)
- LoyaltyTxnId, CustomerId
- PointsEarned, PointsRedeemed
- LoyaltyBalance

## Key Business Rules

1. Terminal must be online/synced before accepting transactions
2. Cash drawer cannot be opened without supervisor code for amounts > threshold
3. Receipt must be printed/emailed before transaction is complete
4. Return is limited to 30 days from original transaction
5. Shift cannot be closed until all transactions are settled
6. Offline transactions must sync back when terminal comes online
7. Inventory held for 5 minutes after sale confirmation

## Database

**Database Name:** `PosDB`

**Key Tables:**
- POSTerminals, POSTransactions, LineItems
- Tenders, CashShifts
- OfflineTransactionQueue
- LoyaltyTransactions, LoyaltyAccounts
- OutboxMessages

## Integration Events

### Published Events

**POSTransactionCompletedIntegrationEvent**
- Subscribers: ProductService (inventory), WareHouseService (stock deduction), ReportingService

**POSReturnInitiatedIntegrationEvent**
- Subscribers: ProductService, WareHouseService, ReportingService

**CashShiftClosedIntegrationEvent**
- Subscribers: ReportingService, AccountingService (if payment settlement needed)

**LoyaltyPointsEarnedIntegrationEvent**
- Subscribers: ReportingService

### Consumed Events

**ProductUpdatedIntegrationEvent** (from PRD)
→ Sync product pricing/details to POS terminals

**InventoryUpdatedIntegrationEvent** (from WH)
→ Sync inventory levels to POS terminals

**PromotionActivatedIntegrationEvent** (from PRM)
→ Sync promotions/coupons to POS terminals

## API Endpoints

```
GET    /api/v1/terminals                   - List terminals
POST   /api/v1/terminals                   - Register terminal
GET    /api/v1/terminals/{id}              - Terminal detail
POST   /api/v1/terminals/{id}/sync         - Sync products & inventory

GET    /api/v1/transactions                - List transactions
POST   /api/v1/transactions                - Create transaction
GET    /api/v1/transactions/{id}           - Transaction detail
POST   /api/v1/transactions/{id}/complete  - Complete transaction
POST   /api/v1/transactions/{id}/void      - Void transaction
POST   /api/v1/transactions/{id}/return    - Process return

GET    /api/v1/cash-shifts                 - List shifts
POST   /api/v1/cash-shifts                 - Open shift
POST   /api/v1/cash-shifts/{id}/close      - Close shift
POST   /api/v1/cash-shifts/{id}/reconcile  - Reconcile shift

GET    /api/v1/loyalty/accounts            - Loyalty accounts
GET    /api/v1/loyalty/points/{id}         - Customer points balance

POST   /api/v1/offline/sync                - Sync offline transactions
```

## Dependencies

**Services that depend on POS Service:**
- ReportingService (POS metrics)
- ProductService (inventory holds)

**Services POS depends on:**
- ProductService (products, pricing)
- WareHouseService (inventory)
- PromotionService (promotions, coupons)
- PaymentService (payment processing - optional for some terminals)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — PosDB
- **Kafka** — Event streaming
- **MediatR** — CQRS
- **Local Storage** — Offline transaction queue (SQLite fallback)

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
