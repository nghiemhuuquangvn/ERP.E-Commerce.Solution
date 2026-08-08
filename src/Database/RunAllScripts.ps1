# ============================================================
# Run All Database Scripts
# ============================================================
# This script creates all databases for the ERP E-Commerce Solution
#
# Prerequisites:
# - SQL Server must be running and accessible
# - Run as Administrator if using Windows Authentication
#
# Configuration:
$ServerName = "localhost"           # Change if using different SQL Server
$UseWindowsAuth = $true             # Set to $false to use SQL Server authentication
$SqlUsername = "sa"                 # Only used if UseWindowsAuth = $false
$SqlPassword = ""                   # Only used if UseWindowsAuth = $false (prompt if empty)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ============================================================
# Database creation order (respects dependencies)
# ============================================================
$DatabaseScripts = @(
    # Core Identity first
    "Identity\001_CreateAuthDB.sql",
    "Identity\002_AddExternalAuthSettingsAndTwoFactor.sql",
    "Identity\003_AddApiKeysAndSecurityPermissions.sql",
    "Identity\004_AddTenantStatusColumn.sql",
    "Identity\005_AddBaseEntityAuditColumns.sql",
    "Identity\006_AddCustomerAndStaffRoles.sql",
    "Identity\007_SeedIdentityManagementPermissions.sql",
    "Identity\008_AddGeneralAndCompanySettings.sql",
    "Identity\008_AddPermissionsAuditColumns.sql",
    "Identity\009_ExtendAuthSettingsSecurityFields.sql",
    "Identity\010_AddPhase3FixedSettingsCategories.sql",
    "Identity\011_AddFeatureFlags.sql",
    "Identity\012_AddLicenseManagement.sql",
    "Identity\013_SeedRoleDefinitionsIntoRolePermissions.sql",
    "Identity\014_ChangeEmailUniqueIndexToPerTenant.sql",
    "Identity\016_SeedFinanceApprovePermission.sql",

    # Cross-cutting master data (Department/Position/Employee/UoM/Geography/Language —
    # seed before HRM, which now references Employees/Departments here instead of owning them)
    "MasterData\001_CreateMasterDataDB.sql",
    "MasterData\002_ProvisionMissingTablesAndColumns.sql",
    "MasterData\003_AddCurrencySettings.sql",
    "MasterData\004_AddOrganizationEntities.sql",
    "MasterData\005_AddCurrencyAndPaymentTerm.sql",

    # Core business services
    "Sales\001_CreateSalesDB.sql",
    "Sales\002_AddOrderLifecycleExtensions.sql",
    "Sales\003_AddBaseEntityColumns.sql",
    "Sales\004_AddOrderFulfillmentSagaState.sql",
    "Sales\005_AddCartCheckoutAndOrderSettings.sql",
    "Sales\006_AddOrderCoreFields.sql",
    "Sales\007_AddSalesQuotations.sql",
    "Sales\008_AddCustomerSalesProfile.sql",
    "Sales\009_AddCommission.sql",
    "Purchasing\001_CreatePurchasingDB.sql",
    "Purchasing\002_AddOutboxMessages.sql",
    "Purchasing\003_AddReceivingScheduleAndSupplierRatings.sql",
    "Purchasing\004_AddRejectionColumns.sql",
    "Purchasing\005_AddSupplierItemsAndPaymentTerm.sql",
    "Purchasing\006_AddPurchaseRequests.sql",
    "Purchasing\008_AddMultiLevelApproval.sql",
    "Warehouse\001_CreateWarehouseDB.sql",
    "Warehouse\002_AddLotsSerialsTransfersStocktakes.sql",
    "Warehouse\003_AddInventorySettings.sql",
    "Warehouse\004_AddBinLevelStockApprovalWarehousesBarcodes.sql",
    "Warehouse\005_AddModule8InventoryReconciliation.sql",
    "Accounting\001_CreateAccountingDB.sql",
    "Accounting\002_AddTaxSettings.sql",
    "Accounting\003_AddTaxRatesBankAccountsAndStatements.sql",
    "Accounting\004_FixMissingEntityColumns.sql",
    "Product\001_CreateProductDB.sql",
    "Product\002_AddProductTranslations.sql",
    "Product\003_AddProductAttributesAndDimensions.sql",
    "Product\004_AddProductExternalMappings.sql",
    "Product\005_AddSearchIndexQueue.sql",
    "Product\006_AddSeoStockBundlesRelationsReviews.sql",
    "Product\007_RebuildProductsAndMissingTables.sql",
    "Product\008_FixMissingEntityColumns.sql",
    "Product\009_AddStoreProductSettings.sql",
    "Product\010_AddBrandsUomConversionsAndPricingUpdates.sql",
    "Product\012_FixProductAttributeTablesSchema.sql",
    "Product\013_FixImagesAndAttributeValueOwnerTypeSchema.sql",
    "Product\014_FixMissingProductsRowVersion.sql",

    # Customer-facing services
    "CRM\001_CreateCrmDB.sql",
    "CRM\002_FixCrmEntityColumns.sql",
    "CRM\003_AddContactTags.sql",
    "Payment\001_CreatePaymentDB.sql",
    "Payment\002_AddInvoiceLinkAndSavedMethodsAndInstallments.sql",
    "Payment\003_AddBaseEntityColumns.sql",
    "Payment\004_AddPaymentSettings.sql",
    "Shipping\001_CreateShippingDB.sql",
    "Shipping\002_AddReturnsPodAddressRestructure.sql",
    "Shipping\003_AddShippingSettings.sql",

    # HR & Retail
    "HRM\001_CreateHrmDB.sql",
    "HRM\002_RemoveEmployeeDepartmentOwnership.sql",
    "HRM\003_FixHrmEntityColumns.sql",
    "HRM\003b_FixAttendanceColumnRenames.sql",
    "POS\001_CreatePosDB.sql",
    "POS\002_AddTendersDenominationsAndTypeFixes.sql",
    "POS\003_AddProductAndPromotionCache.sql",

    # Cross-cutting
    "Promotion\001_CreatePromotionDB.sql",
    "PromotionService\002_FixPromotionEntityColumns.sql",
    "Notification\001_CreateNotificationDB.sql",
    "Notification\002_AddNotificationEmailSmsSettings.sql",
    "Notification\003_AddWebhookEndpoints.sql",
    "Notification\005_SeedWorkflowTemplates.sql",
    "Notification\006_FixNotificationEntityColumns.sql",
    "Reporting\001_CreateReportingDB.sql",
    "Reporting\002_AddMissingEntityColumns.sql",
    "Reporting\004_FixFactPOSTransactionsCashierIdType.sql",
    "Reporting\005_AddDimProductStockQuantity.sql",

    # ============================================================
    # Demo seed data — must run AFTER all schema above, and in this specific
    # order per src/Database/SEED_DATA_CONVENTIONS.md's cross-service GUID
    # registry (each step below references GUIDs seeded by an earlier step).
    # Reporting's fact-seed is last since it reads from every other service.
    # ============================================================
    "Identity\015_SeedDemoUsersAndRoles.sql",
    "MasterData\006_SeedDemoData.sql",
    "Product\011_SeedDemoData.sql",
    "CRM\004_SeedDemoData.sql",
    "Sales\010_SeedDemoData.sql",
    "Warehouse\006_SeedDemoData.sql",
    "Purchasing\007_SeedDemoData.sql",
    "Payment\005_SeedDemoData.sql",
    "Shipping\004_SeedDemoData.sql",
    "Promotion\002_SeedDemoData.sql",
    "POS\004_SeedDemoData.sql",
    "Accounting\005_SeedDemoData.sql",
    "HRM\004_SeedDemoData.sql",
    "Notification\004_SeedDemoData.sql",
    "Reporting\003_SeedFactsFromSeedData.sql"
)

# ============================================================
# Helper function to run SQL script
# ============================================================
function Execute-SqlScript {
    param(
        [string]$ScriptPath,
        [string]$ServerName,
        [bool]$UseWindowsAuth,
        [string]$SqlUsername,
        [string]$SqlPassword
    )

    $fullPath = Join-Path $ScriptDir $ScriptPath

    if (-not (Test-Path $fullPath)) {
        Write-Host "❌ Script not found: $ScriptPath" -ForegroundColor Red
        return $false
    }

    Write-Host ""
    Write-Host "📋 Running: $ScriptPath" -ForegroundColor Cyan
    Write-Host "   Path: $fullPath" -ForegroundColor Gray

    try {
        $scriptContent = Get-Content $fullPath -Raw

        if ($UseWindowsAuth) {
            # Windows Authentication
            Invoke-Sqlcmd -ServerInstance $ServerName `
                          -InputFile $fullPath `
                          -ErrorAction Stop `
                          -Verbose
        } else {
            # SQL Server Authentication
            if ([string]::IsNullOrEmpty($SqlPassword)) {
                Write-Host "Enter SQL Server password for user '$SqlUsername':" -ForegroundColor Yellow
                $SecurePassword = Read-Host -AsSecureString
                $SqlPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToCoTaskMemUnicode($SecurePassword))
            }

            Invoke-Sqlcmd -ServerInstance $ServerName `
                          -Username $SqlUsername `
                          -Password $SqlPassword `
                          -InputFile $fullPath `
                          -ErrorAction Stop `
                          -Verbose
        }

        Write-Host "✅ SUCCESS: $ScriptPath" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "❌ FAILED: $ScriptPath" -ForegroundColor Red
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
Write-Host "Server: $ServerName" -ForegroundColor Yellow
Write-Host "Auth: $(if ($UseWindowsAuth) { 'Windows Authentication' } else { "SQL Server ($SqlUsername)" })" -ForegroundColor Yellow
Write-Host "Scripts Location: $ScriptDir" -ForegroundColor Yellow
Write-Host ""

$successCount = 0
$failureCount = 0
$failedScripts = @()

foreach ($scriptPath in $DatabaseScripts) {
    $success = Execute-SqlScript -ScriptPath $scriptPath `
                                  -ServerName $ServerName `
                                  -UseWindowsAuth $UseWindowsAuth `
                                  -SqlUsername $SqlUsername `
                                  -SqlPassword $SqlPassword

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
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                  Execution Summary                     ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Successful: $successCount" -ForegroundColor Green
Write-Host "❌ Failed: $failureCount" -ForegroundColor $(if ($failureCount -eq 0) { 'Green' } else { 'Red' })

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
    Write-Host "✅ All databases created successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Some scripts failed. Please review errors above." -ForegroundColor Red
    exit 1
}
