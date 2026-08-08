# ============================================================
# Fix missing BaseEntity columns across all service databases.
# Safe to re-run: all ALTER TABLE statements are guarded.
# ============================================================
param(
    [string]$Server   = "localhost,1433",
    [string]$User     = "sa",
    [string]$Password = "!Edw@rd!123456!"
)

function Invoke-Sql($db, $sql) {
    sqlcmd -S $Server -U $User -P $Password -d $db -Q $sql -I 2>&1 | Where-Object { $_ -match '\S' -and $_ -notmatch 'rows affected' }
}

function Add-IfMissing($db, $table, $column, $ddl) {
    $check = "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('$table') AND name='$column') ALTER TABLE [$table] ADD $ddl"
    Invoke-Sql $db $check | ForEach-Object { Write-Host "  $_" }
}

# Tables that are NOT entity tables (skip them)
$skip = @('OutboxMessages','SagaStates','LeaveBalances')

# Base columns to add to every entity table
function Fix-Table($db, $table) {
    Add-IfMissing $db $table "CreatedAt"   "CreatedAt  DATETIME2  NOT NULL DEFAULT GETUTCDATE()"
    Add-IfMissing $db $table "UpdatedAt"   "UpdatedAt  DATETIME2  NOT NULL DEFAULT GETUTCDATE()"
    Add-IfMissing $db $table "CreatedBy"   "CreatedBy  NVARCHAR(256) NULL"
    Add-IfMissing $db $table "UpdatedBy"   "UpdatedBy  NVARCHAR(256) NULL"
    Add-IfMissing $db $table "IsDeleted"   "IsDeleted  BIT NOT NULL DEFAULT 0"
    Add-IfMissing $db $table "Description" "Description NVARCHAR(MAX) NULL"
    Add-IfMissing $db $table "RowVersion"  "RowVersion ROWVERSION"
}

$serviceDBs = @{
    "SalesDB"        = @("SalesOrders","SalesOrderItems","OrderNotes","OrderStatusHistory","SalesQuotations")
    "AccountingDB"   = @("Accounts","Bills","BillLines","Invoices","InvoiceLines","JournalEntries","JournalEntryLines","FiscalPeriods","BankAccounts","TaxRates")
    "PurchasingDB"   = @("PurchaseOrders","PurchaseOrderItems","Suppliers","SupplierRatings","GoodsReceipts")
    "WarehouseDB"    = @("Inventories","InventoryLots","StockMovements","Reservations","WarehouseLocations")
    "PaymentDB"      = @("Payments","PaymentRefunds","Settlements")
    "ShippingDB"     = @("Shipments","ShipmentItems","ShipmentTrackingEvents","Carriers")
    "PosDB"          = @("Transactions","TransactionItems","TransactionTenders","CashShifts","CashDenominationLines","Terminals")
    "PromotionDB"    = @("Promotions","Coupons","LoyaltyAccounts","LoyaltyTransactions","FlashSales","FlashSaleItems")
    "NotificationDB" = @("Notifications","NotificationTemplates","NotificationLogs","NotificationPreferences")
}

foreach ($db in $serviceDBs.Keys) {
    Write-Host "`n=== $db ===" -ForegroundColor Cyan
    foreach ($table in $serviceDBs[$db]) {
        # Only fix if the table exists
        $exists = Invoke-Sql $db "SELECT 1 FROM sys.tables WHERE name='$table'"
        if ($exists -match '1') {
            Write-Host "  $table" -ForegroundColor Yellow
            Fix-Table $db $table
        } else {
            Write-Host "  $table -- MISSING (skipped)" -ForegroundColor DarkGray
        }
    }
}

Write-Host "`nDone." -ForegroundColor Green
