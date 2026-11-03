# Installs a portable JDK 17 + Android SDK (command-line tools, platform-tools, emulator, API 34, build-tools, NDK, CMake, Google APIs x86_64 image)
# under D:\Android (override with $env:ERP_ANDROID_ROOT). Idempotent: run it again and finished parts are skipped.
# No admin rights, no machine / user environment change. About 10 GB of downloads.
# usage:  powershell -ExecutionPolicy Bypass -File src\frontend\mobile\tools\setup-android-sdk.ps1
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'android-env.ps1')

$jdkUrl = 'https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.20.1%2B1/OpenJDK17U-jdk_x64_windows_hotspot_17.0.20.1_1.zip'
$toolsUrl = 'https://dl.google.com/android/repository/commandlinetools-win-15859902_latest.zip'
$packages = @(
  'platform-tools', 'emulator', 'platforms;android-34', 'build-tools;34.0.0',
  'ndk;26.1.10909125', 'cmake;3.22.1', 'system-images;android-34;google_apis;x86_64'
)

function Expand-Download($url, $dest, $probe) {
  if (Test-Path $probe) { Write-Host "skip   $dest (present)"; return }
  $zip = Join-Path (Join-Path $AndroidRoot 'downloads') (Split-Path $url -Leaf)
  New-Item -ItemType Directory -Force (Split-Path $zip) | Out-Null
  if (-not (Test-Path $zip)) { Write-Host "download $url"; Invoke-WebRequest $url -OutFile $zip }
  $tmp = Join-Path $AndroidRoot 'downloads\unzip'
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  Expand-Archive $zip $tmp -Force
  $inner = Get-ChildItem $tmp | Select-Object -First 1
  New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
  if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
  Move-Item $inner.FullName $dest
  Write-Host "ready  $dest"
}

New-Item -ItemType Directory -Force $AndroidRoot, $env:ANDROID_AVD_HOME, $env:ANDROID_USER_HOME, $env:GRADLE_USER_HOME | Out-Null
Expand-Download $jdkUrl $env:JAVA_HOME (Join-Path $env:JAVA_HOME 'bin\java.exe')
Expand-Download $toolsUrl (Join-Path $env:ANDROID_HOME 'cmdline-tools\latest') (Join-Path $env:ANDROID_HOME 'cmdline-tools\latest\bin\sdkmanager.bat')

# the Android SDK licences: accepted by whoever runs this script (the owner asked for the build)
(1..30 | ForEach-Object { 'y' }) | & sdkmanager.bat --licenses | Out-Null
foreach ($p in $packages) {
  Write-Host "sdkmanager $p"
  & sdkmanager.bat --install $p | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "sdkmanager failed for $p" }
}
& sdkmanager.bat --list_installed
