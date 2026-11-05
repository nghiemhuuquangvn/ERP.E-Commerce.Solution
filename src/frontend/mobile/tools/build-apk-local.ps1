# Builds a release APK of one Expo app on this PC (no EAS account needed).
#   powershell -ExecutionPolicy Bypass -File src\frontend\mobile\tools\build-apk-local.ps1 -App staff|customer|admin [-ApiUrl http://shop.glcheck.lan/api]
#   Admin app, one APK per tenant (default SYSTEM): ... -App admin -Tenant glcheck -ApiUrl http://glcheck.erp.local/api [-AppName 'ERP Admin - glcheck'] [-BrandColor '#0369a1']
# Why a copy on a short path: Windows' 260-character limit breaks CMake / ninja inside node_modules paths.
# Output: src\frontend\mobile\apk\<app>-preview.apk (git-ignored). Signed with the debug keystore that `expo prebuild` generates: internal tests only.
# The tree of the app is never touched (the generated android/ folder lives in the copy).
param(
  [Parameter(Mandatory = $true)][ValidateSet('staff', 'customer', 'admin')][string]$App,
  [string]$ApiUrl = '',
  [string]$Tenant = 'SYSTEM',
  [string]$AppName = '',
  [string]$BrandColor = '',
  [string]$WorkRoot = 'D:\b',
  # Real push (admin app): the tenant's Firebase google-services.json (its package_name must equal the APK package). Without it push stays off.
  [string]$GoogleServicesFile = ''
)
$ErrorActionPreference = 'Stop'
if (-not $ApiUrl) { $ApiUrl = if ($App -eq 'admin') { 'http://admin.erp.local/api' } else { 'http://shop.glcheck.lan/api' } }
. (Join-Path $PSScriptRoot 'android-env.ps1')

$src = Resolve-Path (Join-Path $PSScriptRoot "..\$App")
$work = Join-Path $WorkRoot $App
$apkDir = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')) 'apk'
New-Item -ItemType Directory -Force $work, $apkDir | Out-Null

Write-Host "== copy $src -> $work"
robocopy $src $work /MIR /XD node_modules android ios .expo dist /XF '*.log' /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
# /MIR above would delete a node_modules / android of the copy; they are excluded from both sides, so they survive.

$env:APP_VARIANT = 'preview'
$env:EXPO_PUBLIC_API_URL = $ApiUrl
if ($App -eq 'admin') {
  $env:TENANT_CODE = $Tenant
  if ($AppName) { $env:APP_NAME = $AppName }
  if ($BrandColor) { $env:BRAND_COLOR = $BrandColor }
  if ($GoogleServicesFile) {
    if (-not (Test-Path $GoogleServicesFile)) { throw "google-services.json not found: $GoogleServicesFile" }
    Copy-Item $GoogleServicesFile (Join-Path $work 'google-services.json') -Force
    $env:GOOGLE_SERVICES_FILE = './google-services.json'
    $env:PUSH_ENABLED = 'true'
  } else {
    Remove-Item Env:GOOGLE_SERVICES_FILE, Env:PUSH_ENABLED -ErrorAction SilentlyContinue
  }
}
$env:CI = '1'
Push-Location $work
try {
  # `npm install`, not `npm ci`: npm 11 wants peer deps (@babel/preset-env) the committed lock file lacks; only the copy's lock changes.
  Write-Host '== npm install'
  npm install --no-audit --no-fund
  if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }
  Write-Host '== expo prebuild (android)'
  npx expo prebuild --platform android --no-install --clean
  if ($LASTEXITCODE -ne 0) { throw 'expo prebuild failed' }
  Write-Host '== gradle assembleRelease'
  Push-Location android
  try {
    .\gradlew.bat assembleRelease --no-daemon "-PreactNativeArchitectures=x86_64,arm64-v8a"
    if ($LASTEXITCODE -ne 0) { throw 'gradle assembleRelease failed' }
  } finally { Pop-Location }
  $apk = Get-ChildItem (Join-Path $work 'android\app\build\outputs\apk\release') -Filter *.apk | Select-Object -First 1
  if (-not $apk) { throw 'no APK produced' }
  $apkName = if ($App -eq 'admin') { "admin-$($Tenant.ToLower())-preview.apk" } else { "$App-preview.apk" }
  $target = Join-Path $apkDir $apkName
  Copy-Item $apk.FullName $target -Force
  Write-Host ("APK: {0} ({1:N1} MB), API {2}" -f $target, ($apk.Length / 1MB), $ApiUrl)
} finally { Pop-Location }
