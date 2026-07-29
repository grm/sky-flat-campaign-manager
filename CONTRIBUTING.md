# Contributing

1. Read `docs/INVESTIGATION.md` and `AGENTS.md`.
2. Do **not** invent NINA APIs — verify in https://github.com/isbeorn/nina or NuGet packages.
3. Keep domain logic in `SkyFlatCampaignManager.Core` (testable without NINA/WPF).
4. Prefer adapters for NINA mediators.
5. Run unit tests before opening a PR.
6. Follow MPL-2.0; do not copy incompatible third-party plugin code.

## Local commands

```bash
dotnet restore SkyFlatCampaignManager.sln
dotnet build src/SkyFlatCampaignManager.Core/SkyFlatCampaignManager.Core.csproj -c Release
dotnet test tests/SkyFlatCampaignManager.UnitTests/SkyFlatCampaignManager.UnitTests.csproj -c Release
```

On Windows (full plugin):

```powershell
dotnet build SkyFlatCampaignManager.sln -c Release
./scripts/package-plugin.ps1
```
