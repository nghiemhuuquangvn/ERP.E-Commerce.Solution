# Payment Service

Payment processing including payment transactions, refunds, and reconciliation for the ERP E-Commerce Solution.

## Overview

The Payment Service manages all financial transactions:
- **Payment Transactions** — Credit card, debit card, bank transfer, digital wallet
- **Payment Methods** — Customer payment method storage and management
- **Refunds** — Refund processing and tracking
- **Reconciliation** — Bank/gateway reconciliation and settlement
- **Payment Gateway Integration** — Integration with Stripe, PayPal, etc.

## Core Domain Model

### Aggregates

**PaymentTransaction** (Aggregate Root)
- TransactionId, TransactionNumber
- OrderId (from SalesOrderService)
- PaymentMethodId
- Amount, Currency
- TransactionStatus (Pending, Processing, Authorized, Captured, Failed, Declined)
- GatewayReference (external transaction ID from payment gateway)
- CreatedAt, CompletedAt

**PaymentMethod** (Aggregate Root)
- MethodId, CustomerId (UserId)
- PaymentType (CreditCard, DebitCard, BankTransfer, Wallet, OnlineWallet)
- IsDefault, IsActive
- ExpiryDate (for cards)
- MaskedDetails (last 4 digits, cardholder name)

**Refund** (Aggregate Root)
- RefundId, RefundNumber
- TransactionId
- Amount, Reason (CustomerRequest, Duplicate, Fraud, Other)
- RefundStatus (Pending, Processed, Failed, Disputed)
- GatewayRefundRef
- InitiatedAt, CompletedAt

**Settlement** (Aggregate Root)
- SettlementId, SettlementDate
- GatewayName (e.g., Stripe, PayPal)
- TotalAmount, TransactionCount
- SettlementStatus (Pending, Reconciled, Disputed)
- List<TransactionReference> children

## Key Business Rules

1. Payment can only be captured if previously authorized
2. Refund amount cannot exceed original transaction amount
3. Refund can only be initiated for completed transactions
4. Payment method must be validated before storing
5. Settlement must be reconciled daily against gateway records
6. Double-encryption required for sensitive payment data

## Database

**Database Name:** `PaymentDB`

**Key Tables:**
- PaymentTransactions, PaymentMethods
- Refunds, RefundLines
- Settlements, TransactionSettlements
- PaymentGatewayConfigs
- OutboxMessages

## Integration Events

### Published Events

**PaymentAuthorizedIntegrationEvent**
- Subscribers: SalesOrderService, ReportingService

**PaymentCapturedIntegrationEvent**
- Subscribers: AccountingService, SalesOrderService, ReportingService

**PaymentFailedIntegrationEvent**
- Subscribers: NotificationService, SalesOrderService

**RefundProcessedIntegrationEvent**
- Subscribers: AccountingService, SalesOrderService, ReportingService

**SettlementCompletedIntegrationEvent**
- Subscribers: AccountingService, ReportingService

### Consumed Events

**SalesOrderCreatedIntegrationEvent** (from SO)
→ Create payment transaction for order

**SalesOrderCancelledIntegrationEvent** (from SO)
→ Initiate refund if payment was captured

## API Endpoints

```
GET    /api/v1/transactions                - List transactions
POST   /api/v1/transactions                - Create transaction
GET    /api/v1/transactions/{id}           - Transaction detail
POST   /api/v1/transactions/{id}/authorize - Authorize payment
POST   /api/v1/transactions/{id}/capture   - Capture payment
POST   /api/v1/transactions/{id}/void      - Void transaction

GET    /api/v1/payment-methods             - List methods
POST   /api/v1/payment-methods             - Add payment method
GET    /api/v1/payment-methods/{id}        - Method detail
DELETE /api/v1/payment-methods/{id}        - Delete method

GET    /api/v1/refunds                     - List refunds
POST   /api/v1/refunds                     - Create refund
GET    /api/v1/refunds/{id}                - Refund detail
POST   /api/v1/refunds/{id}/process        - Process refund

GET    /api/v1/settlements                 - List settlements
POST   /api/v1/settlements/reconcile       - Reconcile settlement
GET    /api/v1/settlements/{id}            - Settlement detail

POST   /api/v1/webhooks/gateway            - Handle gateway webhooks
```

## Dependencies

**Services that depend on Payment Service:**
- SalesOrderService (payment processing)
- AccountingService (GL entries)
- ReportingService (payment metrics)
- NotificationService (payment notifications)

**Services Payment depends on:**
- SalesOrderService (events)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — PaymentDB
- **Kafka** — Event streaming
- **MediatR** — CQRS
- **External:** Stripe/PayPal APIs

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
