# Dot-source this file: . .\android-env.ps1
# Points the CURRENT PowerShell process at the portable JDK / Android SDK on D: (nothing is written to the machine or user environment).
$script:AndroidRoot = if ($env:ERP_ANDROID_ROOT) { $env:ERP_ANDROID_ROOT } else { 'D:\Android' }
$env:JAVA_HOME = Join-Path $AndroidRoot 'jdk17'
$env:ANDROID_HOME = Join-Path $AndroidRoot 'Sdk'
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:ANDROID_AVD_HOME = Join-Path $AndroidRoot 'avd'
$env:ANDROID_USER_HOME = Join-Path $AndroidRoot 'user'
$env:GRADLE_USER_HOME = Join-Path $AndroidRoot 'gradle'
$sys32 = Join-Path $env:SystemRoot 'System32'   # the .bat tools of the SDK need findstr.exe, which some shells leave off PATH
$env:PATH = @(
  $sys32,
  (Join-Path $env:JAVA_HOME 'bin'),
  (Join-Path $env:ANDROID_HOME 'cmdline-tools\latest\bin'),
  (Join-Path $env:ANDROID_HOME 'platform-tools'),
  (Join-Path $env:ANDROID_HOME 'emulator'),
  $env:PATH
) -join ';'
