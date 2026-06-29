# Database Setup — Quick Start Guide

## What Was Configured

✅ **SuperAdmin User Created with Full Permissions**
- Email: `superadmin@system.local`
- Username: `superadmin`
- Password: `SuperAdmin@123`
- Role: SuperAdmin (system admin role)
- Permissions: All 4 core permissions granted

✅ **13 Database Scripts Ready**
- Identity (AuthDB) — with SuperAdmin seeded
- Sales, Purchasing, Warehouse, Accounting
- CRM, Payment, Shipping
- HRM, POS
- Promotion, Notification, Reporting

✅ **Automated Setup Script** (`RunAllScripts.ps1`)
- Creates all 13 databases in correct order
- Handles SQL Server authentication automatically
- Provides detailed progress output
- Summary with success/failure count

✅ **Comprehensive Documentation**
- `README.md` — Full architecture and setup guide
- Troubleshooting guide
- Backup and maintenance instructions
- Database verification queries

---

## 🚀 How to Run — 3 Steps

### Step 1: Open PowerShell as Administrator

```powershell
# Search for "PowerShell" in Start menu
# Right-click → "Run as Administrator"
```

### Step 2: Navigate to Database Scripts

```powershell
cd "D:\Documents\ERP\Sources\ERP.E-Commerce.Solution\src\Database"
```

### Step 3: Execute Setup Script

```powershell
.\RunAllScripts.ps1
```

### What to Expect

- PowerShell will display progress for each database
- ✅ Green check = success
- ❌ Red X = failure (review error message)
- Final summary showing:
  - Total successful: 13
  - Total failed: 0
  - SuperAdmin credentials

**Estimated time**: 2-5 minutes depending on SQL Server performance

---

## 🔑 Default Credentials

After setup, log in with:

| Field | Value |
|-------|-------|
| Email | `superadmin@system.local` |
| Password | `SuperAdmin@123` |

### ⚠️ IMPORTANT SECURITY NOTES

1. **This is for development only**
2. **Change the password before production deployment**
3. **Enable MFA for SuperAdmin account**
4. **Audit logs are in `SecurityAuditLogs` table**

---

## ✅ Verify Setup Was Successful

### Check All Databases Created

```sql
-- Run in SQL Server Management Studio (SSMS)
SELECT name, create_date 
FROM sys.databases 
WHERE name IN (
    'AuthDB', 'SalesDB', 'PurchasingDB', 'WarehouseDB',
    'AccountingDB', 'CrmDB', 'HrmDB', 'PaymentDB',
    'ShippingDB', 'PosDB', 'PromotionDB', 'NotificationDB', 'ReportingDB'
)
ORDER BY name;
```

**Expected Result**: All 13 databases listed

### Check SuperAdmin User Exists

```sql
-- Run this in SSMS
USE AuthDB;
SELECT Email, UserName, FullName, UserTypeCode
FROM Users
WHERE Email = 'superadmin@system.local';
```

**Expected Result**: One row with SuperAdmin data

### Check Permissions Assigned

```sql
-- Run this in SSMS
USE AuthDB;
SELECT 
    u.Email,
    r.RoleName,
    p.PermissionCode
FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.Id
JOIN RolePermissions rp ON r.Id = rp.RoleId
JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'superadmin@system.local';
```

**Expected Result**: 4 rows showing all permissions granted

---

## 📝 Database Structure Overview

Each database includes:

- **Core tables** — Business entities
- **Audit fields** — CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted
- **Multi-tenancy** — TenantId on all relevant tables
- **Indexes** — Performance optimization
- **Outbox tables** — Event publishing (Outbox pattern)
- **Soft delete** — IsDeleted flag for audit trail

---

## 🆘 Troubleshooting

### Issue: "RunAllScripts.ps1 cannot be loaded because running scripts is disabled"

**Solution**: Run this once in PowerShell (Administrator):
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
```

### Issue: "Invoke-Sqlcmd : The term 'Invoke-Sqlcmd' is not recognized"

**Solution**: Install SQL Server Management Tools or use SSMS to run scripts manually

### Issue: "Login failed for user 'sa'"

**Solution**: 
- Edit `RunAllScripts.ps1` line 15
- Change `$UseWindowsAuth = $false` to `$UseWindowsAuth = $true` (for Windows auth)
- Or verify SQL Server credentials

### Issue: "Connection timeout"

**Solution**:
- Verify SQL Server is running
- Check firewall allows port 1433
- Test connection in SSMS first

---

## 📂 Database File Organization

```
src/Database/
├── Accounting/
│   └── 001_CreateAccountingDB.sql
├── CRM/
│   └── 001_CreateCrmDB.sql
├── HRM/
│   └── 001_CreateHrmDB.sql
├── Identity/
│   └── 001_CreateAuthDB.sql (⭐ includes SuperAdmin)
├── Notification/
│   └── 001_CreateNotificationDB.sql
├── POS/
│   └── 001_CreatePosDB.sql
├── Payment/
│   └── 001_CreatePaymentDB.sql
├── Promotion/
│   └── 001_CreatePromotionDB.sql
├── Purchasing/
│   └── 001_CreatePurchasingDB.sql
├── Reporting/
│   └── 001_CreateReportingDB.sql
├── Sales/
│   └── 001_CreateSalesDB.sql
├── Shipping/
│   └── 001_CreateShippingDB.sql
├── Warehouse/
│   └── 001_CreateWarehouseDB.sql
├── RunAllScripts.ps1 (⭐ Setup automation)
├── README.md (⭐ Full documentation)
└── SETUP_GUIDE.md (this file)
```

---

## 🔄 Running Migrations Later

If you need to run the scripts again:

1. **Safe to re-run** — All scripts use `IF NOT EXISTS` checks
2. **Preserves existing data** — Won't overwrite existing databases
3. **Idempotent** — Can run multiple times without issues

To reset everything and start fresh:
```sql
-- Drop all databases (careful!)
DROP DATABASE AuthDB;
DROP DATABASE SalesDB;
-- ... etc
```

Then re-run `RunAllScripts.ps1`

---

## 📞 Next Steps

1. ✅ Run the setup script
2. ✅ Verify databases were created
3. ✅ Connect services to their respective databases
4. ✅ Update service connection strings in `appsettings.json`
5. 🔐 Change SuperAdmin password for production
6. 📚 Review `README.md` for architecture details

---

**Status**: ✅ Ready for Development  
**Last Updated**: June 30, 2026
