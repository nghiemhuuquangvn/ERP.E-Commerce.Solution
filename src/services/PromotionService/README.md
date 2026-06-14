# Promotion Service

Promotion and discount management including coupons, promotions, loyalty programs, and seasonal campaigns for the ERP E-Commerce Solution.

## Overview

The Promotion Service manages all marketing incentives:
- **Promotions** — Percentage/fixed amount discounts on products/categories/orders
- **Coupons** — Coupon codes with validity, usage limits, and redemption tracking
- **Loyalty Program** — Points earning, redemption, tiered membership
- **Campaigns** — Seasonal/marketing campaigns with promotion bundles
- **Flash Sales** — Time-limited promotions with inventory allocation

## Core Domain Model

### Aggregates

**Promotion** (Aggregate Root)
- PromotionId, PromotionName
- PromotionType (Percentage, FixedAmount, BuyXGetY, FreeShipping)
- DiscountValue, MaxDiscount (ceiling)
- ApplicableScope (Product, Category, Order, Shipping)
- ApplicableProductIds (list for targeted promotions)
- StartDate, EndDate
- PromotionStatus (Draft, Active, Paused, Expired, Cancelled)
- MinOrderAmount (minimum purchase required)
- MaxUsageLimit, CurrentUsageCount
- MaxUsagePerCustomer

**Coupon** (Aggregate Root)
- CouponId, CouponCode (unique)
- PromotionId (linked promotion)
- CouponStatus (Active, Expired, Redeemed, Cancelled)
- IssuedDate, ExpiryDate
- MaxRedemptions, CurrentRedemptions
- CustomerIds (null = anyone, or list of eligible customers)
- StackableWith (list of coupon codes that can be combined)

**LoyaltyProgram** (Aggregate Root)
- ProgramId, ProgramName
- ProgramLevel (Standard, Silver, Gold, Platinum)
- PointsPerDollar (earning rate)
- PointsExpireAfterDays
- RedemptionRates (points to discount conversion)
- IsActive

**LoyaltyAccount** (Entity)
- AccountId, CustomerId (UserId)
- ProgramId
- CurrentPoints, TotalPointsEarned
- MembershipLevel, JoinDate
- LastActivityDate
- List<LoyaltyTransaction> children

**FlashSale** (Aggregate Root)
- SaleId, SaleName
- ProductIds, Quantities (inventory allocation)
- DiscountPercentage
- StartTime, EndTime
- SaleStatus (Scheduled, Active, Completed, Cancelled)

## Key Business Rules

1. Promotion start date must be before end date
2. Coupon code must be unique across system
3. Coupon cannot be redeemed after expiry date
4. Loyalty points expire if not redeemed within configured days
5. Customer can only redeem coupon if within eligibility list (if specified)
6. FlashSale inventory is reserved and decreases as orders placed
7. Maximum discount ceiling prevents abuse (e.g., -$1000 on $100 order)
8. Stackable coupons can be combined; non-stackable ones override

## Database

**Database Name:** `PromotionDB`

**Key Tables:**
- Promotions, Coupons
- LoyaltyPrograms, LoyaltyAccounts, LoyaltyTransactions
- FlashSales, FlashSaleInventory
- CouponRedemptions, PromotionApplications
- OutboxMessages

## Integration Events

### Published Events

**PromotionActivatedIntegrationEvent**
- Subscribers: POSService, ProductService (frontend), ReportingService

**CouponRedeemedIntegrationEvent**
- Subscribers: ReportingService

**LoyaltyPointsAdjustedIntegrationEvent**
- Subscribers: ReportingService

**FlashSaleStartedIntegrationEvent**
- Subscribers: ProductService (frontend), NotificationService

### Consumed Events

**SalesOrderCreatedIntegrationEvent** (from SO)
→ Apply applicable promotions/coupons

**ProductCreatedIntegrationEvent** (from PRD)
→ Add product to available scope for promotions

## API Endpoints

```
GET    /api/v1/promotions                  - List promotions
POST   /api/v1/promotions                  - Create promotion
GET    /api/v1/promotions/{id}             - Promotion detail
PUT    /api/v1/promotions/{id}             - Update promotion
POST   /api/v1/promotions/{id}/activate    - Activate promotion

GET    /api/v1/coupons                     - List coupons
POST   /api/v1/coupons                     - Create coupon
GET    /api/v1/coupons/{code}              - Validate coupon
POST   /api/v1/coupons/{code}/redeem       - Redeem coupon

GET    /api/v1/loyalty/programs            - List programs
POST   /api/v1/loyalty/programs            - Create program
GET    /api/v1/loyalty/accounts/{id}       - Account detail
POST   /api/v1/loyalty/accounts/{id}/redeem- Redeem points

GET    /api/v1/flash-sales                 - List flash sales
POST   /api/v1/flash-sales                 - Create flash sale
POST   /api/v1/flash-sales/{id}/start      - Start flash sale

POST   /api/v1/promotions/apply            - Apply promotions to order
```

## Dependencies

**Services that depend on Promotion Service:**
- SalesOrderService (promotion application)
- POSService (coupon/promotion validation)
- ProductService (frontend display)
- ReportingService (metrics)

**Services Promotion depends on:**
- SalesOrderService (events)
- ProductService (product information)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — PromotionDB
- **Kafka** — Event streaming
- **MediatR** — CQRS
- **Redis** — Optional: coupon code lookup cache

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
