# PO Module API - Quick Start Guide

## Getting Started

### 1. Build the Project
```bash
cd src/services/PurchaseOrderService
dotnet build
```

### 2. Run Tests
```bash
dotnet test
```

### 3. Run the API
```bash
dotnet run --project src/ERP.PO.Api/ERP.PO.Api.csproj
```

API will be available at: `https://localhost:5003`

---

## Quick Examples

### Get JWT Token
First, obtain a JWT token from the Identity Service (Port 5001):

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "user@example.com",
    "password": "password123"
  }'
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

### Example 1: Create and Manage a Purchase Order

**Step 1: Create a Supplier (requires Admin/Manager)**
```bash
curl -X POST https://localhost:5003/api/suppliers \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "SUP-001",
    "name": "ABC Supplies Ltd",
    "contactPerson": "John Smith",
    "email": "contact@abcsupplies.com",
    "phone": "555-1234",
    "address": {
      "street": "123 Business Ave",
      "city": "New York",
      "state": "NY",
      "zipCode": "10001",
      "country": "USA"
    }
  }'
```

**Step 2: Create a Purchase Order**
```bash
SUPPLIER_ID="550e8400-e29b-41d4-a716-446655440000"  # From step 1 response

curl -X POST https://localhost:5003/api/purchase-orders \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "poNumber": "PO-2024-001",
    "supplierId": "'$SUPPLIER_ID'",
    "expectedDeliveryDate": "2024-07-15T00:00:00Z",
    "notes": "Rush order - needed by end of week"
  }'
```

Response includes PO ID - save it!

**Step 3: Add Items to the Purchase Order**
```bash
PO_ID="550e8400-e29b-41d4-a716-446655440001"  # From step 2 response

curl -X POST https://localhost:5003/api/purchase-orders/$PO_ID/items \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productCode": "PROD-001",
    "productName": "Office Supplies - Bulk Pack",
    "quantity": 50,
    "unit": "BOX",
    "unitPrice": 25.99
  }'
```

Add another item:
```bash
curl -X POST https://localhost:5003/api/purchase-orders/$PO_ID/items \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productCode": "PROD-002",
    "productName": "Printer Paper - 500 sheets",
    "quantity": 10,
    "unit": "PKG",
    "unitPrice": 5.50
  }'
```

**Step 4: View the Complete Purchase Order**
```bash
curl -X GET https://localhost:5003/api/purchase-orders/$PO_ID \
  -H "Authorization: Bearer YOUR_TOKEN"
```

Response shows full details including total amount (calculated from items).

**Step 5: Submit the Purchase Order**
```bash
curl -X POST https://localhost:5003/api/purchase-orders/$PO_ID/submit \
  -H "Authorization: Bearer YOUR_TOKEN"
```

Status changes: Draft → Submitted

**Step 6: Manager Approves the Order (requires Manager role)**
```bash
curl -X POST https://localhost:5003/api/purchase-orders/$PO_ID/approve \
  -H "Authorization: Bearer MANAGER_TOKEN"
```

Status changes: Submitted → Approved

---

### Example 2: List and Filter Purchase Orders

**List all POs:**
```bash
curl -X GET https://localhost:5003/api/purchase-orders \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Filter by status:**
```bash
curl -X GET "https://localhost:5003/api/purchase-orders?status=Approved" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Filter by supplier:**
```bash
curl -X GET "https://localhost:5003/api/purchase-orders?supplierId=$SUPPLIER_ID" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Pagination:**
```bash
curl -X GET "https://localhost:5003/api/purchase-orders?pageNumber=2&pageSize=20" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### Example 3: Manage Suppliers

**List all active suppliers:**
```bash
curl -X GET "https://localhost:5003/api/suppliers?isActive=true" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Get supplier details:**
```bash
curl -X GET https://localhost:5003/api/suppliers/$SUPPLIER_ID \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Update supplier info (requires Manager/Admin):**
```bash
curl -X PUT https://localhost:5003/api/suppliers/$SUPPLIER_ID \
  -H "Authorization: Bearer MANAGER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ABC Supplies International",
    "contactPerson": "Jane Doe",
    "email": "jane@abcsupplies.com",
    "phone": "555-5678"
  }'
```

**Deactivate supplier (requires Admin):**
```bash
curl -X POST https://localhost:5003/api/suppliers/$SUPPLIER_ID/deactivate \
  -H "Authorization: Bearer ADMIN_TOKEN"
```

---

## Postman Collection

Save as `PO-API.postman_collection.json`:

```json
{
  "info": {
    "name": "PO Service API",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Purchase Orders",
      "item": [
        {
          "name": "Create PO",
          "request": {
            "method": "POST",
            "url": "{{base_url}}/api/purchase-orders",
            "header": [
              {"key": "Authorization", "value": "Bearer {{token}}"},
              {"key": "Content-Type", "value": "application/json"}
            ],
            "body": {
              "mode": "raw",
              "raw": "{\"poNumber\":\"PO-2024-001\",\"supplierId\":\"{{supplier_id}}\",\"expectedDeliveryDate\":\"2024-07-15T00:00:00Z\"}"
            }
          }
        },
        {
          "name": "Get PO by ID",
          "request": {
            "method": "GET",
            "url": "{{base_url}}/api/purchase-orders/{{po_id}}",
            "header": [
              {"key": "Authorization", "value": "Bearer {{token}}"}
            ]
          }
        },
        {
          "name": "List POs",
          "request": {
            "method": "GET",
            "url": "{{base_url}}/api/purchase-orders",
            "header": [
              {"key": "Authorization", "value": "Bearer {{token}}"}
            ]
          }
        },
        {
          "name": "Submit PO",
          "request": {
            "method": "POST",
            "url": "{{base_url}}/api/purchase-orders/{{po_id}}/submit",
            "header": [
              {"key": "Authorization", "value": "Bearer {{token}}"}
            ]
          }
        },
        {
          "name": "Approve PO",
          "request": {
            "method": "POST",
            "url": "{{base_url}}/api/purchase-orders/{{po_id}}/approve",
            "header": [
              {"key": "Authorization", "value": "Bearer {{manager_token}}"}
            ]
          }
        }
      ]
    },
    {
      "name": "Suppliers",
      "item": [
        {
          "name": "Create Supplier",
          "request": {
            "method": "POST",
            "url": "{{base_url}}/api/suppliers",
            "header": [
              {"key": "Authorization", "value": "Bearer {{admin_token}}"},
              {"key": "Content-Type", "value": "application/json"}
            ],
            "body": {
              "mode": "raw",
              "raw": "{\"code\":\"SUP-001\",\"name\":\"Supplier Name\",\"email\":\"contact@supplier.com\"}"
            }
          }
        },
        {
          "name": "List Suppliers",
          "request": {
            "method": "GET",
            "url": "{{base_url}}/api/suppliers",
            "header": [
              {"key": "Authorization", "value": "Bearer {{token}}"}
            ]
          }
        }
      ]
    }
  ],
  "variable": [
    {"key": "base_url", "value": "https://localhost:5003"},
    {"key": "token", "value": ""},
    {"key": "admin_token", "value": ""},
    {"key": "manager_token", "value": ""},
    {"key": "supplier_id", "value": ""},
    {"key": "po_id", "value": ""}
  ]
}
```

Import into Postman and set the variables before running requests.

---

## Common HTTP Status Codes

| Code | Meaning | Example |
|------|---------|---------|
| 200 | Success - Get/Update operations | Getting PO details |
| 201 | Created - Resource created | Creating new PO |
| 204 | No Content - Success, nothing to return | Submit/Approve PO |
| 400 | Bad Request - Invalid input | Missing required fields |
| 401 | Unauthorized - Missing/invalid token | No Authorization header |
| 403 | Forbidden - Lacks permissions | User without Manager role approving |
| 404 | Not Found - Resource doesn't exist | PO ID not found |
| 500 | Server Error | Database connection failure |

---

## Troubleshooting

### "401 Unauthorized"
- ✅ Ensure you have a valid JWT token
- ✅ Verify token hasn't expired
- ✅ Check Authorization header format: `Bearer <token>`

### "403 Forbidden"
- ✅ User role not sufficient for operation
- ✅ Approval requires Manager/Admin role
- ✅ Supplier creation requires Admin/Manager role

### "400 Bad Request"
- ✅ Check all required fields are present
- ✅ Verify data types (GUIDs for IDs, decimals for prices)
- ✅ Validate format (email addresses, dates)

### "404 Not Found"
- ✅ Verify resource ID exists
- ✅ Check spelling of IDs
- ✅ Ensure you're using the correct resource type

---

## Environment Setup

### Development Configuration

**appsettings.Development.json:**
```json
{
  "JwtSettings": {
    "SecretKey": "your-development-secret-key"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  }
}
```

### Production Configuration

**appsettings.Production.json:**
```json
{
  "JwtSettings": {
    "SecretKey": "your-production-secret-key-use-strong-key"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning"
    }
  }
}
```

⚠️ **IMPORTANT**: Change JWT secret in production!

---

## Test with Unit Tests

### Run specific test class:
```bash
dotnet test --filter "ClassName=PurchaseOrdersControllerTests"
```

### Run tests matching pattern:
```bash
dotnet test --filter "Name~CreatePurchaseOrder"
```

### Run with verbose output:
```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## Next Steps

1. **Setup Identity Service**: Get JWT tokens
2. **Test with Postman**: Use the provided collection
3. **Implement Database**: Add EF Core DbContext
4. **Add Swagger**: Generate OpenAPI docs
5. **Setup CI/CD**: Add to deployment pipeline
6. **Monitor**: Add Application Insights logging

---

## Resources

- [API Documentation](./API_DOCUMENTATION.md)
- [Implementation Summary](./IMPLEMENTATION_SUMMARY.md)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [FluentValidation Docs](https://fluentvalidation.net/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8949)

---

## Support

For issues or questions:
1. Check logs: `dotnet run` and review console output
2. Review test files for examples
3. Check [API_DOCUMENTATION.md](./API_DOCUMENTATION.md) for endpoint details
4. Verify authentication token is valid and not expired
