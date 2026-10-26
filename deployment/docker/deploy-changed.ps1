<#
.SYNOPSIS
  Builds and redeploys ONLY the compose services whose sources changed since their last deploy, then frees the build cache.

.DESCRIPTION
  - A service is "changed" when the content hash of its own folder + the shared inputs of the root Dockerfile
    (src/Shared, Dockerfile, .dockerignore, Directory.*) differs from the hash stored at its last successful deploy
    (deployment/docker/.deploy-state.json, git-ignored). Uncommitted edits count; bin/obj/node_modules do not.
    A change under src/Shared therefore redeploys every .NET service (they all compile it); a change in one service folder, only that one.
  - Services are built one at a time (the Docker VM is small), started with --no-deps, and after EACH one the
    BuildKit cache and dangling images are removed, so disk use does not grow with every rebuild (-KeepCache to skip).
  - The service -> folder map is read from docker-compose.yml (SERVICE_PATH / frontend build context): a new service needs no edit here.

.EXAMPLE
  ./deployment/docker/deploy-changed.ps1 -DryRun          # what would be rebuilt
  ./deployment/docker/deploy-changed.ps1                  # rebuild + restart what changed
  ./deployment/docker/deploy-changed.ps1 -Services warehouseservice,admin
  ./deployment/docker/deploy-changed.ps1 -All
  ./deployment/docker/deploy-changed.ps1 -CleanOnly       # just free cache / dangling images
#>
[CmdletBinding()]
param(
    [string[]] $Services,
    [switch] $All,
    [switch] $DryRun,
    [switch] $KeepCache,
    [switch] $CleanOnly,
    [switch] $MarkCurrent,          # record the current sources as deployed without building (after a manual deploy)
    [string[]] $ComposeFiles,
    [int] $MinFreeGB = 8            # refuse to start a build when the drive holding Docker has less free space
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root
$statePath = Join-Path $PSScriptRoot '.deploy-state.json'
$env:COMPOSE_PARALLEL_LIMIT = '1'

if (-not $ComposeFiles) {
    $ComposeFiles = @('docker-compose.yml')
    if (Test-Path 'docker-compose.localsql.yml') { $ComposeFiles += 'docker-compose.localsql.yml' }
}
$compose = @($ComposeFiles | ForEach-Object { '-f'; $_ })

function Show-Disk {
    $c = Get-PSDrive -Name ((Get-Location).Path.Substring(0, 1))
    '{0}: free {1:N1} GB' -f $c.Name, ($c.Free / 1GB)
    docker system df 2>&1 | Out-String | Write-Host
}

function Clear-BuildCache {
    docker builder prune -a -f 2>&1 | Select-Object -Last 1 | ForEach-Object { Write-Host "  build cache: $_" }
    docker image prune -f 2>&1 | Select-Object -Last 1 | ForEach-Object { Write-Host "  dangling images: $_" }
}

if ($CleanOnly) { Clear-BuildCache; Show-Disk; return }

# ── service -> folder map, from docker-compose.yml ────────────────────────────
$map = [ordered]@{}   # service -> relative folder that holds its code
$current = $null
foreach ($line in Get-Content 'docker-compose.yml') {
    if ($line -match '^networks:|^volumes:') { break }
    if ($line -match '^  ([a-z0-9-]+):\s*$') { $current = $Matches[1]; continue }
    if (-not $current) { continue }
    if ($line -match '^\s+SERVICE_PATH:\s*(\S+)') {
        $parts = $Matches[1] -split '/'
        # src/services/<Service>/src/<Project> -> src/services/<Service>;  src/Gateway/Ocelot.API -> itself
        $map[$current] = if ($parts[1] -eq 'services') { ($parts[0..2] -join '/') } else { $Matches[1] }
    }
    elseif ($line -match '^\s+context:\s*(src/frontend/\S+)') { $map[$current] = $Matches[1] }
}
$shared = @('src/Shared', 'Dockerfile', '.dockerignore', 'Directory.Build.props', 'Directory.Packages.props', 'nuget.config')

function Get-InputHash([string[]] $Folders) {
    $files = foreach ($f in $Folders) {
        if (-not (Test-Path $f)) { continue }
        if ((Get-Item $f).PSIsContainer) {
            Get-ChildItem $f -Recurse -File -Force -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj|node_modules|dist|\.git|\.vs|\.expo)[\\/]' }
        } else { Get-Item $f }
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    $sb = [Text.StringBuilder]::new()
    foreach ($file in ($files | Sort-Object FullName)) {
        $rel = $file.FullName.Substring($root.Length)
        [void]$sb.Append($rel).Append(':').Append((Get-FileHash $file.FullName -Algorithm SHA256).Hash).Append(';')
    }
    [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sb.ToString()))).Replace('-', '')
}

$state = if (Test-Path $statePath) { Get-Content $statePath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
$sharedHash = $null
$hashes = @{}
foreach ($svc in $map.Keys) {
    $isFrontend = $map[$svc] -like 'src/frontend/*'
    if (-not $isFrontend -and -not $sharedHash) { $sharedHash = Get-InputHash $shared }
    $own = Get-InputHash @($map[$svc])
    $hashes[$svc] = if ($isFrontend) { $own } else { "$own|$sharedHash" }
}

# ── what to build ─────────────────────────────────────────────────────────────
if ($Services) {
    $unknown = $Services | Where-Object { -not $map.Contains($_) }
    if ($unknown) { throw "Unknown service(s): $($unknown -join ', '). Known: $($map.Keys -join ', ')" }
    $todo = $Services
} elseif ($All) { $todo = @($map.Keys) }
else { $todo = @($map.Keys | Where-Object { $state[$_] -ne $hashes[$_] }) }

if ($MarkCurrent) {
    foreach ($s in $map.Keys) { $state[$s] = $hashes[$s] }
    $state | ConvertTo-Json | Set-Content $statePath
    Write-Host "Recorded the current sources of $($map.Count) services as deployed."
    return
}

Write-Host ("Changed since last deploy: {0}" -f ($(if ($todo) { $todo -join ', ' } else { '(nothing)' })))
if ($DryRun -or -not $todo) { if (-not $todo) { Write-Host 'Nothing to build.' }; return }

# ── build + restart, one service at a time ────────────────────────────────────
Show-Disk
$failed = @()
foreach ($svc in $todo) {
    $systemDrive = Get-PSDrive -Name ($env:SystemDrive.TrimEnd(':'))
    if ($systemDrive.Free / 1GB -lt $MinFreeGB) {
        Write-Warning ("Only {0:N1} GB free on {1}: - stopping before building {2}. Free space (see -CleanOnly) and run again." -f ($systemDrive.Free / 1GB), $systemDrive.Name, $svc)
        $failed += $svc; break
    }

    Write-Host "=== $svc" -ForegroundColor Cyan
    docker compose @compose build $svc
    if ($LASTEXITCODE -ne 0) { $failed += $svc; if (-not $KeepCache) { Clear-BuildCache }; continue }
    docker compose @compose up -d --no-deps $svc
    if ($LASTEXITCODE -ne 0) { $failed += $svc; continue }

    $state[$svc] = $hashes[$svc]
    $state | ConvertTo-Json | Set-Content $statePath      # saved per service: a later failure keeps the earlier successes
    if (-not $KeepCache) { Clear-BuildCache }
}

Show-Disk
if ($failed) { Write-Error "Not deployed: $($failed -join ', ')"; exit 1 }
Write-Host 'Done.'
