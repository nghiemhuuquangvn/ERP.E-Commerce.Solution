# Shipping Service

Shipping and logistics management including shipment tracking, carrier integration, and delivery management for the ERP E-Commerce Solution.

## Overview

The Shipping Service manages all fulfillment and delivery operations:
- **Shipments** — Order-to-shipment mapping with tracking
- **Carriers** — Carrier integration (FedEx, UPS, DHL, Local couriers)
- **Shipping Rates** — Dynamic rate calculation based on weight, distance, service level
- **Tracking** — Real-time tracking updates from carriers
- **Delivery** — Proof of delivery, signature capture, delivery photos

## Core Domain Model

### Aggregates

**Shipment** (Aggregate Root)
- ShipmentId, ShipmentNumber
- OrderId (from SalesOrderService)
- ShipmentStatus (Pending, Packed, Shipped, InTransit, Delivered, Failed, Returned)
- TrackingNumber
- CarrierId, CarrierName
- ShippingAddress (address value object)
- EstimatedDeliveryDate, ActualDeliveryDate
- List<ShipmentItem> children
- List<TrackingEvent> children

**Carrier** (Aggregate Root)
- CarrierId, CarrierName (FedEx, UPS, DHL, etc.)
- CarrierType (Domestic, International, LocalCourier)
- IntegrationUrl, ApiKey
- IsActive

**ShippingRate** (Aggregate Root)
- RateId, CarrierId
- WeightMin, WeightMax
- DistanceMin, DistanceMax
- ServiceLevel (Standard, Express, Overnight)
- BaseRate, PerKgRate
- EffectiveDate, ExpiryDate

**TrackingEvent** (Entity, child of Shipment)
- EventId, ShipmentId
- EventType (PickedUp, InTransit, OutForDelivery, Delivered, Delayed, Exception)
- EventDescription, Location
- OccurredAt

**DeliveryProof** (Entity, child of Shipment)
- ProofId, ShipmentId
- RecipientName, SignatureRequired
- SignatureCapture (image/binary)
- PhotoUrl (delivery location photo)
- DeliveryNotes

## Key Business Rules

1. Shipment can only be created after SalesOrderApproved event received
2. Tracking updates must be synced from carrier every 4 hours
3. Shipment cannot be modified after marked as Delivered
4. Shipping rate is locked at time of shipment creation
5. Carrier API credentials must be validated before activating carrier
6. Proof of delivery required before marking shipment as delivered

## Database

**Database Name:** `ShippingDB`

**Key Tables:**
- Shipments, ShipmentItems
- Carriers, ShippingRates
- TrackingEvents, DeliveryProofs
- CarrierIntegrationLogs
- OutboxMessages

## Integration Events

### Published Events

**ShipmentCreatedIntegrationEvent**
- Subscribers: SalesOrderService, NotificationService, ReportingService

**ShipmentShippedIntegrationEvent**
- Published when shipment is handed to carrier
- Subscribers: SalesOrderService, NotificationService, ReportingService

**ShipmentDeliveredIntegrationEvent**
- Subscribers: SalesOrderService, AccountingService, ReportingService

**DeliveryFailedIntegrationEvent**
- Subscribers: SalesOrderService, NotificationService

### Consumed Events

**SalesOrderApprovedIntegrationEvent** (from SO)
→ Create shipment from order items

**SalesOrderCancelledIntegrationEvent** (from SO)
→ Cancel shipment if not yet shipped

## API Endpoints

```
GET    /api/v1/shipments                   - List shipments
POST   /api/v1/shipments                   - Create shipment
GET    /api/v1/shipments/{id}              - Shipment detail
PUT    /api/v1/shipments/{id}              - Update shipment
POST   /api/v1/shipments/{id}/ship         - Ship shipment
GET    /api/v1/shipments/{id}/tracking     - Tracking history

GET    /api/v1/carriers                    - List carriers
POST   /api/v1/carriers                    - Add carrier
GET    /api/v1/carriers/{id}               - Carrier detail
PUT    /api/v1/carriers/{id}               - Update carrier

GET    /api/v1/rates/calculate             - Calculate shipping rate
GET    /api/v1/rates                       - List rates

POST   /api/v1/tracking/sync               - Sync tracking updates
GET    /api/v1/tracking/{id}               - Tracking detail

POST   /api/v1/delivery/proof              - Submit proof of delivery
```

## Dependencies

**Services that depend on Shipping Service:**
- SalesOrderService (shipping status updates)
- ReportingService (shipping metrics)
- NotificationService (delivery notifications)

**Services Shipping depends on:**
- SalesOrderService (events, order data)
- WareHouseService (warehouse location for pickup)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — ShippingDB
- **Kafka** — Event streaming
- **MediatR** — CQRS
- **External:** Carrier APIs (FedEx, UPS, DHL)

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
