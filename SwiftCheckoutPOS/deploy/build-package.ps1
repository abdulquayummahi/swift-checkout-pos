<#
  BP-06: builds the offline install package on the developer PC (needs the .NET 10 SDK and internet for the first restore).
    powershell -ExecutionPolicy Bypass -File deploy\build-package.ps1
  Output: package\SwiftCheckoutPOS-<date>\ (app\, install.ps1, sql-permissions.sql, docs). Copy that folder to the shop PC on USB.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$out = Join-Path $root "package\SwiftCheckoutPOS-$stamp"
dotnet publish (Join-Path $root 'SwiftCheckoutPOS.UI\SwiftCheckoutPOS.UI.csproj') -p:PublishProfile=ShopPC -o (Join-Path $out 'app')
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
# Never ship developer settings: the shop PC uses C:\ProgramData\SwiftCheckoutPOS\Config\appsettings.Local.json.
Remove-Item (Join-Path $out 'app\appsettings.Development.json') -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot 'install.ps1'), (Join-Path $PSScriptRoot 'sql-permissions.sql') $out
New-Item -ItemType Directory -Force (Join-Path $out 'docs') | Out-Null
foreach ($d in 'DEPLOYMENT_GUIDE.md','DATA_RECOVERY_PLAN.md') { Copy-Item (Join-Path $root "docs\$d") (Join-Path $out 'docs') -ErrorAction SilentlyContinue }
Write-Host "Package ready: $out"
