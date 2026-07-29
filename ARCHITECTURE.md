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

`Idle → CheckingCampaign → CheckingEquipment → WaitingForAstronomicalWindow → MeasuringSky → SelectingFilter → ChangingFilter → EstimatingExposure → Capturing → Validating → Persisting → … → Completed | StoppedByWindow | StoppedByTimeout | Cancelled | Faulted`

## Partial success

When the astronomical window closes or max duration is hit with remaining flats, the instruction **completes successfully** after atomic persist. The campaign stays `InProgress` for the next evening/morning.

## NINA APIs (verified)

- `IImagingMediator.CaptureImage` / `PrepareImage`
- `IImageSaveMediator.Enqueue`
- `CaptureSequence.ImageTypes.FLAT`
- `IImageStatistics.Median`
- `AstroUtil.GetSunAltitude`
- `IWeatherDataMediator` → `WeatherDataInfo.SkyQuality` (optional SQM)
- `SwitchFilter` sequence item for filter changes
- `ITelescopeMediator.SlewToTopocentricCoordinates` / `SetTrackingEnabled`

## Persistence

JSON file via temp + `File.Replace` + `.bak`. UTC timestamps. `schemaVersion` + migrator.

## Security

Sun separation limit, no silent safety bypass, dry-run mode, optional SQM never required.
