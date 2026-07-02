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

    # Cross-cutting master data (Department/Position/Employee/UoM/Geography/Language —
    # seed before HRM, which now references Employees/Departments here instead of owning them)
    "MasterData\001_CreateMasterDataDB.sql",

    # Core business services
    "Sales\001_CreateSalesDB.sql",
    "Purchasing\001_CreatePurchasingDB.sql",
    "Purchasing\002_AddOutboxMessages.sql",
    "Warehouse\001_CreateWarehouseDB.sql",
    "Accounting\001_CreateAccountingDB.sql",
    "Product\001_CreateProductDB.sql",
    "Product\002_AddProductTranslations.sql",
    "Product\003_AddProductAttributesAndDimensions.sql",
    "Product\004_AddProductExternalMappings.sql",
    "Product\005_AddSearchIndexQueue.sql",

    # Customer-facing services
    "CRM\001_CreateCrmDB.sql",
    "Payment\001_CreatePaymentDB.sql",
    "Shipping\001_CreateShippingDB.sql",

    # HR & Retail
    "HRM\001_CreateHrmDB.sql",
    "HRM\002_RemoveEmployeeDepartmentOwnership.sql",
    "POS\001_CreatePosDB.sql",

    # Cross-cutting
    "Promotion\001_CreatePromotionDB.sql",
    "Notification\001_CreateNotificationDB.sql",
    "Reporting\001_CreateReportingDB.sql"
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
