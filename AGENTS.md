# AGENTS.md — Sky Flat Campaign Manager

## Mission

NINA plugin for multi-day automated sky flats without a flat panel. Camera ADU is mandatory; SQM is optional.

## Hard rules

1. **Never invent NINA APIs.** Verify in `isbeorn/nina` or NuGet `NINA.Plugin` 3.2.0.9001.
2. Keep product name in `SkyFlatCampaignManager.Core/PluginIdentity.cs`.
3. Domain logic lives in Core; plugin project is adapters + MEF + WPF only.
4. Do not copy third-party plugin source under incompatible licenses.
5. Keep `AGENTS.md`, `CLAUDE.md`, `ARCHITECTURE.md`, `README.md` synchronized when architecture changes.
6. **Never assume a fixed full-scale ADU (e.g. `65535`).** Read the effective bit depth from the camera/image (`ICameraSettings.BitDepth` / `ImageStatisticsResult.MaxAdu`); `PluginIdentity.LegacyMigrationMaxAdu` (65535) exists only for one-time migration of pre-existing settings, never for live acceptance.
7. **Astronomical window checks must be direction-aware.** Never wait for a window that has already closed and cannot reopen this session (`AstronomicalWindowState.TooLate`) — see `IAstronomicalWindowService.Evaluate` and `ARCHITECTURE.md`.

## Commands

```bash
dotnet restore SkyFlatCampaignManager.sln
dotnet build src/SkyFlatCampaignManager.Core -c Release
dotnet test tests/SkyFlatCampaignManager.UnitTests -c Release
# Windows:
dotnet build SkyFlatCampaignManager.sln -c Release
pwsh ./scripts/package-plugin.ps1
```

## Definition of done

- Core builds; unit tests pass without hardware
- CI on Windows builds plugin + package artifact
- Sequencer instruction/conditions export via MEF
- Campaign persists after each accepted flat
- Works without SQM; SQM disconnect does not fail camera mode
- Cancellation saves state and returns cleanly

## Sensitive NINA APIs

See `docs/INVESTIGATION.md` and `ARCHITECTURE.md`.
