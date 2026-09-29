using System.Reflection;
using System.Runtime.InteropServices;
using SkyFlatCampaignManager.Core;

[assembly: Guid("60fa0ecc-a71d-49a9-9890-274d3d5ff1d8")]
[assembly: AssemblyVersion("0.0.11.0")]
[assembly: AssemblyFileVersion("0.0.11.0")]
[assembly: AssemblyTitle("Sky Flat Campaign Manager")]
[assembly: AssemblyDescription("Automates evening and morning sky-flat campaigns in NINA with adaptive twilight timing, persistent progress, per-filter exposure control, and Advanced Sequencer integration.")]
[assembly: AssemblyCompany("Jérémie Klein (@grm)")]
[assembly: AssemblyProduct("Sky Flat Campaign Manager")]
[assembly: AssemblyCopyright("Copyright © 2026 Jérémie Klein and contributors")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/grm/sky-flat-campaign-manager")]
[assembly: AssemblyMetadata("Homepage", "https://github.com/grm/sky-flat-campaign-manager")]
[assembly: AssemblyMetadata("Tags", "Flats,Sky Flats,Sequencer,Automation,Calibration,Twilight")]
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/grm/sky-flat-campaign-manager/blob/main/CHANGELOG.md")]
[assembly: AssemblyMetadata("FeaturedImageURL", "https://raw.githubusercontent.com/grm/sky-flat-campaign-manager/main/docs/images/options-page.png")]
[assembly: AssemblyMetadata("ScreenshotURL", "https://raw.githubusercontent.com/grm/sky-flat-campaign-manager/main/docs/images/container-events.png")]
[assembly: AssemblyMetadata("AltScreenshotURL", "https://raw.githubusercontent.com/grm/sky-flat-campaign-manager/main/docs/images/container-advanced.png")]
[assembly: AssemblyMetadata("LongDescription", @"Sky Flat Campaign Manager (SFCM) automates sky-flat acquisition in N.I.N.A. for observatories that do not use a flat panel.

## Highlights
* Runs evening and morning sky-flat campaigns from the Advanced Sequencer.
* Tracks accepted flats per filter across multiple sessions and resumes incomplete campaigns automatically.
* Uses the imaging camera's measured histogram/ADU as the authoritative brightness signal; SQM/weather SkyQuality is optional.
* Adapts exposure time and filter selection to the changing twilight.
* Supports per-filter target count, histogram target/tolerance, gain, offset, binning, exposure limits, manual order, and priority.
* Waits for the usable twilight window when started early, and skips cleanly when the evening session starts too late.
* Saves only accepted flats; rejected exposure-search probes are discarded instead of polluting NINA Image History.
* Provides blocking lifecycle event containers for notifications, scripts, Ground Station, or other NINA instructions.
* Persists campaign state after every accepted flat so an interrupted session can continue later.

## Advanced Sequencer
The recommended entry point is **Sky Flat Campaign Container**. It evaluates whether flats are required, manages the twilight window, performs the flat run, and returns control to the surrounding sequence when complete or when the usable window has closed.

## Project links
* [Source code](https://github.com/grm/sky-flat-campaign-manager)
* [Releases](https://github.com/grm/sky-flat-campaign-manager/releases)
* [Change log](https://github.com/grm/sky-flat-campaign-manager/blob/main/CHANGELOG.md)
* [Issues / support](https://github.com/grm/sky-flat-campaign-manager/issues)

Licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).")]
[assembly: ComVisible(false)]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
