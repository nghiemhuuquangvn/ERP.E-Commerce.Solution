<#
.SYNOPSIS
    Exports NotificationService as a standalone package, droppable into an
    unrelated project (docs/architecture/common-services-roadmap.md, Tier 1 #1).

.DESCRIPTION
    Copies NotificationService's 4 projects plus the specific ERP.Shared.* sub-projects
    it actually depends on (per its own .csproj ProjectReference lists) into a fresh
    folder, retargets the copied .csproj ProjectReference paths for the flatter export
    layout, trims ERP-specific dead weight from Authorization (ModuleCodes/
    LicenseEnforcementMiddleware — tied to this platform's tenant-license model;
    HeaderUserContextService/RoleDefinitions — unused by this service), replaces the
    ~13-ERP-specific-event IntegrationEventHandlers.cs and the 3 ERP-topic-specific
    consumers (IdentityService's identity-topic, a payment/shipping/master-data
    consumer, a RabbitMQ sales-order consumer) with one generic example event +
    consumer pair, drops the now-unused RabbitMQ.Client dependency, swaps in a trimmed
    PermissionConstants.cs and a generic seed-template migration
    (deployment/scripts/export-templates/notification/), and adds a self-contained
    Dockerfile/docker-compose.yml/README.md.

    Mirrors deployment/scripts/export-identity-service.ps1's structure — see that
    script's own comments for the general pattern this one follows.

    Never run automatically — this is a manual, on-demand export tool. The output
    folder is gitignored and never committed to this monorepo.

.PARAMETER OutputPath
    Where to write the export. Defaults to export/notification-service-standalone
    under the repo root.

.EXAMPLE
    ./deployment/scripts/export-notification-service.ps1
    ./deployment/scripts/export-notification-service.ps1 -OutputPath C:\temp\my-export
#>
param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$templates = Join-Path $PSScriptRoot "export-templates\notification"

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "export\notification-service-standalone"
}
if (Test-Path $OutputPath) {
    Write-Host "Removing existing export at $OutputPath"
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$OutputPath = (Resolve-Path $OutputPath).Path

function Remove-BinObj([string]$path) {
    Get-ChildItem -Path $path -Recurse -Directory -Include "bin", "obj" -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# ── 1. Copy NotificationService's 4 projects ──────────────────────────────────
Write-Host "Copying NotificationService projects..."
$ntfSrc = Join-Path $repoRoot "src\services\NotificationService\src"
$destSrc = Join-Path $OutputPath "src"
New-Item -ItemType Directory -Path $destSrc -Force | Out-Null
foreach ($proj in @("ERP.NTF.Domain", "ERP.NTF.Application", "ERP.NTF.Infrastructure", "ERP.NTF.API")) {
    Copy-Item -Path (Join-Path $ntfSrc $proj) -Destination (Join-Path $destSrc $proj) -Recurse
}
Remove-BinObj $destSrc

# ── 2. Copy the specific ERP.Shared.* sub-projects NotificationService uses ───
# (Same set as IdentityService's export — Middleware/Observability are transitive
# ProjectReferences of Extensions and won't build without them, even though
# NotificationService never uses them directly. Infrastructure/Data is excluded:
# unused here, only PurchaseOrderService references it in the source monorepo.)
Write-Host "Copying ERP.Shared sub-projects..."
$sharedSrc = Join-Path $repoRoot "src\Shared"
$destShared = Join-Path $OutputPath "Shared"
New-Item -ItemType Directory -Path $destShared -Force | Out-Null

foreach ($proj in @("Core", "Contracts", "Messaging", "Authorization", "Localization", "Middleware", "Observability", "Extensions", "Security", "Versioning")) {
    Copy-Item -Path (Join-Path $sharedSrc $proj) -Destination (Join-Path $destShared $proj) -Recurse
}
# Infrastructure/{Caching,Exceptions} only — not Data
New-Item -ItemType Directory -Path (Join-Path $destShared "Infrastructure") -Force | Out-Null
foreach ($proj in @("Caching", "Exceptions")) {
    Copy-Item -Path (Join-Path $sharedSrc "Infrastructure\$proj") -Destination (Join-Path $destShared "Infrastructure\$proj") -Recurse
}
Remove-BinObj $destShared

# ── 3. Trim ERP/monorepo-specific dead weight from the copied Authorization ───
# Unlike IdentityService's own export, NotificationService DOES use
# AuthenticationExtensions (it validates tokens issued elsewhere, it doesn't issue
# them), HubTicketStore (SignalR ticket workaround) and UserContextHeaderMiddleware
# — those are kept. Only the license/module-gating pair and two confirmed-unused
# files are trimmed.
Write-Host "Trimming dead-weight files from Authorization..."
$authDest = Join-Path $destShared "Authorization"
$filesToRemove = @(
    "ModuleCodes.cs",                # ERP module-code catalog for this platform's tenant-license gating
    "LicenseEnforcementMiddleware.cs", # gates access by ERP module license; meaningless without this platform's License service
    "HeaderUserContextService.cs",   # confirmed unused by NotificationService (it uses JwtClaimsUserContextService instead)
    "RoleDefinitions.cs"             # confirmed unused, dead even in the source monorepo
)
foreach ($f in $filesToRemove) {
    $p = Join-Path $authDest $f
    if (Test-Path $p) { Remove-Item $p -Force }
}
Copy-Item -Path (Join-Path $templates "PermissionConstants.cs") -Destination (Join-Path $authDest "PermissionConstants.cs") -Force

# ── 4. Make ERP.Shared.csproj itself irrelevant — export doesn't ship the umbrella,
#       only the sub-projects each NotificationService .csproj references directly.
$umbrella = Join-Path $destShared "ERP.Shared.csproj"
if (Test-Path $umbrella) { Remove-Item $umbrella -Force }

# ── 5. Retarget ProjectReference paths in the copied NotificationService .csproj ──
# Source monorepo depth: src/services/NotificationService/src/<Project>/ -> ../../../../Shared/
# Export depth:          src/<Project>/                                 -> ../../Shared/
Write-Host "Retargeting ProjectReference paths..."
Get-ChildItem -Path $destSrc -Filter "*.csproj" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $updated = $content -replace [regex]::Escape("..\..\..\..\Shared\"), "..\..\Shared\"
    if ($updated -ne $content) {
        Set-Content -Path $_.FullName -Value $updated -NoNewline
        Write-Host "  retargeted $($_.Name)"
    }
}

# ── 6. Drop the now-unused RabbitMQ.Client dependency from Infrastructure ─────
# (only SalesOrderRabbitMqConsumer used it, and that file is removed in step 7)
$infraCsproj = Join-Path $destSrc "ERP.NTF.Infrastructure\ERP.NTF.Infrastructure.csproj"
$infraContent = Get-Content $infraCsproj -Raw
$infraUpdated = $infraContent -replace '(?m)^\s*<PackageReference Include="RabbitMQ\.Client"[^/]*/>\r?\n', ''
if ($infraUpdated -ne $infraContent) {
    Set-Content -Path $infraCsproj -Value $infraUpdated -NoNewline
    Write-Host "  removed RabbitMQ.Client PackageReference from ERP.NTF.Infrastructure.csproj"
}

# ── 7. Replace ERP-specific event handlers/consumers with the generic example ─
Write-Host "Replacing ERP-specific event handlers/consumers with generic examples..."
$appEventHandlersDest = Join-Path $destSrc "ERP.NTF.Application\EventHandlers"
Remove-Item (Join-Path $appEventHandlersDest "IntegrationEventHandlers.cs") -Force
Copy-Item -Path (Join-Path $templates "ExampleIntegrationEvent.cs") -Destination (Join-Path $appEventHandlersDest "ExampleIntegrationEvent.cs")

$backgroundJobsDest = Join-Path $destSrc "ERP.NTF.Infrastructure\BackgroundJobs"
foreach ($f in @("IdentityEventConsumer.cs", "NotificationKafkaConsumer.cs", "SalesOrderRabbitMqConsumer.cs")) {
    Remove-Item (Join-Path $backgroundJobsDest $f) -Force
}
Copy-Item -Path (Join-Path $templates "ExampleKafkaConsumer.cs") -Destination (Join-Path $backgroundJobsDest "ExampleKafkaConsumer.cs")

Copy-Item -Path (Join-Path $templates "Program.cs") -Destination (Join-Path $destSrc "ERP.NTF.API\Program.cs") -Force

# ── 8. Database migrations: everything except the ERP-specific ones ───────────
Write-Host "Copying database migrations..."
$dbSrc = Join-Path $repoRoot "src\Database\Notification"
$dbDest = Join-Path $OutputPath "Database"
New-Item -ItemType Directory -Path $dbDest -Force | Out-Null

$excluded = @(
    "004_SeedDemoData.sql",          # ~1000 rows keyed off IdentityService's customer-user GUID formula, meaningless standalone
    "005_SeedWorkflowTemplates.sql"  # PurchaseOrderService/HRMService-specific approval-chain templates — replaced below
)
Get-ChildItem -Path $dbSrc -Filter "*.sql" | Where-Object { $excluded -notcontains $_.Name } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination (Join-Path $dbDest $_.Name)
}
Copy-Item -Path (Join-Path $templates "005_SeedGenericTemplates.sql") -Destination (Join-Path $dbDest "005_SeedGenericTemplates.sql")

# ── 9. Docker packaging + docs ─────────────────────────────────────────────────
Write-Host "Copying Docker packaging and docs..."
foreach ($f in @("Dockerfile", "docker-compose.yml", "run-migrations.sh", ".env.example", "README.md")) {
    Copy-Item -Path (Join-Path $templates $f) -Destination (Join-Path $OutputPath $f)
}

# ── 10. Manifest + negative check ──────────────────────────────────────────────
$manifestPath = Join-Path $OutputPath "export-manifest.txt"
@"
NotificationService standalone export
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss K")
Source: $repoRoot

Included:
  src/ERP.NTF.{Domain,Application,Infrastructure,API}  (copied from the source monorepo,
    minus the ERP-specific event handlers/consumers replaced below)
  Shared/{Core,Contracts,Messaging,Authorization,Localization,Middleware,Observability,Extensions,Security,Versioning}
  Shared/Infrastructure/{Caching,Exceptions}
  Database/*.sql  (source monorepo's Notification migrations, minus the exclusions below)

Generated fresh (not copied from the source monorepo):
  Shared/Authorization/PermissionConstants.cs  (trimmed to this service's own constants + a Sample_* example)
  src/ERP.NTF.Application/EventHandlers/ExampleIntegrationEvent.cs  (replaces IntegrationEventHandlers.cs's ~13 ERP-specific event/handler pairs)
  src/ERP.NTF.Infrastructure/BackgroundJobs/ExampleKafkaConsumer.cs  (replaces IdentityEventConsumer/NotificationKafkaConsumer/SalesOrderRabbitMqConsumer)
  src/ERP.NTF.API/Program.cs  (same as the source, minus the RabbitMQ connection, license/module-gating middleware, and the 3 removed consumers' registration)
  Database/005_SeedGenericTemplates.sql  (replaces the PO-approval/Leave-request-specific original)
  Dockerfile, docker-compose.yml, run-migrations.sh, .env.example, README.md

Excluded (dead weight or ERP-specific coupling for a standalone deployment):
  Shared/Authorization/{ModuleCodes,LicenseEnforcementMiddleware,HeaderUserContextService,RoleDefinitions}.cs
  Shared/Infrastructure/Data  (unused here; only PurchaseOrderService references it)
  RabbitMQ.Client PackageReference on ERP.NTF.Infrastructure.csproj (only the removed SalesOrderRabbitMqConsumer used it)
  Database/004_SeedDemoData.sql, 005_SeedWorkflowTemplates.sql
"@ | Set-Content -Path $manifestPath

Write-Host ""
Write-Host "Negative check: scanning for monorepo-specific strings that shouldn't have leaked in..."
$leakPatterns = @("IdentityService", "SalesOrderService", "PurchaseOrderService", "HRMService", "ChatBot", "sales_order_events", "identity-topic", "purchasing-topic", "hrm-topic")
$leaks = Get-ChildItem -Path $OutputPath -Recurse -File -Include "*.cs", "*.sql", "*.csproj" |
    Select-String -Pattern $leakPatterns -SimpleMatch
if ($leaks) {
    Write-Warning "Potential leaked monorepo-specific content found:"
    $leaks | ForEach-Object { Write-Warning "  $($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
} else {
    Write-Host "  OK — no matches."
}

Write-Host ""
Write-Host "Export complete: $OutputPath"
