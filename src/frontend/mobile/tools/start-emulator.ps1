# Creates (once) and starts the Android emulator AVD "erp-pixel" (Pixel 6, Android 14, Google APIs, x86_64) and makes it resolve the test shop's
# host name to this PC:  shop.glcheck.lan -> 10.0.2.2  (10.0.2.2 is the emulator's alias of the PC). Only the emulator image's own hosts file is
# edited - never the Windows hosts file, no router, no firewall rule.
#   powershell/pwsh -ExecutionPolicy Bypass -File src\frontend\mobile\tools\start-emulator.ps1 [-HostName shop.glcheck.lan] [-Headless]
param(
  [string]$HostName = 'shop.glcheck.lan',
  [string]$Avd = 'erp-pixel',
  [switch]$Headless
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'android-env.ps1')

$existing = & avdmanager.bat list avd -c
if ($existing -notcontains $Avd) {
  Write-Host "== create AVD $Avd"
  'no' | & avdmanager.bat create avd -n $Avd -k 'system-images;android-34;google_apis;x86_64' -d pixel_6 --force | Out-Null
  $ini = Join-Path $env:ANDROID_AVD_HOME "$Avd.avd\config.ini"
  $lines = Get-Content $ini | Where-Object { $_ -notmatch '^(hw.camera.back|hw.camera.front|hw.ramSize|hw.keyboard|disk.dataPartition.size)=' }
  $lines += 'hw.camera.back=virtualscene', 'hw.camera.front=emulated', 'hw.ramSize=4096', 'hw.keyboard=yes', 'disk.dataPartition.size=8G'
  Set-Content $ini $lines
}

$running = (& adb.exe devices) -match 'emulator-\d+\s+device'
if (-not $running) {
  Write-Host "== start emulator $Avd"
  $args = @('-avd', $Avd, '-writable-system', '-no-snapshot', '-gpu', 'host', '-netdelay', 'none', '-netspeed', 'full')
  if ($Headless) { $args += '-no-window' }
  Start-Process (Join-Path $env:ANDROID_HOME 'emulator\emulator.exe') -ArgumentList $args -WindowStyle Minimized
}
& adb.exe wait-for-device
while ((& adb.exe shell getprop sys.boot_completed 2>$null).Trim() -ne '1') { Start-Sleep -Seconds 3 }
Write-Host 'booted'

$line = "10.0.2.2 $HostName"
$has = (& adb.exe shell "grep -c '$HostName' /system/etc/hosts" 2>$null).Trim()
if ($has -ne '1') {
  Write-Host "== hosts entry: $line"
  & adb.exe root | Out-Null
  Start-Sleep -Seconds 3
  & adb.exe wait-for-device
  $out = (& adb.exe remount 2>&1) -join "`n"
  if ($out -match 'verity|reboot') {
    & adb.exe disable-verity | Out-Null
    & adb.exe reboot
    Start-Sleep -Seconds 10
    & adb.exe wait-for-device
    while ((& adb.exe shell getprop sys.boot_completed 2>$null).Trim() -ne '1') { Start-Sleep -Seconds 3 }
    & adb.exe root | Out-Null
    Start-Sleep -Seconds 3
    & adb.exe wait-for-device
    $out = (& adb.exe remount 2>&1) -join "`n"
  }
  Write-Host $out
  & adb.exe shell "echo '$line' >> /system/etc/hosts"
}
Write-Host ('hosts: ' + (& adb.exe shell "grep '$HostName' /system/etc/hosts"))
Write-Host ('resolves: ' + (& adb.exe shell "ping -c 1 -W 2 $HostName 2>&1 | head -1"))
