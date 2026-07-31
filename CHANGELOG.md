# Changelog

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
