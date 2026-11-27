# Build VaultForge MSI on Windows (requires WiX Toolset 3.x on PATH)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

dotnet publish -c Release -o publish
candle installer\product.wxs -out product.wixobj
light -ext WixUIExtension -out VaultForge-1.0.5.msi product.wixobj
Write-Host "MSI: $root\VaultForge-1.0.5.msi"
