<#
  Cho phep dien thoai trong cung Wi-Fi vao cong 80 cua may nay (nginx cua stack Docker), CHI tren mang "Private".
  Chay bang PowerShell "Run as administrator":
      .\allow-lan-http.ps1            # mo cong 80 (rule "ERP LAN test (HTTP 80)")
      .\allow-lan-http.ps1 -Remove    # dong lai, xoa rule
  Script nay do ANH chay; no thay doi tuong lua Windows. Khong sua file hosts.
#>
param([switch]$Remove)

$name = 'ERP LAN test (HTTP 80)'
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { Write-Error 'Hay mo PowerShell bang Run as administrator.'; exit 1 }

if ($Remove) {
    Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Write-Host "Da xoa rule '$name'. Cong 80 khong con mo cho mang LAN."
    exit 0
}

if (-not (Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $name -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow -Profile Private | Out-Null
}
Write-Host "Da mo cong 80 (chi mang Private) bang rule '$name'."
Write-Host 'Mang Wi-Fi hien tai:'
Get-NetConnectionProfile | Select-Object Name, NetworkCategory | Format-Table -AutoSize
Write-Host "Neu NetworkCategory la 'Public', doi sang Private (Settings > Network > Wi-Fi > Properties) roi chay lai, hoac rule se khong co tac dung."
Write-Host 'Khi test xong, chay: .\allow-lan-http.ps1 -Remove'
