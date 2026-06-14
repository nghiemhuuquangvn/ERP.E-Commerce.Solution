# Product Service

Master product catalog and pricing management for the ERP E-Commerce Solution.

## Overview

The Product Service is the foundational catalog service. It manages:
- **Product Master Data** — SKUs, names, descriptions, categories
- **Product Variants** — Size, color, dimensions (each variant has unique barcode/SKU)
- **Pricing** — Price lists with effective date ranges per variant
- **Attributes** — Product attributes (color, size, material) and their values
- **Images** — Product images with sort order

**Key Characteristic:** All other services reference `ProductId` from this service but **never query ProductDB directly**. They consume `ProductUpdatedIntegrationEvent` to maintain local read models.

## Core Domain Model

### Aggregates

**Product** (Aggregate Root)
- ProductId, SKU, Name, Description
- ProductType (Physical, Digital, Service, Bundle)
- ProductStatus (Draft, Active, Discontinued)
- List<ProductVariant> children
- List<ProductImage> children
- List<ProductAttribute> references

**ProductVariant** (Entity, child of Product)
- Sku (globally unique)
- Barcode
- Weight, Dimensions
- Cost

**Category** (Aggregate Root)
- CategoryId, Name
- ParentCategoryId (hierarchical)
- SortOrder

**PriceList** (Aggregate Root)
- PriceListName (e.g., "Retail", "Wholesale")
- List<PriceListEntry> children

**PriceListEntry** (Entity, child of PriceList)
- ProductVariantId (implicit via SKU lookup)
- Price
- StartDate, EndDate (effective date range)

### Value Objects
- Money (Amount, Currency)
- Dimension (Length, Width, Height, Unit)

## Key Business Rules

1. A Product must have at least one Variant (base SKU)
2. ProductStatus can only transition to Active after all required fields are completed
3. SKU must be globally unique across all variants
4. PriceList entries must have non-overlapping date ranges per variant per list
5. Products can only be deleted (soft-delete) if they have no active orders
6. Images are ordered by SortOrder; the first image is the product thumbnail

## Database

**Database Name:** `ProductDB`

**Key Tables:**
- Products
- ProductVariants
- Categories
- ProductAttributes
- ProductAttributeValues
- PriceLists
- PriceListEntries
- ProductImages
- OutboxMessages

## Integration Events

### Published Events

**ProductCreatedIntegrationEvent**
- Published when a new product is published to Active status
- Subscribers: WareHouseService (cache), SaleOrderService (cache), POSService (cache)

**ProductUpdatedIntegrationEvent**
- Published when product details (name, description, attributes) change
- Subscribers: All dependent services (refresh local read models)

**ProductPriceChangedIntegrationEvent**
- Published when a price list entry is created/updated
- Subscribers: SaleOrderService (update quotes), POSService (refresh pricing)

**ProductDiscontinuedIntegrationEvent**
- Published when product is set to Discontinued status
- Subscribers: SaleOrderService (prevent new orders), WareHouseService (alert low-stock triggers)

### Consumed Events

**None** — ProductService is upstream only; no dependencies on other services.

## API Endpoints

```
GET    /api/v1/products                    - List products (with filters: category, status)
POST   /api/v1/products                    - Create product (Draft status)
GET    /api/v1/products/{id}               - Product detail with all variants
PUT    /api/v1/products/{id}               - Update product details
POST   /api/v1/products/{id}/publish       - Transition to Active (publish to catalog)
POST   /api/v1/products/{id}/discontinue   - Transition to Discontinued

GET    /api/v1/products/{id}/variants      - List variants for a product
POST   /api/v1/products/{id}/variants      - Add new variant (SKU, barcode, cost)
PUT    /api/v1/products/{id}/variants/{variantId} - Update variant

GET    /api/v1/categories                  - List product categories (tree structure)
POST   /api/v1/categories                  - Create category
GET    /api/v1/categories/{id}             - Category detail with products
PUT    /api/v1/categories/{id}             - Update category

GET    /api/v1/price-lists                 - List all price lists
POST   /api/v1/price-lists                 - Create new price list
GET    /api/v1/price-lists/{id}            - Price list detail
GET    /api/v1/price-lists/{id}/entries    - Price entries in this list
POST   /api/v1/price-lists/{id}/entries    - Add/update price entry (with date range)
PUT    /api/v1/price-lists/{id}/entries/{entryId} - Update price entry

GET    /api/v1/products/search             - Full-text search by name, description, SKU
GET    /api/v1/products/by-sku/{sku}       - Lookup product variant by SKU
GET    /api/v1/products/by-barcode/{code}  - Lookup variant by barcode (POS point-of-sale use)

GET    /api/v1/products/{id}/images        - List product images
POST   /api/v1/products/{id}/images        - Upload product image
PUT    /api/v1/products/{id}/images/{imageId} - Update image sort order
DELETE /api/v1/products/{id}/images/{imageId} - Delete image
```

## Dependencies

**Services that depend on Product Service:**
- SaleOrderService (references ProductId, consumes ProductUpdatedIntegrationEvent)
- WareHouseService (consumes ProductCreatedIntegrationEvent, caches variant SKU↔ProductId mapping)
- POSService (consumes ProductUpdatedIntegrationEvent for pricing/stock)
- PromotionService (references ProductId for promotion conditions)
- ReportingService (consumes ProductUpdatedIntegrationEvent for analytics)

**Product Service depends on:**
- None (upstream only)

## Technology Stack

- **.NET 10** — Runtime
- **EF Core** — ORM, Code-First migrations
- **SQL Server** — ProductDB database
- **Kafka** — Integration event streaming
- **Swagger** — API documentation
- **MediatR** — CQRS (Commands, Queries, Handlers)
- **FluentValidation** — Input validation

## Deployment

**Local Development:**
```bash
# Database
sqlcmd -S localhost -Q "CREATE DATABASE ProductDB"

# Run migrations
dotnet ef database update

# Start service
dotnet run
# Runs on http://localhost:5008
```

**Docker:**
```bash
docker build -t product-service:latest .
docker run -p 5008:80 -e ConnectionStrings__ProductDB="..." product-service:latest
```

**Kubernetes:**
```bash
kubectl apply -f deployment/k8s/product-service.yaml
```

## Testing

**Unit Tests:**
```bash
dotnet test --filter "Category=Unit"
```

**Integration Tests (requires database):**
```bash
dotnet test --filter "Category=Integration"
```

## Monitoring

**Kafka Topics:** `product-topic`
- ProductCreatedIntegrationEvent
- ProductUpdatedIntegrationEvent
- ProductPriceChangedIntegrationEvent
- ProductDiscontinuedIntegrationEvent

**Outbox Polling:** Every 5 seconds via OutboxProcessor background job

**Key Metrics:**
- Products in catalog (total, active, discontinued)
- Variants per product (avg)
- Price list entries (total)
- Search query latency

## Common Tasks

### Add a New Product

1. `POST /api/v1/products` with ProductType, Name
2. `POST /api/v1/products/{id}/variants` to add SKU and barcode
3. `POST /api/v1/price-lists/{id}/entries` to set pricing
4. `POST /api/v1/products/{id}/images` to upload images
5. `POST /api/v1/products/{id}/publish` to set status=Active → triggers ProductCreatedIntegrationEvent

### Update Pricing

1. `POST /api/v1/price-lists/{id}/entries` with new price and effective date range
2. ProductPriceChangedIntegrationEvent published → dependent services refresh cache
3. Existing orders continue with original pricing; new orders use new price

### Discontinue Product

1. `POST /api/v1/products/{id}/discontinue`
2. ProductDiscontinuedIntegrationEvent published
3. SaleOrderService rejects new orders for this product
4. WareHouseService stops replenishment triggers

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
