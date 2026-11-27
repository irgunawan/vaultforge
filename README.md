# VaultForge

A lightweight Windows 10/11 GUI for the Hermes local vault. It runs through a
PowerShell bridge and calls the same `agent.vault_store` API that
`hermes vault add` uses, so entries land in the real Hermes vault.

## Features

- Add login entries: label, origin, identifier type, identifier, password, optional authenticator key.
- List vault items (metadata only, passwords never shown).
- Delete an item by handle.
- Password is passed through a temporary JSON file and is never placed on the command line.
- Window controls: fullscreen, 1/16 screen size, minimize, resize.
- Over-the-air (OTA) updates: downloads the released `.msi`, verifies its size
  and SHA-256 checksum, then silently installs it and restarts the app. A manual
  download button and a dark/light theme toggle sit beside it in the banner.
- Native Windows 10 look by default, anvil application icon, optional dark theme.
- Live system indicators (CPU, RAM, HDD C:, CPU temperature) and three gauges of
  the heaviest processes while Hermes works.

## Requirements

- Windows 10 1903+ or Windows 11
- .NET Framework 4.8 (built in on supported Windows versions)
- Hermes installed with Python, and the Python environment that can `import agent.vault_store`
- Windows PowerShell 5.1 (built in)

The app auto-detects `hermes.exe` on `PATH` and uses the sibling `python.exe`.
If auto-detection fails, open **⚙ Settings** and point it at the right
`hermes.exe`, `python.exe`, and Hermes home directory.

## Build

```powershell
dotnet build -c Release
dotnet publish -c Release -o publish
```

The MSI is built from `installer/product.wxs`. On Windows, use WiX Toolset 3.x:

```powershell
candle installer\product.wxs
light -ext WixUIExtension -o VaultForge-1.0.4.msi product.wixobj
```

The checked-in `installer/product.wxs` was also built on Linux with `wixl`.
Publish `VaultForge-1.0.4.msi.sha256` beside the MSI on GitHub Releases so OTA
can verify the download before installing.

## Security

- The vault key and encrypted payload remain in the Hermes profile; this app does not copy them.
- Passwords are not logged or shown again.
- The PowerShell bridge only forwards JSON to the Python helper.
