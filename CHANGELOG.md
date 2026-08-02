# Changelog

## 0.0.5 — 2026-08-03

### Fixed
- **Astronomical window is now direction-aware.** `IAstronomicalWindowService.Evaluate` classifies the sun altitude as `TooEarly` / `Open` / `TooLate` relative to the resolved Morning/Evening mode. Previously the session runner treated "not open yet" and "already closed" identically and would wait up to `MaxWaitMinutes` even when the twilight window had already passed and could not reopen. `TooLate` now stops immediately (`EveningSkyTooDark` / `MorningSkyTooBright`), never waits, and is reported as a normal closed-twilight outcome rather than a fault.
- **Exposure feasibility now uses the unclamped required exposure.** `IFlatExposureEstimator.Estimate(...)` returns `UnclampedExposureSeconds` (the exposure the sky actually requires) alongside `ClampedExposureSeconds` and an `ExposureFeasibility` (`TooShort`/`Feasible`/`TooLong`). Previously both the initial guess and every estimate were clamped into `[MinExposureSeconds, MaxExposureSeconds]` before the feasibility check ran, so "required exposure is outside the configured range" could never be detected. Waiting for a filter to become feasible is now also direction-aware (`ExposureFeasibilityRules.CanImproveByWaiting`): e.g. an evening required-exposure that is already too long means the sky is already too dark for that filter and waiting will never help.
- **All incomplete filters are evaluated before giving up.** The runner used to select one filter via the configured strategy and could end the session solely because that filter was infeasible, even if another incomplete filter (e.g. a broadband filter late in a narrowband-unfeasible evening) was still feasible. A per-session temporarily-unavailable-filter set also prevents the adaptive strategy from endlessly re-selecting a filter that keeps getting rejected.
- **Wait timers are now scoped per reason.** A single shared `waitStarted` timestamp meant `MaxWaitMinutes` effectively measured time since session start rather than continuous waiting for one specific reason. Astronomical-window waits and filter-feasibility waits now use independent, resettable timers that reset when the window opens, a feasible filter is found, or a frame is captured.
- **`LastSunAltitudeDegrees` now survives a restart.** It is persisted atomically inside `AcceptFlatAsync`'s single save (together with accepted count, exposure, and measured level) instead of being set on the in-memory object *after* the campaign was already saved. `ClosestToOptimalWindowStrategy` learning is no longer silently lost on a plugin/NINA restart.

### Changed
- **Normalized histogram brightness model.** Per-filter acceptance targets are now `TargetHistogramFraction` (0.0–1.0, shown as 0–100% in the UI — "Target histogram level") and `TargetToleranceFraction` (a percentage **of the target**, NINA-style — e.g. target 40% ± 10% of target ⇒ accepted range 36–44% of full scale). `maxAdu` is derived from the actual camera bit depth (`ICameraSettings.BitDepth`, verified — see `docs/INVESTIGATION.md`) instead of being assumed to be `65535`; 12-bit (4095), 14-bit (16383), and other depths are all supported. The acceptance algorithm still validates the robust **median** (not the mean); the log/UI label was corrected accordingly ("Measured median histogram level"), and the arithmetic mean is now reported as an additional, clearly-labelled diagnostic value only.
- Raw ADU values are still shown alongside the normalized percentage in diagnostics and logs (e.g. `Measured median: 39.2% / 25690 ADU`).
- `FilterCampaignSettings.MinimumAcceptableCount` now has defined semantics: enough accepted flats for a filter/campaign to be considered usable (`FilterCompletionStatus.MinimumReached`) without being fully done. Only reaching `TargetCount` marks a filter/campaign `Complete`; status is exposed as `Incomplete` / `MinimumReached` / `Complete` on `FilterProgress.Status` and `CampaignState.CompletionStatus`.
- Removed unused `AstronomicalWindowOptions` fields that had no runtime effect (`MaxDurationMinutes`, `MaxWaitMinutes`, `SafetyMarginMinutes`, `EarliestLocalTime`, `LatestLocalTime`, previously duplicated at the request level). `SkyFlatSessionRequest.MaxDurationMinutes`/`MaxWaitMinutes` are now the single source of truth for session time budgets; `AstronomicalWindowOptions` only defines *where* the window is (min/max sun altitude).
- Options UI: per-filter grid now shows **Target %** / **Tol % of target** (with a read-only `≈ADU @16-bit` preview column) instead of raw **ADU** / **±ADU** columns; global defaults are **Default target histogram level (%)** / **Default tolerance (% of target)**.

### Migration
- Existing `TargetAdu`/`AduTolerance` filter configuration is migrated automatically on first load (schema 1→2): `TargetHistogramFraction = TargetAdu / 65535`, `TargetToleranceFraction = AduTolerance / TargetAdu`. The stock defaults (`TargetAdu=25000`, `AduTolerance=2500`) migrate to `TargetHistogramFraction≈0.3815` (38.15%) and `TargetToleranceFraction=0.10` (10%), preserving prior acceptance behaviour on a 16-bit sensor. Migration only touches the filter *configuration* document — accepted/rejected campaign progress counts are never reset or invalidated by it.
- Global plugin settings `DefaultTargetAdu`/`DefaultAduTolerance` are superseded by `DefaultTargetHistogramPercent`/`DefaultTargetTolerancePercent` (defaults 38.15 / 10, matching the same legacy behaviour); the legacy settings are no longer read by new code.

### Added
- Unit tests: direction-aware window classification (`AstronomicalWindowTests`), direction-aware exposure feasibility (`ExposureFeasibilityDirectionTests`), histogram normalization / bit-depth / migration (`HistogramMigrationTests`), plus updated exposure/validator and session/SQM tests for the new fraction-based settings.

## 0.0.4 — 2026-08-01

### Added
- Per-filter campaign configuration in Options (enabled, count, ADU, gain, offset, binning, exposure bounds, evening/morning order)
- Settings persisted per NINA profile as `filter-config.<profileId>.json` under the SFCM state directory
- New filters seeded from Flat Wizard gain/offset/binning/exposure when available

## 0.0.3 — 2026-07-31

### Fixed
- Sequencer UI: CheckBox labels were invisible under NINA's toggle-switch style on Run / Diagnostic / Campaign Required

## 0.0.2 — 2026-07-31

### Fixed
- Options page: CheckBox labels were invisible under NINA's toggle-switch style; labels are now explicit TextBlocks
- Options page: show the effective campaign state directory when the field is left blank

## 0.0.1 — 2026-07-31

### Added
- Initial Sky Flat Campaign Manager plugin for NINA 3.2
- Advanced Sequencer instruction `Run Sky Flat Campaign`
- Conditions `Sky Flat Campaign Required` and `Sky Flat Window Available`
- Instructions `Reset or Invalidate Sky Flat Campaign` and `Sky Flat Diagnostic`
- JSON atomic campaign persistence with schema versioning
- Camera-only ADU exposure search; optional weather SkyQuality (SQM) hybrid mode
- Filter selection strategies (manual, recommended, adaptive, exposure-based, priority)
- Simulation / dry-run support and unit tests
- GitHub Actions CI + Release packaging
