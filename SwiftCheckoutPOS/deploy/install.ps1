<#
  BP-06: installs or upgrades SwiftCheckoutPOS on the shop PC as a Windows service listening on this PC only.
  Run in an ADMINISTRATOR PowerShell from the folder that contains this script and the published files:
    powershell -ExecutionPolicy Bypass -File .\install.ps1
  Upgrade: make a backup first (Back up now, or SwiftCheckoutPOS.UI.exe --backup), then run the same command.
  Remove the service (data is kept): powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall
  See docs\DEPLOYMENT_GUIDE.md.
#>
param(
  [string]$SourceDir = (Join-Path $PSScriptRoot 'app'),
  [string]$InstallDir = 'C:\Program Files\SwiftCheckoutPOS',
  [string]$DataRoot = 'C:\ProgramData\SwiftCheckoutPOS',
  [string]$SqlInstance = '.\SQLEXPRESS',
  [string]$Database = 'SwiftCheckoutPOS',
  [string]$SqlServiceAccount = 'NT SERVICE\MSSQL$SQLEXPRESS',
  [int]$Port = 5080,
  [switch]$Uninstall
)
$ErrorActionPreference = 'Stop'
$service = 'SwiftCheckoutPOS'
$account = "NT SERVICE\$service"

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw 'Run this script in an Administrator PowerShell.' }

$existing = Get-Service -Name $service -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') { Write-Host 'Stopping service...'; Stop-Service $service -Force; Start-Sleep -Seconds 3 }

if ($Uninstall) {
  if ($existing) { sc.exe delete $service | Out-Null; Write-Host 'Service removed. Program files and all data in' $DataRoot 'were kept.' }
  return
}

if (-not (Test-Path (Join-Path $SourceDir 'SwiftCheckoutPOS.UI.exe'))) { throw "Published files not found in $SourceDir (expected SwiftCheckoutPOS.UI.exe)." }

Write-Host "Copying program to $InstallDir ..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
# /E (not /MIR): files already in the program folder, such as older product images in wwwroot\images\products, are kept.
robocopy $SourceDir $InstallDir /E /NFL /NDL /NJH /NJS /R:2 /W:2 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copy failed (robocopy code $LASTEXITCODE)." }

Write-Host "Preparing data folders in $DataRoot ..."
foreach ($d in 'Config','Backups','Media\products','Logs','Keys','VirtualReceipts') { New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot $d) | Out-Null }
# Only SYSTEM, Administrators and the POS service may use the data root (backups hold customer data and password hashes).
icacls $DataRoot /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' "${account}:(OI)(CI)M" | Out-Null
# The SQL Server service writes .bak files into the backup folder.
icacls (Join-Path $DataRoot 'Backups') /grant "${SqlServiceAccount}:(OI)(CI)M" | Out-Null
# Older images in the program folder must stay readable by the service.
icacls $InstallDir /grant "${account}:(OI)(CI)RX" | Out-Null

$local = Join-Path $DataRoot 'Config\appsettings.Local.json'
if (-not (Test-Path $local)) {
  $settings = [ordered]@{
    ConnectionStrings = [ordered]@{ DbConn = "Server=$SqlInstance;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True" }
    Hosting = [ordered]@{ Port = $Port }
    Printer = [ordered]@{ Mode = 'Virtual' }
  }
  $settings | ConvertTo-Json -Depth 4 | Set-Content -Path $local -Encoding UTF8
  Write-Host "Created $local (edit Printer settings after the printer is tested)."
}

if (-not $existing) {
  Write-Host 'Creating Windows service...'
  $exe = Join-Path $InstallDir 'SwiftCheckoutPOS.UI.exe'
  $sqlService = 'MSSQL$' + ($SqlInstance -replace '^.*\\','')
  sc.exe create $service binPath= "`"$exe`"" start= delayed-auto obj= $account DisplayName= 'SwiftCheckoutPOS' depend= $sqlService | Out-Null
  sc.exe description $service 'SwiftCheckoutPOS till (this PC only)' | Out-Null
  sc.exe failure $service reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
}

$shortcut = Join-Path $env:PUBLIC 'Desktop\SwiftCheckoutPOS.url'
Set-Content -Path $shortcut -Value "[InternetShortcut]`r`nURL=http://localhost:$Port/" -Encoding ASCII

Write-Host ''
Write-Host 'Next steps (first install):'
Write-Host "  1. cd `"$InstallDir`" ; .\SwiftCheckoutPOS.UI.exe --migrate"
Write-Host "  2. sqlcmd -S $SqlInstance -E -i `"$PSScriptRoot\sql-permissions.sql`" -v Database=`"$Database`" AppLogin=`"$account`""
Write-Host "  3. Start-Service $service ; open http://localhost:$Port/ and create the first Admin."
Write-Host 'Upgrade: run step 1 (after a verified backup), then Start-Service.'
