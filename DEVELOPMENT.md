# Development

## Prerequisites

- Windows 10/11 x64 for full plugin + WPF builds
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (matches `NINA.Plugin` 3.2.0.9001 TFM `net8.0-windows7.0`)
- Visual Studio 2022 (17.x) with .NET desktop development workload **or** `dotnet` CLI
- NINA 3.2.x installed for live debugging

macOS/Linux can build and test **Core + UnitTests** only (no WPF plugin).

## Restore / build / test

```bash
dotnet restore SkyFlatCampaignManager.sln
dotnet build src/SkyFlatCampaignManager.Core -c Release
dotnet test tests/SkyFlatCampaignManager.UnitTests -c Release
```

Windows full solution:

```powershell
dotnet build SkyFlatCampaignManager.sln -c Release
dotnet test SkyFlatCampaignManager.sln -c Release
```

## Debug in NINA

1. Build `SkyFlatCampaignManager` Release/Debug.
2. Copy `SkyFlatCampaignManager.dll` and `SkyFlatCampaignManager.Core.dll` to  
   `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Sky Flat Campaign Manager\`
3. Start NINA and enable the plugin.
4. Attach Visual Studio debugger to `NINA.exe`.

Optional post-build copy can be added locally; CI packages a ZIP instead.

## Packaging

```powershell
./scripts/package-plugin.ps1 -Version 1.0.0.0
./scripts/verify-package.ps1 -ZipPath artifacts/package/SkyFlatCampaignManager-1.0.0.0.zip
```

## Release

Push a tag `v1.2.3` — `.github/workflows/release.yml` builds, tests, packages, and publishes a GitHub Release.
