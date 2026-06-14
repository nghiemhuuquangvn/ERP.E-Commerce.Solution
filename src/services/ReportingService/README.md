# Reporting Service

Business intelligence and analytics including dashboards, KPI tracking, and report generation for the ERP E-Commerce Solution.

## Overview

The Reporting Service provides analytics and insights across the entire ERP platform:
- **Sales Analytics** — Orders, revenue, customers, products
- **Inventory Analytics** — Stock levels, movement, warehouse efficiency
- **Financial Analytics** — GL entries, profit/loss, cash flow, tax reporting
- **HR Analytics** — Headcount, payroll, attendance, turnover
- **Operational Metrics** — POS transactions, shipping performance, payment success rates
- **Dashboards** — Real-time KPI dashboards for different user roles

## Architecture

**Reporting Service is a pure Kafka consumer:**
- Subscribes to ALL service topics
- Aggregates events into analytical database (ReportingDB)
- Provides read-only API for dashboards, reports, exports
- No writes to other services; no outbound events

**Design Pattern:** Event Sourcing Read Model
- Each event received → updates fact tables/dimensional tables
- Designed for OLAP (analytics queries), not OLTP (transactional)

## Core Domain Model

### Read Models (Fact & Dimension Tables)

**FactSalesOrder** (Fact Table)
- OrderId, CustomerId, OrderDate
- Amount, Quantity, Discount, Tax
- DeliveryDate, OrderStatus
- ProductCategory, Region
- Used for: Revenue analysis, order trends

**FactPayment** (Fact Table)
- PaymentId, OrderId, PaymentDate
- Amount, PaymentMethod, PaymentStatus
- ProcessingTime (auth → capture duration)
- Used for: Payment success rates, processing metrics

**FactShipment** (Fact Table)
- ShipmentId, OrderId, ShipmentDate
- CarrierId, DeliveryDate, ShippingCost
- DeliveryTime (shipment → delivery duration)
- Used for: Shipping performance, logistics KPIs

**FactInventory** (Fact Table - snapshot)
- WarehouseId, ProductId, SnapshotDate
- Quantity, StockValue, Turnover
- Used for: Stock levels, inventory health

**FactGLEntry** (Fact Table)
- EntryId, EntryDate, Amount
- AccountId, AccountType, Reference
- FiscalPeriod
- Used for: GL reporting, P&L, balance sheet

**DimProduct** (Dimension Table)
- ProductId, Sku, Name, Category
- Price, Cost, Status

**DimCustomer** (Dimension Table)
- CustomerId, Name, Email, Country
- CrmStatus (Lead, Opportunity, Customer)
- LifetimeValue, SegmentId

**DimDate** (Dimension Table)
- DateId, Date, Year, Quarter, Month, Week, DayOfWeek

## Key Business Rules

1. Reporting data is read-only (snapshots fed by events)
2. Dimension updates are slowly-changing dimension (SCD Type 2) — maintain history
3. Nightly batch jobs aggregate daily snapshots
4. Real-time dashboards query from in-memory cache (Redis) for performance
5. Historical data retained for 5+ years for trend analysis
6. Personal data (PII) is excluded from reports unless explicitly requested with audit log
7. Report exports are logged for compliance/audit

## Database

**Database Name:** `ReportingDB`

**Key Tables (Fact & Dimension):**
- FactSalesOrder, FactPayment, FactShipment, FactInventory, FactGLEntry
- DimProduct, DimCustomer, DimDate, DimCarrier, DimWarehouse
- ReportDefinitions, ReportSchedules
- ReportExecutionLogs, DataQualityMetrics
- Kafka Consumer Offset Tracking

## Consumed Integration Events

The Reporting Service consumes events from ALL 13 services:

**From SalesOrderService:**
- SalesOrderCreatedIntegrationEvent
- SalesOrderApprovedIntegrationEvent
- SalesOrderCancelledIntegrationEvent
- SalesOrderDeliveredIntegrationEvent

**From ProductService:**
- ProductCreatedIntegrationEvent
- ProductUpdatedIntegrationEvent
- ProductPriceChangedIntegrationEvent

**From PaymentService:**
- PaymentAuthorizedIntegrationEvent
- PaymentCapturedIntegrationEvent
- PaymentFailedIntegrationEvent
- RefundProcessedIntegrationEvent

**From ShippingService:**
- ShipmentCreatedIntegrationEvent
- ShipmentShippedIntegrationEvent
- ShipmentDeliveredIntegrationEvent

**From WareHouseService:**
- InventoryReservedIntegrationEvent
- InventoryReleasedIntegrationEvent

**From AccountingService:**
- JournalEntryPostedIntegrationEvent
- InvoiceCreatedIntegrationEvent

**From HRMService:**
- EmployeeHiredIntegrationEvent
- PaySlipGeneratedIntegrationEvent

**From CRMService:**
- LeadCreatedIntegrationEvent
- OpportunityWonIntegrationEvent

**From PromotionService:**
- PromotionActivatedIntegrationEvent
- CouponRedeemedIntegrationEvent

**From POSService:**
- POSTransactionCompletedIntegrationEvent

## API Endpoints

```
GET    /api/v1/dashboards                  - List available dashboards
GET    /api/v1/dashboards/{id}             - Get dashboard data
POST   /api/v1/dashboards/{id}/refresh     - Refresh dashboard cache

GET    /api/v1/reports                     - List report definitions
POST   /api/v1/reports/{id}/execute        - Generate report
GET    /api/v1/reports/{id}/results        - Get report results
POST   /api/v1/reports/{id}/schedule       - Schedule report

GET    /api/v1/analytics/sales             - Sales analytics
GET    /api/v1/analytics/inventory         - Inventory analytics
GET    /api/v1/analytics/financial         - Financial analytics
GET    /api/v1/analytics/operational       - Operational metrics

GET    /api/v1/exports/{id}                - Export report (CSV, Excel, PDF)
GET    /api/v1/data-quality                - Data quality metrics
```

## Sample Dashboards

1. **Executive Dashboard** — Revenue, orders, customers, cash flow
2. **Sales Dashboard** — Orders, conversion, customer acquisition, churn
3. **Inventory Dashboard** — Stock levels, turnover, warehouse efficiency
4. **Financial Dashboard** — Revenue, expenses, profit margin, tax liability
5. **Shipping Dashboard** — On-time delivery %, carrier performance, cost per unit
6. **POS Dashboard** — Terminal sales, cash drawer balance, average transaction
7. **HR Dashboard** — Headcount, payroll, attendance, turnover rate

## Dependencies

**Reporting Service depends on:**
- All 13 other services (event consumers)

**Services that depend on Reporting Service:**
- None (read-only; provides insights only)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — Fact/Dimension schema (OLAP optimized)
- **Kafka** — Event consumption (all topics)
- **MediatR** — Event handler registration
- **Redis** — Dashboard cache (optional)
- **Power BI** or **Grafana** — External visualization (optional)

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
