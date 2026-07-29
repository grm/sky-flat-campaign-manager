# Investigation — Sky Flat Campaign Manager

**Date:** 2026-07-29  
**Status:** Complete (Phase 1)  
**Decision freeze for Phase 2+**

## 1. Official sources examined

| Source | Location | Notes |
|--------|----------|-------|
| Plugin template | https://github.com/isbeorn/nina.plugin.template | MEF exports, AssemblyInfo metadata, Options DataTemplate naming, sequencer entity patterns |
| NINA source | https://github.com/isbeorn/nina (`develop`) | `TakeExposure`, `CaptureSequence`, `SunAltitudeCondition`, `AstroUtil`, `WeatherDataInfo`, `IImagingMediator` |
| Plugin manifests | https://github.com/isbeorn/nina.plugin.manifests | ZIP archive + `CreateManifest.ps1`, checksum, installer URL |
| NuGet | `NINA.Plugin` **3.2.0.9001** | Stable package as of investigation; TFM `net8.0-windows7.0` |
| Reference plugin (API usage only, no code copy) | Target Scheduler (GPL-compatible review of public API call patterns) | Confirmed capture/save/filter-switch patterns and plugin install folder |

Local clones kept under `_ref/` (gitignored).

## 2. Target platform

| Item | Value | Rationale |
|------|-------|-----------|
| NINA stable target | **3.2.x** (`MinimumApplicationVersion` = `3.2.0.9001`) | Matches published stable NuGet `NINA.Plugin` 3.2.0.9001 (2025-11-12) |
| .NET TFM | **`net8.0-windows7.0`** | Exact TFM of NINA.Plugin 3.2.0.9001 |
| UI | WPF (`UseWPF=true`) | Required by NINA plugin options + sequencer DataTemplates |
| Platform | Windows x64 | WPF + NINA runtime |
| Plugin folder | `%LOCALAPPDATA%\NINA\Plugins\3.0.0\<AssemblyTitle>\` | Confirmed by Target Scheduler post-build and NINA PluginLoader docs |
| Nightly / .NET 10 | **Out of scope for v1** | `develop` mentions .NET 10 / 3.3 nightlies; we pin to stable 3.2 |

## 3. Required NuGet packages

Primary reference (pulls the rest transitively):

- `NINA.Plugin` **3.2.0.9001**

Transitive packages of interest (do not invent wrappers around missing types):

- `NINA.Sequencer`, `NINA.Equipment`, `NINA.Image`, `NINA.Astrometry`, `NINA.Core`, `NINA.Profile`, `NINA.WPF.Base`, `NINA.CustomControlLibrary`, `NINA.PlateSolving`

Test packages:

- `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `Moq` / `NSubstitute`

## 4. Real NINA interfaces used (verified in source / NuGet)

### MEF exports

| Interface | Base class | Purpose |
|-----------|------------|---------|
| `IPluginManifest` | `PluginBase` | Plugin metadata + options DataContext |
| `ISequenceItem` | `SequenceItem` | Instructions |
| `ISequenceCondition` | `SequenceCondition` | Conditions |
| `ResourceDictionary` | WPF RD with `[Export]` | Options + entity templates |

### Constructor-injected mediators (from official template list + source)

| Interface | Use in this plugin |
|-----------|--------------------|
| `IProfileService` | Filters, observer location, file paths, profile id |
| `ICameraMediator` | Connection / camera info |
| `IFilterWheelMediator` | Filter switch |
| `ITelescopeMediator` | Optional pointing / tracking |
| `IImagingMediator` | `CaptureImage`, `PrepareImage` |
| `IImageSaveMediator` | `Enqueue` + FITS keyword hooks |
| `IImageHistoryVM` | Optional history for lights only; flats may skip |
| `IApplicationStatusMediator` | Status bar progress |
| `IWeatherDataMediator` | Optional SQM via `WeatherDataInfo.SkyQuality` (mag/arcsec²) |
| `INighttimeCalculator` | Dusk/dawn reference times |
| `IOptionsVM` | Optional image pattern registration |
| `ISafetyMonitorMediator` | Safety gate (do not bypass) |

### Imaging / ADU

- `CaptureSequence` + `CaptureSequence.ImageTypes.FLAT`
- `IExposureData.ToImageData(...)` then `IImageData.Statistics` → `IImageStatistics.Median` / `Mean` / `StDev` / `Max` / `Min`
- ROI / robust percentiles: computed in our Core from raw pixel buffers when available; otherwise use NINA statistics as primary median

### Astronomy

- `AstroUtil.GetSunAltitude(DateTime, ObserverInfo)` — **verified**
- `AstroUtil.GetSunPosition(...)` for sun-avoidance pointing
- Do **not** invent alternate solar models when this API is available

### Persistence of plugin settings

- Assembly `Settings` (user-scoped) for global defaults
- `PluginOptionsAccessor` (profile-scoped) for per-profile options — pattern from official template

### Packaging

- Build DLL (+ allowed satellite deps)
- ZIP via `CreateManifest.ps1 -createArchive -includeAll`
- Manifest JSON with checksum; DLL must not be rebuilt after checksum
- Manual install: copy into `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Sky Flat Campaign Manager\`

## 5. Sequencer semantics — partial success

Verified behaviour of `SequenceItem.Execute`:

- Completing the `Task` without throwing → instruction **succeeded** for the sequencer
- `OperationCanceledException` / cancellation token → cancelled
- Other exceptions → failed / fault handling

**Decision:** When the sky window closes or max duration is reached with an incomplete campaign, **return success** after atomic persist. The campaign remains `InProgress`. This is the NINA-compatible meaning of “partial success” — not a failure.

## 6. SQM

NINA does **not** expose a dedicated `ISqmMediator`. Sky quality is part of weather equipment:

- `IWeatherDataMediator.GetInfo()` → `WeatherDataInfo.SkyQuality` (magnitudes per square arc second)
- Also `SkyBrightness` (Lux)

**Decision:** `SqmSkyBrightnessProvider` adapts weather `SkyQuality`. Fully optional. Camera provider is default and always authoritative for flat acceptance.

## 7. Architecture decisions

1. **`SkyFlatCampaignManager.Core`** (`net8.0`, no WPF/NINA) — domain, persistence, strategies, ADU math, state machine, simulator. Unit-testable on any OS.
2. **`SkyFlatCampaignManager`** (`net8.0-windows7.0`) — MEF plugin, WPF, NINA adapters only.
3. Centralized product name in `PluginIdentity` / `PluginConstants`.
4. All filesystem I/O behind `IFileSystem`; clock behind `IClock`.
5. Atomic JSON campaign state: temp → flush → replace + `.bak`.
6. No Discord / webhook dependency in v1 core; `INotificationService` with NINA notification adapter.
7. Dry-run / simulation modes live in Core + adapter stubs.
8. **License:** MPL-2.0 (aligned with NINA). Do not copy Target Scheduler or other third-party plugin source.

## 8. Risks

| Risk | Mitigation |
|------|------------|
| WPF cannot build on macOS agents | Core + UnitTests build everywhere; Plugin + IntegrationTests + packaging on `windows-latest` |
| NINA 3.3 / .NET 10 break | Pin `MinimumApplicationVersion` and NuGet; document upgrade path |
| Pixel buffer access for percentiles varies | Prefer `IImageStatistics.Median`; ROI percentiles via adapter with fallback |
| Pointing near Sun | Hard angular safety limit; never bypass mount/safety monitors |
| Flat panel plugins differ | We do **not** require flat device; sky only |
| Schema upgrades | `schemaVersion` + explicit migrations; never silent wipe |

## 9. Non-goals for v1

- Official NINA plugin-repo listing PR (package + manifest artifacts ready)
- Full star detection for flats
- Discord / MQTT
- Dome orchestration beyond respecting existing safety
- .NET 10 / NINA 3.3 nightlies

## 10. Phase gate

Phase 1 complete. Proceed to Phase 2 (compilable skeleton) with the decisions above.
