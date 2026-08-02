# Architecture

## Projects

| Project | TFM | Role |
|---------|-----|------|
| `SkyFlatCampaignManager.Core` | `net8.0` | Domain: campaigns, ADU, strategies, session runner, simulation |
| `SkyFlatCampaignManager` | `net8.0-windows7.0` | NINA MEF plugin, WPF options, sequencer entities, adapters |
| `*.UnitTests` | `net8.0` | Domain tests (no hardware) |
| `*.IntegrationTests` | `net8.0-windows7.0` | Light smoke against plugin identity / Windows TFM |

## Layers

```
Sequencer Instructions/Conditions (MEF)
        │
        ▼
PluginServiceFactory + NINA Adapters
        │
        ▼
SkyFlatSessionRunner (state machine)
        │
   ┌────┼────┬──────────┬────────────┐
   ▼    ▼    ▼          ▼            ▼
Campaign  Filters  Brightness  Acquisition  Astronomy
Repository Strategy Providers  Estimator/   Window/Sun
                               Validator
```

## Session states

`Idle → CheckingCampaign → CheckingEquipment → WaitingForAstronomicalWindow → SelectingFilter → ChangingFilter → EstimatingExposure → Capturing → Validating → Persisting → … → Completed | StoppedByWindow | StoppedByTimeout | Cancelled | Faulted`

## Direction-aware astronomical window

`IAstronomicalWindowService.Evaluate(mode, sunAltitude, window)` classifies the current sun altitude into `TooEarly` / `Open` / `TooLate` **relative to the resolved Morning/Evening mode** (never `Automatic` — the runner resolves that first via `ResolveMode`). Waiting (`AllowWaitForSky` + `MaxWaitMinutes`) is only meaningful for `TooEarly`, because the sky is moving toward the window. `TooLate` means the sun has already crossed the window in the direction it keeps moving, so the runner stops immediately with `EveningSkyTooDark` / `MorningSkyTooBright` — this is a normal closed-twilight outcome, not a fault, and it is never worth waiting for. See `SessionReasons.cs` for the full set of stable wait/stop reason strings.

## Exposure feasibility

`IFlatExposureEstimator.Estimate(...)` returns both `UnclampedExposureSeconds` (the exposure the sky actually requires) and `ClampedExposureSeconds` (safe to command the camera with), plus an `ExposureFeasibility` (`TooShort` / `Feasible` / `TooLong`) computed from the **unclamped** value. `ExposureFeasibilityRules.CanImproveByWaiting(mode, feasibility)` encodes the direction-aware rule (e.g. evening + `TooShort` ⇒ sky still brightening the exposure need, wait may help; evening + `TooLong` ⇒ already too dark for this filter, waiting never helps). Every incomplete, enabled filter not yet parked in the session's `unavailableFilters` set is evaluated for feasibility each loop iteration before the runner gives up — not just the filter the strategy would prefer — so a still-feasible broadband filter is never abandoned just because a narrowband filter's exposure need has drifted out of range.

## Wait timers

Astronomical-window waits and filter-feasibility waits use independent `WaitTracker` instances scoped to the session. Each tracks continuous elapsed time for **one wait reason** and resets to zero the moment that reason no longer applies (window opens, a feasible filter is found, a frame is captured). `MaxWaitMinutes` therefore always means "how long to keep waiting for the current reason", never "elapsed time since the session started".

## Normalized histogram / brightness model

Acceptance targets are stored as `TargetHistogramFraction` (0.0–1.0 of full scale) and `TargetToleranceFraction` (fraction **of the target**, NINA-style — not of full scale). `maxAdu` is never assumed to be `65535`; it is read from the camera/image bit depth for every real capture (see `NinaCameraAcquisitionService.ResolveMaxAdu()` and `docs/INVESTIGATION.md` §4 for the verified `ICameraSettings.BitDepth` API) and stamped onto `ImageStatisticsResult.MaxAdu`. The validator (`DefaultFlatFrameValidator`) accepts/rejects on the **median** (not the mean); the mean is reported only as an additional diagnostic field (`MeasuredMeanAdu`). Legacy `TargetAdu`/`AduTolerance` settings are migrated to the fraction fields on load (`FilterCampaignConfigMigrator`, schema 1→2) using the historical `65535` full-scale assumption — that legacy constant (`PluginIdentity.LegacyMigrationMaxAdu`) is used **only** for one-time migration of pre-existing settings, never for live validation.

## Minimum acceptable count vs. target count

`FilterCampaignSettings.TargetCount` is the desired number of flats; `MinimumAcceptableCount` is a lower usability threshold. `FilterProgress.Status` / `CampaignState.CompletionStatus` expose `Incomplete` / `MinimumReached` / `Complete`. Only reaching `TargetCount` everywhere marks the multi-day campaign `Completed` — `MinimumReached` is informational and never short-circuits the campaign.

## Partial success

When the astronomical window closes or max duration is hit with remaining flats, the instruction **completes successfully** after atomic persist. The campaign stays `InProgress` for the next evening/morning.

## NINA APIs (verified)

- `IImagingMediator.CaptureImage` / `PrepareImage`
- `IImageSaveMediator.Enqueue`
- `CaptureSequence.ImageTypes.FLAT`
- `IImageStatistics.Median` / `Mean` / `StDev` / `Max` / `Min`
- `ICameraSettings.BitDepth` (effective full-scale ADU; see `docs/INVESTIGATION.md` §4)
- `AstroUtil.GetSunAltitude`
- `IWeatherDataMediator` → `WeatherDataInfo.SkyQuality` (optional SQM)
- `SwitchFilter` sequence item for filter changes
- `ITelescopeMediator.SlewToTopocentricCoordinates` / `SetTrackingEnabled`

## Persistence

JSON file via temp + `File.Replace` + `.bak`. UTC timestamps. `schemaVersion` + migrator. `AcceptFlatAsync` persists accepted count, exposure, measured histogram level/ADU, **and** sun altitude in a single atomic save so `ClosestToOptimalWindowStrategy` learning survives a plugin/NINA restart.

## Security

Sun separation limit, no silent safety bypass, dry-run mode, optional SQM never required.
