# Notification Service

Multi-channel notification delivery including email, SMS, push notifications, and in-app messages for the ERP E-Commerce Solution.

## Overview

The Notification Service manages all customer and internal communications:
- **Email Notifications** — Transactional and marketing emails (SMTP integration)
- **SMS Notifications** — Text message delivery (Twilio, AWS SNS)
- **Push Notifications** — Mobile app push alerts
- **In-App Messages** — Web/app in-application notifications
- **Notification Templates** — Template management with variable substitution
- **Delivery Tracking** — Delivery status and retry logic

## Core Domain Model

### Aggregates

**NotificationTemplate** (Aggregate Root)
- TemplateId, TemplateName
- NotificationType (Email, Sms, Push, InApp)
- TemplateContent (with {variable} placeholders)
- Subject (for email)
- IsActive, CreatedDate, UpdatedDate

**Notification** (Aggregate Root)
- NotificationId, NotificationNumber
- RecipientId (UserId or email address)
- NotificationType
- TemplateId
- VariablesJson (serialized {key: value} for template substitution)
- NotificationStatus (Queued, Sent, Delivered, Failed, Bounced)
- Priority (Low, Normal, High, Critical)
- ScheduledFor (for delayed sending)
- CreatedAt, SentAt, DeliveredAt
- RetryCount, LastRetryAt
- ErrorMessage

**NotificationLog** (Entity)
- LogId, NotificationId
- EventType (Created, Sent, Delivered, Failed, Bounced)
- EventTimestamp, Details

**NotificationPreference** (Entity)
- PreferenceId, UserId
- NotificationType (OrderConfirmation, Shipment, Promotion, Newsletter, etc.)
- ChannelPreferences (Email, Sms, Push, InApp - which channels enabled)
- UnsubscribeFrom (list of notification types)
- OptInAt, OptOutAt

## Key Business Rules

1. Notification can only be sent if recipient has opted in for that notification type
2. Failed notifications retry up to 3 times with exponential backoff (5min, 15min, 1hour)
3. Templates must have valid variable names; substitution fails if variable missing
4. High-priority notifications skip queue and send immediately
5. Email bounces automatically unsubscribe recipient
6. SMS cannot exceed 160 characters (or split into multiple)
7. Push notifications limited to 5 per user per day (configurable)

## Database

**Database Name:** `NotificationDB`

**Key Tables:**
- NotificationTemplates
- Notifications, NotificationLogs
- NotificationPreferences
- SmsBalances, EmailQuotas
- FailedDeliveryQueue
- OutboxMessages

## Integration Events

The Notification Service is a **pure consumer** of events from all services:

### Consumed Events

From **SalesOrderService:**
- SalesOrderCreatedIntegrationEvent → Send order confirmation
- SalesOrderApprovedIntegrationEvent → Send order approved
- SalesOrderCancelledIntegrationEvent → Send cancellation

From **ShippingService:**
- ShipmentCreatedIntegrationEvent → Send shipment notification
- ShipmentShippedIntegrationEvent → Send tracking info
- ShipmentDeliveredIntegrationEvent → Send delivery confirmation

From **PaymentService:**
- PaymentFailedIntegrationEvent → Send payment failure alert
- RefundProcessedIntegrationEvent → Send refund confirmation

From **HRMService:**
- EmployeeHiredIntegrationEvent → Send welcome email
- PaySlipGeneratedIntegrationEvent → Send payslip notification

From **IdentityService** (`identity-topic` — the only topic with a live consumer today,
`IdentityEventConsumer`; see note below):
- TwoFactorCodeGeneratedIntegrationEvent → Send the 2FA one-time code via SMS or Email
  (`TWO_FACTOR_CODE` template, `{code}`/`{expiryMinutes}` placeholders) — Channel is
  chosen per the event's `Method` field, not a fixed channel
- UserCreatedIntegrationEvent → Send welcome email (name fields blank — IdentityService's
  underlying `UserRegisteredDomainEvent` doesn't carry a first/last name split)

> **Live consumer status:** as of the 2FA feature, `IdentityEventConsumer` is the
> **first and only** real Kafka consumer in this service — it subscribes only
> `identity-topic`. Every other "Consumed Events" entry in this README (SalesOrder,
> Shipping, Payment, HRM, CRM, ...) describes the intended design; the corresponding
> `INotification` + handler classes already exist in `IntegrationEventHandlers.cs`, but
> nothing dispatches Kafka messages to them yet for those other 12 topics. When wiring
> the next one, copy `IdentityEventConsumer`'s pattern (itself mirrored from
> HRMService's `MasterDataEventConsumer`) rather than assuming the handler classes are
> already live.

From **CRMService:**
- CaseOpenedIntegrationEvent → Send case confirmation
- CaseResolvedIntegrationEvent → Send resolution notification

From **PromotionService:**
- FlashSaleStartedIntegrationEvent → Send flash sale alert
- PromotionActivatedIntegrationEvent → Send promotion announcement

## API Endpoints

```
GET    /api/v1/templates                   - List templates
POST   /api/v1/templates                   - Create template
GET    /api/v1/templates/{id}              - Template detail
PUT    /api/v1/templates/{id}              - Update template

GET    /api/v1/notifications               - List notifications
POST   /api/v1/notifications               - Send notification
GET    /api/v1/notifications/{id}          - Notification detail
GET    /api/v1/notifications/{id}/logs     - Delivery logs

GET    /api/v1/preferences/{userId}        - Get preferences
PUT    /api/v1/preferences/{userId}        - Update preferences
POST   /api/v1/preferences/{userId}/unsubscribe - Unsubscribe

GET    /api/v1/delivery-status/{id}        - Check delivery status
POST   /api/v1/test-notification           - Send test notification
```

## Dependencies

**Services that Notification depends on:**
- All 13 other services (event consumers)

**Services that depend on Notification Service:**
- None (purely event-driven consumer)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — NotificationDB
- **Kafka** — Event consumption
- **MediatR** — Command processing
- **External APIs:**
  - **SMTP** — Email (SendGrid, AWS SES, or custom)
  - **Twilio** or **AWS SNS** — SMS
  - **Firebase Cloud Messaging** — Push notifications
  - **Custom** — In-app message broker

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
