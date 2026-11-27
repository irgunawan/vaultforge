# Build Hermes Vault Manager MSI on Windows (requires WiX Toolset 3.x on PATH)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

dotnet publish -c Release -o publish
candle installer\product.wxs -out product.wixobj
light -ext WixUIExtension -out HermesVaultManager-1.0.0.msi product.wixobj
Write-Host "MSI: $root\HermesVaultManager-1.0.0.msi"
