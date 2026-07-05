# ============================================================
# Run All Database Scripts using sqlcmd
# ============================================================
$ServerName = "localhost"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# All database scripts in execution order
$DatabaseScripts = @(
    # Identity first - with SuperAdmin seeding
    "Identity\001_CreateAuthDB.sql",
    "Identity\002_AddExternalAuthSettingsAndTwoFactor.sql",
    "Identity\003_AddApiKeysAndSecurityPermissions.sql",

    # MasterData (employees, departments, reference data)
    "MasterData\001_CreateMasterDataDB.sql",

    # Product catalog
    "Product\001_CreateProductDB.sql",
    "Product\002_AddProductTranslations.sql",
    "Product\003_AddProductAttributesAndDimensions.sql",
    "Product\004_AddProductExternalMappings.sql",
    "Product\005_AddSearchIndexQueue.sql",
    "Product\006_AddSeoStockBundlesRelationsReviews.sql",

    # Core business services
    "Sales\001_CreateSalesDB.sql",
    "Sales\002_AddOrderLifecycleExtensions.sql",

    "Purchasing\001_CreatePurchasingDB.sql",
    "Purchasing\002_AddOutboxMessages.sql",
    "Purchasing\003_AddReceivingScheduleAndSupplierRatings.sql",

    "Warehouse\001_CreateWarehouseDB.sql",
    "Warehouse\002_AddLotsSerialsTransfersStocktakes.sql",

    "Accounting\001_CreateAccountingDB.sql",

    # Customer-facing services
    "CRM\001_CreateCrmDB.sql",

    "Payment\001_CreatePaymentDB.sql",
    "Payment\002_AddInvoiceLinkAndSavedMethodsAndInstallments.sql",

    "Shipping\001_CreateShippingDB.sql",
    "Shipping\002_AddReturnsPodAddressRestructure.sql",

    # HR & Retail
    "HRM\001_CreateHrmDB.sql",
    "HRM\002_RemoveEmployeeDepartmentOwnership.sql",

    "POS\001_CreatePosDB.sql",
    "POS\002_AddTendersDenominationsAndTypeFixes.sql",

    # Cross-cutting
    "Promotion\001_CreatePromotionDB.sql",
    "Notification\001_CreateNotificationDB.sql",
    "Reporting\001_CreateReportingDB.sql"
)

# ============================================================
# Execute using sqlcmd
# ============================================================
function Execute-SqlScript {
    param(
        [string]$ScriptPath,
        [string]$ServerName
    )

    $fullPath = Join-Path $ScriptDir $ScriptPath

    if (-not (Test-Path $fullPath)) {
        Write-Host "❌ Script not found: $ScriptPath" -ForegroundColor Red
        return $false
    }

    Write-Host ""
    Write-Host "📋 Running: $ScriptPath" -ForegroundColor Cyan

    try {
        # Use sqlcmd with -i for input file and -S for server
        & sqlcmd -S $ServerName -i $fullPath -b

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ SUCCESS: $ScriptPath" -ForegroundColor Green
            return $true
        } else {
            Write-Host "❌ FAILED: $ScriptPath (Exit code: $LASTEXITCODE)" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "❌ ERROR: $ScriptPath" -ForegroundColor Red
        Write-Host "Error: $_" -ForegroundColor Red
        return $false
    }
}

# ============================================================
# Main execution
# ============================================================
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     ERP E-Commerce Solution — Database Creation       ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server: $ServerName (Windows Authentication)" -ForegroundColor Yellow
Write-Host "Scripts Location: $ScriptDir" -ForegroundColor Yellow
Write-Host "Total Scripts: $($DatabaseScripts.Count)" -ForegroundColor Yellow
Write-Host ""

$successCount = 0
$failureCount = 0
$failedScripts = @()
$startTime = Get-Date

foreach ($scriptPath in $DatabaseScripts) {
    $success = Execute-SqlScript -ScriptPath $scriptPath -ServerName $ServerName

    if ($success) {
        $successCount++
    } else {
        $failureCount++
        $failedScripts += $scriptPath
    }
}

# ============================================================
# Summary
# ============================================================
$duration = (Get-Date) - $startTime
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                  Execution Summary                     ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Successful: $successCount / $($DatabaseScripts.Count)" -ForegroundColor $(if ($failureCount -eq 0) { 'Green' } else { 'Yellow' })
Write-Host "❌ Failed: $failureCount / $($DatabaseScripts.Count)" -ForegroundColor $(if ($failureCount -eq 0) { 'Green' } else { 'Red' })
Write-Host "⏱️  Duration: $($duration.TotalMinutes.ToString('F1')) minutes" -ForegroundColor Cyan

if ($failureCount -gt 0) {
    Write-Host ""
    Write-Host "Failed scripts:" -ForegroundColor Red
    $failedScripts | ForEach-Object { Write-Host "  ❌ $_" -ForegroundColor Red }
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║              SuperAdmin Credentials                    ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "📧 Email:    superadmin@system.local" -ForegroundColor Yellow
Write-Host "👤 Username: superadmin" -ForegroundColor Yellow
Write-Host "🔐 Password: SuperAdmin@123" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  IMPORTANT: Change this password in production!" -ForegroundColor Red
Write-Host ""

if ($failureCount -eq 0) {
    Write-Host "✅ All $successCount databases created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Verify databases in SQL Server Management Studio" -ForegroundColor Cyan
    Write-Host "2. Update connection strings in appsettings.json" -ForegroundColor Cyan
    Write-Host "3. Run backend services" -ForegroundColor Cyan
    Write-Host "4. Run frontend applications" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "⚠️  Some scripts failed ($failureCount/$($DatabaseScripts.Count)). Review errors above." -ForegroundColor Yellow
    exit 1
}
