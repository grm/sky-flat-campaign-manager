# Sky Flat Campaign Manager

NINA plugin that automates **multi-evening and multi-morning sky flat campaigns** without a flat panel.

Progress is persisted after every accepted flat so a crash, power loss, or closed twilight window can resume the next session. A completed campaign remains valid for a configurable period (default **60 days**) and can be invalidated manually after optical changes.

**SQM / weather SkyQuality is optional.** The default and authoritative path is camera ADU analysis.

## Compatible NINA versions

- **NINA 3.2.x** (`MinimumApplicationVersion` / NuGet `NINA.Plugin` **3.2.0.9001**)
- Target framework: `net8.0-windows7.0`

## Features

- Advanced Sequencer instruction **Run Sky Flat Campaign**
- Conditions: **Sky Flat Campaign Required**, **Sky Flat Window Available**
- **Reset / Invalidate** campaign instruction
- **Diagnostic** instruction (camera ADU, filter, SQM, paths, sun altitude)
- Adaptive / manual / morning-evening filter strategies
- Atomic JSON campaign state with schema versioning
- Dry-run and simulation modes
- Optional hybrid SQM anticipation via weather `SkyQuality`

## Installation

### From GitHub Releases

1. Download `SkyFlatCampaignManager-<version>.zip`
2. Extract into `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Sky Flat Campaign Manager\`
3. Restart NINA → Plugins → enable **Sky Flat Campaign Manager**

### From GitHub Actions (CI artifact)

1. Open the successful CI run
2. Download artifact `SkyFlatCampaignManager-<run>-<commit>`
3. Install as above

### Manual build (Windows)

```powershell
dotnet build SkyFlatCampaignManager.sln -c Release
./scripts/package-plugin.ps1 -Version 1.0.0.0
```

## Configuration

Plugin options page (Plugins tab):

- Enable / dry-run / detailed logging
- Campaign name, validity days, auto-restart when expired
- State directory (default `%LOCALAPPDATA%\NINA\SFCM`)
- Default target count / ADU / tolerance
- Sun safety separation degrees

Filter lists come from the **active NINA profile filter wheel** — LRGBSHO is not assumed.

## Advanced Sequencer examples

### Evening

1. Wait for sun altitude / dusk (built-in NINA utility) — optional
2. Condition loop: **Sky Flat Campaign Required**
3. **Run Sky Flat Campaign** — Mode=`Evening`, Allow wait=true, Use SQM=false

### Morning

1. **Run Sky Flat Campaign** — Mode=`Morning`

### Multi-day

Leave the instruction in both dusk and dawn sequences. Incomplete filters resume automatically from JSON state.

### After optical work

Insert **Reset or Invalidate Sky Flat Campaign** with Action=`Invalidate` and a reason.

## Without SQM

Leave **Use SQM** unchecked. All feasibility and acceptance decisions use camera frames.

## With SQM

Enable **Use SQM**. Weather equipment `SkyQuality` is used for anticipation only. Stale/disconnected SQM falls back to camera without failing the campaign.

## Limits

- Full plugin build requires Windows (WPF)
- Pointing near the Sun is refused by a configurable angular safety limit
- First version uses robust median/percentiles (not full star detection)
- Official NINA plugin repository listing is a separate publish step (ZIP/manifest ready)

## Recovery

- State files: `%LOCALAPPDATA%\NINA\SFCM\*.campaign.json` (+ `.bak`)
- Corrupted JSON falls back to `.bak`
- Use diagnostic instruction to verify camera/path/sun
- Use reset instruction to clear a filter or force a new campaign

## Docs

- [Investigation](docs/INVESTIGATION.md)
- [Architecture](ARCHITECTURE.md)
- [Development](DEVELOPMENT.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
- [Agents](AGENTS.md)

## License

MPL-2.0
