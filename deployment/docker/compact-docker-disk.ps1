<#
.SYNOPSIS
  Gives back to Windows the space Docker no longer uses: docker_data.vhdx only grows, it never shrinks by itself.

.DESCRIPTION
  Docker Desktop on WSL2 has no "virtual disk limit" setting, so the size is kept down by compacting the file when it has grown.
  Run in an ADMINISTRATOR PowerShell. It: stops the compose stack -> fstrim inside WSL (tells the virtual disk which blocks are free;
  without it the compaction gives back almost nothing) -> quits Docker Desktop -> wsl --shutdown -> diskpart "compact vdisk" ->
  starts Docker Desktop -> starts the stack again (not built). Takes a few minutes; every container is down meanwhile.
  Free the cache first (./deploy-changed.ps1 -CleanOnly) or there is little to give back.

.EXAMPLE
  ./deployment/docker/compact-docker-disk.ps1                 # only if the file is above -ThresholdGB (default 80)
  ./deployment/docker/compact-docker-disk.ps1 -Force          # whatever its size
  ./deployment/docker/compact-docker-disk.ps1 -NoRestart      # leave the stack stopped
#>
[CmdletBinding()]
param(
    [int] $ThresholdGB = 80,
    [switch] $Force,
    [switch] $NoRestart,
    [string[]] $ComposeFiles
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root
$vhd = Join-Path $env:LOCALAPPDATA 'Docker\wsl\disk\docker_data.vhdx'
$diskpart = Join-Path $env:SystemRoot 'System32\diskpart.exe'
$desktop = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this in an administrator PowerShell (diskpart needs it).'
}
if (-not (Test-Path $vhd)) { throw "Not found: $vhd (Docker Desktop with the WSL2 backend?)" }
if (-not $ComposeFiles) {
    $ComposeFiles = @('docker-compose.yml')
    if (Test-Path 'docker-compose.localsql.yml') { $ComposeFiles += 'docker-compose.localsql.yml' }
}
$compose = @($ComposeFiles | ForEach-Object { '-f'; $_ })
$sizeGB = { (Get-Item $vhd).Length / 1GB }
$before = & $sizeGB
Write-Host ('docker_data.vhdx is {0:N1} GB (threshold {1} GB).' -f $before, $ThresholdGB)
if (-not $Force -and $before -lt $ThresholdGB) { Write-Host 'Below the threshold: nothing to do (use -Force).'; return }

function Wait-Docker([int] $Seconds = 300) {
    $end = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $end) {
        docker info *> $null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep 5
    }
    throw 'Docker did not come up in time.'
}

# 1. Docker must be running for fstrim; stop the stack so nothing writes meanwhile.
if (-not (Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue)) { Start-Process $desktop }
Wait-Docker
Write-Host '1/5 stopping the stack'
docker compose @compose stop 2>&1 | Select-Object -Last 1

# 2. Tell the virtual disk which blocks are free (busybox ships in the docker-desktop distro; there is no fstrim binary).
Write-Host '2/5 fstrim'
wsl -d docker-desktop -u root -e busybox fstrim -v /mnt/docker-desktop-disk
if ($LASTEXITCODE -ne 0) { throw 'fstrim failed: nothing was changed (the stack is stopped; start it with docker compose up -d --no-build).' }

# 3. Quit Docker Desktop and WSL so the file is not in use.
Write-Host '3/5 stopping Docker Desktop and WSL'
docker desktop stop 2>&1 | Select-Object -Last 1
Start-Sleep 15
wsl --shutdown
Start-Sleep 5

# 4. Compact.
Write-Host '4/5 compacting (a few minutes)'
$script = Join-Path $env:TEMP 'compact-docker-vdisk.txt'
@("select vdisk file=`"$vhd`"", 'attach vdisk readonly', 'compact vdisk', 'detach vdisk', 'exit') | Set-Content $script -Encoding ascii
& $diskpart /s $script | Select-Object -Last 8
Remove-Item $script -ErrorAction SilentlyContinue
$after = & $sizeGB
Write-Host ('docker_data.vhdx: {0:N1} GB -> {1:N1} GB' -f $before, $after) -ForegroundColor Green

# 5. Start again.
if ($NoRestart) { Write-Host 'Left stopped (-NoRestart).'; return }
Write-Host '5/5 starting Docker Desktop and the stack'
Start-Process $desktop
Start-Sleep 20
Wait-Docker
$env:COMPOSE_PARALLEL_LIMIT = '1'
docker compose @compose up -d --no-build 2>&1 | Select-Object -Last 2
Get-PSDrive -Name ($env:SystemDrive.TrimEnd(':')) | ForEach-Object { Write-Host ('{0}: free {1:N1} GB' -f $_.Name, ($_.Free / 1GB)) }
