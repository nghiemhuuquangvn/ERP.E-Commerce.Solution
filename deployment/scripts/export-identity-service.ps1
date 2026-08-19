<#
.SYNOPSIS
    Exports IdentityService as a standalone package, droppable into an unrelated
    project (CommonServiceRoadmap.md §7 / Part B).

.DESCRIPTION
    Copies IdentityService's 4 projects plus the specific ERP.Shared.* sub-projects
    it actually depends on (per its own .csproj ProjectReference lists — see Part B1)
    into a fresh folder, retargets the copied .csproj ProjectReference paths for the
    flatter export layout, trims ERP-specific dead weight (AuthenticationExtensions,
    HubTicketStore, etc. — built for the OTHER 13 services in the source monorepo,
    meaningless standalone), replaces the ~230-business-permission-code
    PermissionConstants.cs and the ERP-specific seed migrations with generic
    equivalents (deployment/scripts/export-templates/), and adds a self-contained
    Dockerfile/docker-compose.yml/README.md.

    Never run automatically — this is a manual, on-demand export tool. The output
    folder is gitignored and never committed to this monorepo.

.PARAMETER OutputPath
    Where to write the export. Defaults to export/identity-service-standalone
    under the repo root.

.EXAMPLE
    ./deployment/scripts/export-identity-service.ps1
    ./deployment/scripts/export-identity-service.ps1 -OutputPath C:\temp\my-export
#>
param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$templates = Join-Path $PSScriptRoot "export-templates"

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "export\identity-service-standalone"
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

# ── 1. Copy IdentityService's 4 projects ──────────────────────────────────────
Write-Host "Copying IdentityService projects..."
$identitySrc = Join-Path $repoRoot "src\services\IdentityService\src"
$destSrc = Join-Path $OutputPath "src"
New-Item -ItemType Directory -Path $destSrc -Force | Out-Null
foreach ($proj in @("ERP.Identity.Domain", "ERP.Identity.Application", "ERP.Identity.Infrastructure", "ERP.Identity.Api")) {
    Copy-Item -Path (Join-Path $identitySrc $proj) -Destination (Join-Path $destSrc $proj) -Recurse
}
Remove-BinObj $destSrc

# ── 2. Copy the specific ERP.Shared.* sub-projects IdentityService uses ───────
# (Observability is included even though Identity never uses it directly —
# ERP.Shared.Extensions.csproj has a hard ProjectReference to it and won't build
# without it. Infrastructure/Data is excluded: confirmed unused by Identity,
# only PurchaseOrderService references it in the source monorepo.)
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
Write-Host "Trimming dead-weight files from Authorization..."
$authDest = Join-Path $destShared "Authorization"
$filesToRemove = @(
    "AuthenticationExtensions.cs",   # built for the OTHER 13 services to validate Identity-issued JWTs; Identity itself never calls it
    "HubTicketStore.cs",             # SignalR ticket store for Gateway/ChatBot/Notification hubs; Identity has no SignalR hub
    "UserContextHeaderMiddleware.cs",# Gateway-forwarded-header user context; Identity uses JwtClaimsUserContextService instead
    "HeaderUserContextService.cs",   # pairs with the above; same reason
    "ModuleCodes.cs",                # ERP module code catalog for the OTHER 13 services' license gating
    "LicenseEnforcementMiddleware.cs", # gates access by ERP module license; Identity is the license ISSUER, not a consumer
    "RoleDefinitions.cs"             # dead code even in the source monorepo (only ever read by a one-time seed script, itself excluded below)
)
foreach ($f in $filesToRemove) {
    $p = Join-Path $authDest $f
    if (Test-Path $p) { Remove-Item $p -Force }
}
Copy-Item -Path (Join-Path $templates "PermissionConstants.cs") -Destination (Join-Path $authDest "PermissionConstants.cs") -Force

# ── 4. Make ERP.Shared.csproj itself irrelevant — export doesn't ship the umbrella,
#       only the sub-projects each Identity .csproj references directly.
$umbrella = Join-Path $destShared "ERP.Shared.csproj"
if (Test-Path $umbrella) { Remove-Item $umbrella -Force }

# ── 5. Retarget ProjectReference paths in the copied Identity .csproj files ───
# Source monorepo depth: src/services/IdentityService/src/<Project>/ -> ../../../../Shared/
# Export depth:          src/<Project>/                              -> ../../Shared/
Write-Host "Retargeting ProjectReference paths..."
Get-ChildItem -Path $destSrc -Filter "*.csproj" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $updated = $content -replace [regex]::Escape("..\..\..\..\Shared\"), "..\..\Shared\"
    if ($updated -ne $content) {
        Set-Content -Path $_.FullName -Value $updated -NoNewline
        Write-Host "  retargeted $($_.Name)"
    }
}

# ── 6. Database migrations: everything except the ERP-specific ones ───────────
Write-Host "Copying database migrations..."
$dbSrc = Join-Path $repoRoot "src\Database\Identity"
$dbDest = Join-Path $OutputPath "Database"
New-Item -ItemType Directory -Path $dbDest -Force | Out-Null

$excluded = @(
    "013_SeedRoleDefinitionsIntoRolePermissions.sql", # ~230 other-services' permission codes — replaced below
    "015_SeedDemoUsersAndRoles.sql",                  # monorepo-wide deterministic-GUID demo data, meaningless standalone
    "015_test_wrapped.sql",                           # looks like an accidentally-committed ad-hoc test script
    "016_SeedFinanceApprovePermission.sql",           # seeds a PurchaseOrderService-only permission code
    "017_AddChatBotLicenseModules.sql",               # ChatBotService-specific
    "018_AddChatBotAssistantPermission.sql"           # ChatBotService-specific
)
Get-ChildItem -Path $dbSrc -Filter "*.sql" | Where-Object { $excluded -notcontains $_.Name } | ForEach-Object {
    $destName = $_.Name
    # Pre-existing filename collision in the source monorepo (both legitimate,
    # alphabetically-ordered by run-migrations.sh there) — disambiguate here so an
    # external consumer doesn't inherit an unexplained-looking pair.
    if ($destName -eq "008_AddPermissionsAuditColumns.sql") { $destName = "008b_AddPermissionsAuditColumns.sql" }
    Copy-Item -Path $_.FullName -Destination (Join-Path $dbDest $destName)
}
Copy-Item -Path (Join-Path $templates "013_SeedGenericRolePermissions.sql") -Destination (Join-Path $dbDest "013_SeedGenericRolePermissions.sql")

# ── 7. Docker packaging + docs ─────────────────────────────────────────────────
Write-Host "Copying Docker packaging and docs..."
foreach ($f in @("Dockerfile", "docker-compose.yml", "run-migrations.sh", ".env.example", "README.md")) {
    Copy-Item -Path (Join-Path $templates $f) -Destination (Join-Path $OutputPath $f)
}

# ── 8. Manifest + negative check ───────────────────────────────────────────────
$manifestPath = Join-Path $OutputPath "export-manifest.txt"
@"
IdentityService standalone export
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss K")
Source: $repoRoot

Included:
  src/ERP.Identity.{Domain,Application,Infrastructure,Api}  (copied verbatim from the source monorepo)
  Shared/{Core,Contracts,Messaging,Authorization,Localization,Middleware,Observability,Extensions,Security,Versioning}
  Shared/Infrastructure/{Caching,Exceptions}
  Database/*.sql  (source monorepo's Identity migrations, minus the exclusions below)

Generated fresh (not copied from the source monorepo):
  Shared/Authorization/PermissionConstants.cs  (trimmed to Identity's own constants + a Sample_* example)
  Database/013_SeedGenericRolePermissions.sql  (replaces the 230-business-permission-code original)
  Dockerfile, docker-compose.yml, run-migrations.sh, .env.example, README.md

Excluded (dead weight for a standalone deployment):
  Shared/Authorization/{AuthenticationExtensions,HubTicketStore,UserContextHeaderMiddleware,
    HeaderUserContextService,ModuleCodes,LicenseEnforcementMiddleware,RoleDefinitions}.cs
  Shared/Infrastructure/Data  (unused by Identity; only PurchaseOrderService references it)
  Database/013_SeedRoleDefinitionsIntoRolePermissions.sql, 015_SeedDemoUsersAndRoles.sql,
    015_test_wrapped.sql, 016_SeedFinanceApprovePermission.sql, 017_AddChatBotLicenseModules.sql,
    018_AddChatBotAssistantPermission.sql
"@ | Set-Content -Path $manifestPath

Write-Host ""
Write-Host "Negative check: scanning for monorepo-specific strings that shouldn't have leaked in..."
$leakPatterns = @("ChatBot", "PurchaseOrderService", "SalesOrderService", "SEED_DATA_CONVENTIONS", "crm:", "sales_order:", "purchase_order:")
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
