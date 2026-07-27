# Purchase Order (PO) Service API Documentation

## Overview

The Purchase Order Service API provides endpoints for managing purchase orders and suppliers in the ERP E-Commerce Solution. The API follows RESTful conventions and implements CQRS (Command Query Responsibility Segregation) pattern with MediatR.

**Base URL:** `http://localhost:5003/api`
**Port:** 5003

## Authentication & Authorization

### JWT Authentication
All endpoints (except specified exceptions) require JWT Bearer token authentication.

**Header Format:**
```
Authorization: Bearer <jwt_token>
```

### User Identity Extraction
- The API extracts user ID from JWT claims: `sub` claim or `Name` claim
- This user ID is automatically included in audit fields (`CreatedBy`, `UpdatedBy`, etc.)

### Role-Based Access Control (RBAC)

| Endpoint | Role Required | Description |
|----------|---------------|-------------|
| `POST /purchase-orders` | User (Any Authenticated User) | Create purchase order |
| `GET /purchase-orders/{id}` | User | View purchase order details |
| `GET /purchase-orders` | User | List purchase orders |
| `POST /purchase-orders/{id}/submit` | User | Submit draft PO |
| `POST /purchase-orders/{id}/approve` | **Manager, Admin** | Approve submitted PO |
| `POST /purchase-orders/{id}/cancel` | **Manager, Admin** | Cancel purchase order |
| `POST /purchase-orders/{id}/items` | User | Add item to PO |
| `DELETE /purchase-orders/{id}/items/{itemId}` | User | Remove item from PO |
| `POST /suppliers` | **Admin, Manager** | Create supplier |
| `GET /suppliers/{id}` | User | View supplier details |
| `GET /suppliers` | User | List suppliers |
| `PUT /suppliers/{id}` | **Admin, Manager** | Update supplier |
| `POST /suppliers/{id}/deactivate` | **Admin** | Deactivate supplier |
| `GET /purchase-requests` | Requires `purchase_request:view` (added 2026-07-27) | List suggestion-only Purchase Requests |
| `POST /purchase-requests/{id}/dismiss` | Requires `purchase_request:manage` (added 2026-07-27) | Dismiss a suggestion |
| `POST /purchase-requests/{id}/convert` | Requires `purchase_request:manage` (added 2026-07-27) | Mark a suggestion acted-upon |

## API Endpoints

### Purchase Orders

#### 1. Create Purchase Order
**Endpoint:** `POST /purchase-orders`
**Authentication:** Required
**Role:** Any authenticated user

**Request Body:**
```json
{
  "poNumber": "PO-2024-001",
  "supplierId": "550e8400-e29b-41d4-a716-446655440000",
  "expectedDeliveryDate": "2024-07-01T00:00:00Z",
  "notes": "Urgent order"
}
```

**Response:** `201 Created`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "poNumber": "PO-2024-001",
  "supplierId": "550e8400-e29b-41d4-a716-446655440000",
  "supplier": null,
  "status": "Draft",
  "orderDate": "2024-06-14T10:30:00Z",
  "expectedDeliveryDate": "2024-07-01T00:00:00Z",
  "totalAmount": 0,
  "currency": "USD",
  "notes": "Urgent order",
  "items": []
}
```

**Validation:**
- `poNumber`: Required, max 50 characters
- `supplierId`: Required, must be valid GUID
- `expectedDeliveryDate`: Must be in the future (if provided)

---

#### 2. Get Purchase Order by ID
**Endpoint:** `GET /purchase-orders/{id}`
**Authentication:** Required
**Role:** Any authenticated user

**Path Parameters:**
- `id` (GUID): Purchase order ID

**Response:** `200 OK`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "poNumber": "PO-2024-001",
  "supplierId": "550e8400-e29b-41d4-a716-446655440000",
  "supplier": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "code": "SUP-001",
    "name": "Supplier Company",
    "contactPerson": "John Doe",
    "email": "john@supplier.com",
    "phone": "123-456-7890",
    "isActive": true,
    "address": {
      "street": "123 Main St",
      "city": "New York",
      "state": "NY",
      "zipCode": "10001",
      "country": "USA"
    }
  },
  "status": "Draft",
  "orderDate": "2024-06-14T10:30:00Z",
  "expectedDeliveryDate": "2024-07-01T00:00:00Z",
  "totalAmount": 1000.50,
  "currency": "USD",
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440002",
      "productCode": "PROD-001",
      "productName": "Product 1",
      "quantity": 10,
      "unit": "PCS",
      "unitPrice": 100.05,
      "totalPrice": 1000.50,
      "status": "Pending"
    }
  ]
}
```

**Error Response:** `404 Not Found`

---

#### 3. List Purchase Orders
**Endpoint:** `GET /purchase-orders`
**Authentication:** Required
**Role:** Any authenticated user

**Query Parameters:**
- `supplierId` (GUID, optional): Filter by supplier
- `status` (string, optional): Filter by status (Draft, Submitted, Approved, etc.)
- `pageNumber` (int, default: 1): Page number for pagination
- `pageSize` (int, default: 10): Items per page

**Response:** `200 OK`
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440001",
    "poNumber": "PO-2024-001",
    "status": "Draft",
    "totalAmount": 1000.50,
    "currency": "USD"
  },
  {
    "id": "550e8400-e29b-41d4-a716-446655440003",
    "poNumber": "PO-2024-002",
    "status": "Submitted",
    "totalAmount": 2500.00,
    "currency": "USD"
  }
]
```

---

#### 4. Submit Purchase Order
**Endpoint:** `POST /purchase-orders/{id}/submit`
**Authentication:** Required
**Role:** Any authenticated user

**Path Parameters:**
- `id` (GUID): Purchase order ID

**Response:** `204 No Content`

**Errors:**
- `404 Not Found`: PO not found
- `400 Bad Request`: PO is not in Draft status

---

#### 5. Approve Purchase Order
**Endpoint:** `POST /purchase-orders/{id}/approve`
**Authentication:** Required
**Role:** Manager, Admin

**Path Parameters:**
- `id` (GUID): Purchase order ID

**Response:** `204 No Content`

**Errors:**
- `404 Not Found`: PO not found
- `400 Bad Request`: PO is not in Submitted status
- `403 Forbidden`: User lacks required role

---

#### 6. Cancel Purchase Order
**Endpoint:** `POST /purchase-orders/{id}/cancel`
**Authentication:** Required
**Role:** Manager, Admin

**Path Parameters:**
- `id` (GUID): Purchase order ID

**Response:** `204 No Content`

**Errors:**
- `404 Not Found`: PO not found
- `400 Bad Request`: PO cannot be cancelled (already received or cancelled)
- `403 Forbidden`: User lacks required role

---

#### 7. Add Item to Purchase Order
**Endpoint:** `POST /purchase-orders/{id}/items`
**Authentication:** Required
**Role:** Any authenticated user

**Path Parameters:**
- `id` (GUID): Purchase order ID

**Request Body:**
```json
{
  "productCode": "PROD-001",
  "productName": "Product 1",
  "quantity": 10,
  "unit": "PCS",
  "unitPrice": 100.50
}
```

**Response:** `200 OK` (Returns updated purchase order)

**Validation:**
- `productCode`: Required, max 50 characters
- `productName`: Required, max 100 characters
- `quantity`: Required, must be > 0
- `unit`: Required, max 20 characters
- `unitPrice`: Required, must be > 0

---

#### 8. Remove Item from Purchase Order
**Endpoint:** `DELETE /purchase-orders/{id}/items/{itemId}`
**Authentication:** Required
**Role:** Any authenticated user

**Path Parameters:**
- `id` (GUID): Purchase order ID
- `itemId` (GUID): Item ID to remove

**Response:** `200 OK` (Returns updated purchase order)

**Errors:**
- `404 Not Found`: PO or item not found

---

### Suppliers

#### 1. Create Supplier
**Endpoint:** `POST /suppliers`
**Authentication:** Required
**Role:** Admin, Manager

**Request Body:**
```json
{
  "code": "SUP-001",
  "name": "Supplier Company",
  "contactPerson": "John Doe",
  "email": "john@supplier.com",
  "phone": "123-456-7890",
  "address": {
    "street": "123 Main St",
    "city": "New York",
    "state": "NY",
    "zipCode": "10001",
    "country": "USA"
  }
}
```

**Response:** `201 Created`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "code": "SUP-001",
  "name": "Supplier Company",
  "contactPerson": "John Doe",
  "email": "john@supplier.com",
  "phone": "123-456-7890",
  "address": {
    "street": "123 Main St",
    "city": "New York",
    "state": "NY",
    "zipCode": "10001",
    "country": "USA"
  },
  "isActive": true
}
```

**Validation:**
- `code`: Required, max 20 characters
- `name`: Required, max 100 characters
- `email`: Must be valid email format (if provided)

---

#### 2. Get Supplier by ID
**Endpoint:** `GET /suppliers/{id}`
**Authentication:** Required
**Role:** Any authenticated user

**Path Parameters:**
- `id` (GUID): Supplier ID

**Response:** `200 OK`

**Errors:**
- `404 Not Found`: Supplier not found

---

#### 3. List Suppliers
**Endpoint:** `GET /suppliers`
**Authentication:** Required
**Role:** Any authenticated user

**Query Parameters:**
- `isActive` (bool, optional): Filter by active status
- `pageNumber` (int, default: 1): Page number
- `pageSize` (int, default: 10): Items per page

**Response:** `200 OK`
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "code": "SUP-001",
    "name": "Supplier Company",
    "isActive": true
  },
  {
    "id": "550e8400-e29b-41d4-a716-446655440001",
    "code": "SUP-002",
    "name": "Another Supplier",
    "isActive": true
  }
]
```

---

#### 4. Update Supplier
**Endpoint:** `PUT /suppliers/{id}`
**Authentication:** Required
**Role:** Admin, Manager

**Path Parameters:**
- `id` (GUID): Supplier ID

**Request Body:**
```json
{
  "name": "Updated Supplier Name",
  "contactPerson": "Jane Smith",
  "email": "jane@supplier.com",
  "phone": "987-654-3210",
  "address": {
    "street": "456 Oak Ave",
    "city": "Los Angeles",
    "state": "CA",
    "zipCode": "90001",
    "country": "USA"
  }
}
```

**Response:** `200 OK` (Returns updated supplier)

---

#### 5. Deactivate Supplier
**Endpoint:** `POST /suppliers/{id}/deactivate`
**Authentication:** Required
**Role:** Admin

**Path Parameters:**
- `id` (GUID): Supplier ID

**Response:** `204 No Content`

**Errors:**
- `404 Not Found`: Supplier not found
- `403 Forbidden`: User lacks required role

---

### Purchase Requests (added 2026-07-27, Module 8 reconciliation)

Suggestion-only requests auto-created by `WarehouseEventRabbitMqConsumer` when WareHouseService
publishes `StockAlertEvent` (a product at/below its reorder point). Not a real purchase-order
workflow — `convert` only records that the suggestion led to a real order; the actual
`PurchaseOrder` is still created via the endpoints above.

#### 1. List Purchase Requests
**Endpoint:** `GET /purchase-requests?status=Suggested`
**Permission:** `purchase_request:view`

```json
[
  {
    "id": "...", "productId": "...", "productName": "Widget A",
    "currentQuantity": 8, "reorderPoint": 20, "suggestedQuantity": 500,
    "status": "Suggested", "createdAt": "2026-07-27T10:00:00Z", "resolvedAt": null
  }
]
```

#### 2. Dismiss
**Endpoint:** `POST /purchase-requests/{id}/dismiss`
**Permission:** `purchase_request:manage`
Fails (400) if the request is not currently `Suggested`.

#### 3. Convert
**Endpoint:** `POST /purchase-requests/{id}/convert`
**Permission:** `purchase_request:manage`
Marks the request `Converted` — does **not** create a `PurchaseOrder` automatically. Fails
(400) if the request is not currently `Suggested`.

---

## Status Codes

| Code | Meaning |
|------|---------|
| 200 | OK - Request succeeded |
| 201 | Created - Resource successfully created |
| 204 | No Content - Request succeeded, no content to return |
| 400 | Bad Request - Invalid request data or business logic violation |
| 401 | Unauthorized - Missing or invalid authentication |
| 403 | Forbidden - Authenticated but lacks required permissions |
| 404 | Not Found - Resource not found |
| 500 | Internal Server Error - Server error occurred |

## Error Response Format

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PONumber": ["PO Number is required."]
  }
}
```

## Domain Models

### Purchase Order Status
- `Draft` (0): Initial state
- `Submitted` (1): Sent for approval
- `Approved` (2): Approved by manager
- `Ordered` (3): Placed with supplier
- `PartiallyReceived` (4): Partially received
- `Received` (5): Fully received
- `Cancelled` (6): Cancelled

### Purchase Order Item Status
- `Pending` (0): Awaiting receipt
- `PartiallyReceived` (1): Partially received
- `Received` (2): Fully received

## Examples

### Example 1: Create and Submit a Purchase Order

```bash
# 1. Create PO
curl -X POST http://localhost:5003/api/purchase-orders \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "poNumber": "PO-2024-001",
    "supplierId": "550e8400-e29b-41d4-a716-446655440000",
    "expectedDeliveryDate": "2024-07-01T00:00:00Z"
  }'

# 2. Add item
curl -X POST http://localhost:5003/api/purchase-orders/550e8400-e29b-41d4-a716-446655440001/items \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "productCode": "PROD-001",
    "productName": "Product 1",
    "quantity": 10,
    "unit": "PCS",
    "unitPrice": 100.50
  }'

# 3. Submit PO
curl -X POST http://localhost:5003/api/purchase-orders/550e8400-e29b-41d4-a716-446655440001/submit \
  -H "Authorization: Bearer <token>"

# 4. Approve PO (requires Manager role)
curl -X POST http://localhost:5003/api/purchase-orders/550e8400-e29b-41d4-a716-446655440001/approve \
  -H "Authorization: Bearer <manager-token>"
```

### Example 2: Manage Suppliers

```bash
# 1. Create supplier
curl -X POST http://localhost:5003/api/suppliers \
  -H "Authorization: Bearer <admin-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "SUP-001",
    "name": "New Supplier",
    "email": "contact@supplier.com"
  }'

# 2. Get supplier
curl -X GET http://localhost:5003/api/suppliers/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer <token>"

# 3. Update supplier
curl -X PUT http://localhost:5003/api/suppliers/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer <manager-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Updated Supplier Name",
    "contactPerson": "Updated Contact"
  }'
```

## Configuration

### appsettings.json
```json
{
  "JwtSettings": {
    "SecretKey": "your-secret-key-change-this-in-production"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## Testing

Run unit tests:
```bash
dotnet test tests/ERP.PO.Domain.Tests/
dotnet test tests/ERP.PO.Application.Tests/
dotnet test tests/ERP.PO.Api.Tests/
```

Test categories include:
- **Domain Tests**: Entity validation and business logic
- **Application Tests**: CQRS commands/queries and validators
- **API Tests**: Controller endpoints and authentication/authorization

## Best Practices

1. **Always provide Bearer token** in Authorization header
2. **Use appropriate roles** for operations (Admin for sensitive operations)
3. **Validate response status codes** before processing response body
4. **Handle pagination** for list endpoints
5. **Include audit trails** - user IDs are automatically captured
6. **Implement retry logic** for transient failures
7. **Use HTTP caching** appropriately for GET requests
