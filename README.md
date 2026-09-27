# Sky Flat Campaign Manager

[![CI](https://github.com/grm/sky-flat-campaign-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/grm/sky-flat-campaign-manager/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/grm/sky-flat-campaign-manager)](https://github.com/grm/sky-flat-campaign-manager/releases/latest)
[![License: MPL-2.0](https://img.shields.io/badge/license-MPL--2.0-blue.svg)](LICENSE.txt)
[![N.I.N.A. 3.2+](https://img.shields.io/badge/N.I.N.A.-3.2%2B-4C8BF5)](https://nighttime-imaging.eu/)

**Automated evening and morning sky-flat campaigns for N.I.N.A., without a flat panel.**

Sky Flat Campaign Manager (SFCM) turns twilight flats into a persistent, unattended workflow. It decides when the sky is usable, selects the next filter, adapts exposure time as twilight changes, validates each flat against the configured histogram target, and resumes unfinished campaigns on later evenings or mornings.

Only **accepted flats** are saved to disk and NINA Image History. Rejected exposure-search probes are discarded. Campaign state is persisted after every accepted flat, so a crash, power loss, weather interruption, or closed twilight window does not lose progress.

**SQM / weather SkyQuality is optional.** Camera histogram/ADU measurement remains authoritative.

**Author:** Jérémie Klein ([@grm](https://github.com/grm))  
**Repository:** [github.com/grm/sky-flat-campaign-manager](https://github.com/grm/sky-flat-campaign-manager)  
**Download:** [Latest release](https://github.com/grm/sky-flat-campaign-manager/releases/latest) · **Support:** [GitHub Issues](https://github.com/grm/sky-flat-campaign-manager/issues) · **Changelog:** [CHANGELOG.md](CHANGELOG.md)

## What SFCM does

- Runs from a single **Sky Flat Campaign Container** in NINA Advanced Sequencer.
- Persists per-filter progress across nights and resumes exactly where it stopped.
- Supports evening and morning twilight with direction-aware astronomical windows.
- Adapts exposure and filter selection as sky brightness changes.
- Supports per-filter target count, minimum usable count, histogram target/tolerance, gain, offset, binning, exposure bounds, manual order, and priority.
- Can wait when started too early and can **skip a late evening start** to protect science imaging time.
- Exposes blocking lifecycle hooks for Ground Station, Discord notifications, scripts, or any other NINA instruction.
- Works without an SQM; optional SkyQuality data is used only for anticipation.
- Includes dry-run, simulation, diagnostics, campaign reset/invalidate, and reusable sequencer conditions.

## Quick start

1. Download the latest `SkyFlatCampaignManager-<version>.zip` from [GitHub Releases](https://github.com/grm/sky-flat-campaign-manager/releases/latest).
2. Extract it to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Sky Flat Campaign Manager\`.
3. Restart NINA and enable **Sky Flat Campaign Manager** in Plugins.
4. Configure your filters in the plugin options page.
5. Add **Sky Flat Campaign Container** to the Advanced Sequencer, choose `Evening` or `Morning`, and enable **Wait for sky**.

## Compatible NINA versions

- **NINA 3.2.x** (`MinimumApplicationVersion` / NuGet `NINA.Plugin` **3.2.0.9001**)
- Target framework: `net8.0-windows7.0`

## Features

- Advanced Sequencer **Sky Flat Campaign Container** with blocking custom event hooks
- Backward-compatible **Run Sky Flat Campaign** instruction
- Conditions: **Sky Flat Campaign Required**, **Sky Flat Window Available**
- **Reset / Invalidate** campaign instruction
- **Diagnostic** instruction (camera ADU, filter, SQM, paths, sun altitude)
- Adaptive / manual / morning-evening filter strategies
- Atomic JSON campaign state with schema versioning
- Dry-run and simulation modes
- Optional hybrid SQM anticipation via weather `SkyQuality`

## Screenshots

Screenshots of the options page, the Advanced Sequencer container, and a running campaign will be added here. The repository keeps these under `docs/images/` so the same assets can also be used by NINA's plugin metadata.

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
- Default target count / target histogram level (%) / tolerance (% of target)
- Sun safety separation degrees

Per-filter grid: target count, minimum acceptable count, **target histogram level (%)** and **tolerance (% of target)**, gain/offset/binning, min/max exposure, evening/morning order, priority.

Filter lists come from the **active NINA profile filter wheel** — LRGBSHO is not assumed.

## Brightness / histogram model

The acceptance target is a **normalized histogram level** — a percentage of full scale (0–100%), not a raw ADU number:

- **Target histogram level**: e.g. 40% of full scale
- **Tolerance**: a percentage *of the target* (NINA-style), e.g. ±10% of a 40% target accepts 36–44% of full scale
- `maxAdu` is read from the camera's configured bit depth (`ICameraSettings.BitDepth` — 12-bit/4095, 14-bit/16383, 16-bit/65535, etc.), never assumed to be 65535
- Acceptance validates the robust **median** (not the mean); the mean is shown as an extra diagnostic only
- Diagnostics and logs show both forms, e.g. `Measured median histogram level: 39.2% / 25690 ADU`

Existing `TargetAdu`/`AduTolerance` filter configurations are migrated automatically to the normalized fields the first time they're loaded — no manual action needed, and in-progress campaign counts are never reset by this migration. See [`CHANGELOG.md`](CHANGELOG.md) for the exact migration formula.

## Astronomical window behaviour

The sun altitude is classified relative to the resolved Morning/Evening mode as **TooEarly**, **Open**, or **TooLate**:

| Mode | TooEarly (may wait) | Open (flats run) | TooLate (stop immediately, never wait) |
|------|----------------------|-------------------|------------------------------------------|
| Evening | altitude > max | min ≤ altitude ≤ max | altitude < min |
| Morning | altitude < min | min ≤ altitude ≤ max | altitude > max |

`TooLate` is a normal closed-twilight outcome, not a fault — the runner stops immediately regardless of **Allow wait for sky**, because the sun keeps moving in the same direction and the window cannot reopen this session.

For **Evening** sessions there is also a configurable soft latest-start cutoff (default **Sun altitude -10°**). If SFCM first starts after that altitude, it skips the flat run before any mount movement and returns control to the NINA sequence so science imaging is not delayed for a nearly-finished twilight. Disable **Skip late evening start** to use every remaining minute up to the hard window minimum (default -12°). The soft cutoff is checked only at session start; a campaign already running may continue to the hard window limit.

Morning sessions intentionally have no equivalent soft cutoff: there is no science-imaging block to protect after dawn, so SFCM uses every remaining usable minute until the hard morning limit (default Sun altitude -1°).

## Advanced Sequencer examples

The preferred setup is now a single **Sky Flat Campaign Container**. It evaluates campaign
state itself: if all flats are current it fires **Campaign Not Required / Skip** and returns;
otherwise it fires **Campaign Required** with the live number of missing flats and starts the
campaign. An outer IF/loop is therefore optional.

Custom event containers are blocking and run sequentially before SFCM continues:

- Campaign Required / Campaign Not Required (Skip)
- Before Wait / After Wait (one pair per continuous twilight wait episode, not per 5–30 s probe)
- Before Filter / After Filter Complete
- Campaign Completed / Session Incomplete / Error

Each event has an optional message template. Available placeholders include `{remaining}`,
`{required}`, `{accepted}`, `{filter}`, `{filterRemaining}`, `{mode}`, `{exposure}`,
`{adu}`, `{histogram}`, `{sunAltitude}`, `{waitReason}`, `{stopReason}`, and
`{duration}`. Leave the template blank for SFCM's contextual default.

For **Ground Station on NINA 3.2**, drop a Ground Station notification instruction into an event
container and set its message to <code>&#36;&#36;INSTRUCTION_SET&#36;&#36;</code>. Immediately before the event runs, SFCM
sets the event-container name to the fully resolved message, so Ground Station can send live SFCM
values without SFCM depending on Ground Station.

### Evening

1. Add **Sky Flat Campaign Container**
2. Mode=`Evening`, Wait for sky=true
3. Optionally add Ground Station messages to the event containers
4. Continue with night imaging when the container returns (complete or partial success)

### Morning

1. Add **Sky Flat Campaign Container**
2. Mode=`Morning`, Wait for sky=true
3. SFCM may start early and waits adaptively for the usable twilight window

The older **Run Sky Flat Campaign** instruction remains available for existing sequences.

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
