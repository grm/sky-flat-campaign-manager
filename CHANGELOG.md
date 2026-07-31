# Changelog

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
