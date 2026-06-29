# ERP E-Commerce Solution — Database Setup

This directory contains SQL scripts to initialize all databases for the ERP E-Commerce microservices platform.

## Database Overview

The solution consists of 13 independently owned databases (one per service):

### Core Services
- **AuthDB** (Identity Service, Port 5001) — Authentication, authorization, user/role management
- **SalesDB** (Sales Order Service, Port 5002) — Sales orders, customers, order processing
- **PurchasingDB** (Purchase Order Service, Port 5003) — Purchase orders, supplier management
- **WarehouseDB** (Warehouse Service, Port 5004) — Inventory, stock movements, reservations
- **AccountingDB** (Accounting Service, Port 5005) — General ledger, AR/AP, fiscal periods
- **CrmDB** (CRM Service, Port 5006) — Customer relationships, leads, opportunities, cases
- **HrmDB** (HRM Service, Port 5007) — Employees, departments, attendance, payroll
- **PaymentDB** (Payment Service, Port 5009) — Transactions, payment methods, settlements
- **ShippingDB** (Shipping Service, Port 5010) — Shipments, tracking, carriers
- **PosDB** (POS Service, Port 5011) — Retail transactions, cash shifts, registers
- **PromotionDB** (Promotion Service, Port 5012) — Promotions, coupons, loyalty programs
- **NotificationDB** (Notification Service, Port 5013) — Notification logs, preferences
- **ReportingDB** (Reporting Service, Port 5014) — Data warehouse, BI tables

### Planned Services
- **ProductDB** (Product Service, Port 5008) — Product catalog, categories, SKUs, pricing *(TODO)*

## Architecture Principles

1. **Database Ownership**: Each service owns its database exclusively
2. **No Cross-Database Access**: Services communicate via REST APIs, RabbitMQ, or Kafka
3. **Event-Driven**: Integration events enable asynchronous service communication
4. **Outbox Pattern**: Each service uses outbox tables for reliable event publishing
5. **Multi-Tenant Ready**: All services support multi-tenancy via TenantId

## Quick Start

### Prerequisites

1. **SQL Server 2019+** or **SQL Server 2022**
   - Ensure SQL Server is running and accessible
   - Windows Authentication or SQL Server login credentials ready

2. **PowerShell 5.0+** (for automated setup)
   - `Invoke-Sqlcmd` cmdlet available
   - Run as Administrator if using Windows Authentication

### Method 1: Automated Setup (Recommended)

```powershell
# Open PowerShell as Administrator
cd src\Database

# Run the setup script
.\RunAllScripts.ps1

# Follow prompts for SQL Server connection details
```

#### Configuration Options

Edit `RunAllScripts.ps1` to customize:

```powershell
$ServerName = "localhost"           # SQL Server instance
$UseWindowsAuth = $true             # Windows or SQL auth
$SqlUsername = "sa"                 # SQL login name
$SqlPassword = ""                   # SQL password (prompted if empty)
```

### Method 2: Manual Setup

```powershell
# Via SQL Server Management Studio:
# 1. Connect to your SQL Server instance
# 2. Open each script and execute in order (starting with Identity)

# Via PowerShell:
$serverName = "localhost"

# Run each script in order
Invoke-Sqlcmd -ServerInstance $serverName `
              -InputFile "Identity\001_CreateAuthDB.sql"

Invoke-Sqlcmd -ServerInstance $serverName `
              -InputFile "Sales\001_CreateSalesDB.sql"

# ... continue for all scripts
```

## SuperAdmin Account

After setup, the following default admin account is created in **AuthDB**:

| Property | Value |
|----------|-------|
| **Email** | `superadmin@system.local` |
| **Username** | `superadmin` |
| **Password** | `SuperAdmin@123` |
| **Role** | SuperAdmin (full system access) |
| **Permissions** | All system permissions granted |

### ⚠️ Security Recommendations

1. **Change the default password immediately** in production
2. **Enable Multi-Factor Authentication (MFA)** for SuperAdmin
3. **Use strong passwords** following your security policy
4. **Audit login attempts** via SecurityAuditLogs table
5. **Rotate access credentials** regularly

## Database Schema Details

Each database script includes:

- **Core tables** — Domain entities and value objects
- **Audit fields** — CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
- **Indexes** — Performance optimization on frequently queried columns
- **Foreign keys** — Referential integrity within service boundaries
- **Outbox tables** — Reliable event publishing via Outbox pattern
- **Seed data** — System roles, permissions, and default admin account

### Multi-Tenancy Support

All tables include:
- `TenantId` — Organizational unit for data isolation
- `IsDeleted` — Soft delete flag for audit trail retention
- Indexes on `(TenantId, ...)` for query performance

### Audit Trail

Security-critical tables include:
- `SecurityAuditLogs` — Authentication events, role changes, permission grants
- `LoginAttempts` — Login success/failure tracking for rate limiting
- `PasswordHistories` — Password change tracking (prevent reuse)
- `RefreshTokens` — Token lifecycle tracking with family rotation

## Execution Order

The scripts are designed to run in this order to respect any cross-service dependencies:

1. **Identity** — Authentication & authorization foundation
2. **Sales**, **Purchasing**, **Warehouse**, **Accounting** — Core business services
3. **CRM**, **Payment**, **Shipping** — Customer-facing operations
4. **HRM**, **POS** — Employees & retail
5. **Promotion**, **Notification**, **Reporting** — Cross-cutting services

## Troubleshooting

### Issue: "The INVOKE-SQLCMD cmdlet not found"

**Solution**: Install SQL Server Management Tools
```powershell
# Via chocolatey
choco install sqlserver-management-studio

# Or download from Microsoft
# https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms
```

### Issue: "Login failed for user 'sa'"

**Solution**: 
- Verify SQL Server is running: `Get-Service MSSQLSERVER`
- Check credentials in `RunAllScripts.ps1`
- Ensure SQL Server authentication is enabled (not Windows-only)

### Issue: "Database 'AuthDB' already exists"

**Solution**: 
- Script includes `IF NOT EXISTS` checks — safe to re-run
- Or drop existing databases first if needed:
```sql
DROP DATABASE AuthDB;
DROP DATABASE SalesDB;
-- ... etc
```

### Issue: "Connection timeout"

**Solution**:
- Verify SQL Server is running and accessible
- Check firewall rules allow SQL Server port (default 1433)
- Test connectivity:
```powershell
$server = New-Object "Microsoft.SqlServer.Management.Smo.Server" $serverName
$server.ConnectionContext.Connect()
```

## Verifying Setup

After running scripts, verify all databases were created:

```sql
SELECT name, create_date FROM sys.databases 
WHERE name IN (
    'AuthDB', 'SalesDB', 'PurchasingDB', 'WarehouseDB',
    'AccountingDB', 'CrmDB', 'HrmDB', 'PaymentDB',
    'ShippingDB', 'PosDB', 'PromotionDB', 'NotificationDB', 'ReportingDB'
)
ORDER BY name;
```

Expected result: 13 databases listed.

### Verify SuperAdmin User

```sql
USE AuthDB;
SELECT Email, UserName, FullName, UserTypeCode, IsActive, IsEmailConfirmed
FROM Users
WHERE Email = 'superadmin@system.local';
```

### Verify Permissions

```sql
USE AuthDB;
SELECT 
    u.Email,
    r.RoleName,
    p.PermissionCode,
    p.PermissionName
FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.Id
JOIN RolePermissions rp ON r.Id = rp.RoleId
JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'superadmin@system.local'
ORDER BY r.RoleName, p.PermissionCode;
```

## Database Maintenance

### Backup Strategy

```sql
-- Full backup
BACKUP DATABASE AuthDB 
TO DISK = 'C:\Backups\AuthDB_full.bak'
WITH INIT, NAME = 'AuthDB Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10;

-- Transaction log backup
BACKUP LOG AuthDB 
TO DISK = 'C:\Backups\AuthDB_log.bak'
WITH INIT, NAME = 'AuthDB Log Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10;
```

### Index Maintenance

```sql
-- Rebuild fragmented indexes
ALTER INDEX ALL ON Users REBUILD;

-- Reorganize slightly fragmented indexes
ALTER INDEX ALL ON SalesOrders REORGANIZE;

-- Update statistics
UPDATE STATISTICS Users;
```

## Integration with Microservices

Each service's `appsettings.json` should reference its database:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AuthDB;Integrated Security=true;"
  }
}
```

## Additional Resources

- **CLAUDE.md** — Architecture overview and coding standards
- **docs/architecture/event-catalog.md** — Integration events reference
- **docs/architecture/database-map.md** — ER diagrams and schema details
- **Service README.md** — Per-service database documentation

## Support

For issues or questions:
1. Check troubleshooting section above
2. Review SQL Server error logs: `%PROGRAMFILES%\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Log\`
3. Verify prerequisites are installed and configured
4. Review service-specific README files

---

**Last Updated**: June 2026  
**Database Version**: 1.0  
**Status**: ✅ Production Ready
