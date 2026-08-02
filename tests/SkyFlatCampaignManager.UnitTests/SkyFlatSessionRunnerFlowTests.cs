using FluentAssertions;
using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Astronomy;
using SkyFlatCampaignManager.Core.Brightness;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Equipment;
using SkyFlatCampaignManager.Core.Filters;
using SkyFlatCampaignManager.Core.Notifications;
using SkyFlatCampaignManager.Core.Simulation;
using SkyFlatCampaignManager.Core.Utilities;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

/// <summary>
/// End-to-end <see cref="SkyFlatSessionRunner"/> behaviour: alternative-feasible-filter
/// selection, partial-success progress preservation, wait-timer reset/scoping, and cancellation
/// mid-wait. These use scripted sun-altitude providers (and, where a real wait must elapse, a
/// small number of real <c>PollInterval</c> ticks) instead of driving many iterations through
/// real wall-clock time, matching the approach used elsewhere in this suite.
/// </summary>
public class SkyFlatSessionRunnerFlowTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 8, 1, 19, 0, 0, DateTimeKind.Utc);
        public DateTime LocalNow => UtcNow;
    }

    private sealed class MemoryFs : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
        public bool FileExists(string path) => _files.ContainsKey(path);
        public bool DirectoryExists(string path) => true;
        public void CreateDirectory(string path) { }
        public string ReadAllText(string path) => _files[path];
        public void WriteAllText(string path, string contents) => _files[path] = contents;
        public void WriteAllBytes(string path, byte[] contents) => _files[path] = Convert.ToBase64String(contents);
        public byte[] ReadAllBytes(string path) => Convert.FromBase64String(_files[path]);
        public void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName)
        {
            if (destinationBackupFileName is not null && _files.TryGetValue(destinationFileName, out var old))
                _files[destinationBackupFileName] = old;
            _files[destinationFileName] = _files[sourceFileName];
            _files.Remove(sourceFileName);
        }
        public void Delete(string path) => _files.Remove(path);
        public void Copy(string source, string destination, bool overwrite) => _files[destination] = _files[source];
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => _files.Keys;
    }

    /// <summary>Returns a scripted altitude per call and optionally advances a FakeClock's UtcNow as a side effect, so tests can control elapsed wait time deterministically without real delays.</summary>
    private sealed class ScriptedSunAltitudeProvider : ISunAltitudeProvider
    {
        private readonly FakeClock _clock;
        private readonly Func<int, (double Altitude, TimeSpan Advance)> _script;
        private int _calls;

        public ScriptedSunAltitudeProvider(FakeClock clock, Func<int, (double, TimeSpan)> script)
        {
            _clock = clock;
            _script = script;
        }

        public double GetSunAltitudeDegrees(DateTime utc)
        {
            var (altitude, advance) = _script(_calls++);
            _clock.UtcNow += advance;
            return altitude;
        }
    }

    private static SkyFlatSessionRunner CreateRunner(
        CampaignService campaigns,
        ICameraAcquisitionService camera,
        IFilterWheelService wheel,
        ISunAltitudeProvider sun,
        IClock clock)
        => new(
            campaigns,
            camera,
            wheel,
            new NoOpMountPositioningService(),
            new ProportionalFlatExposureEstimator(),
            new DefaultFlatFrameValidator(),
            new AstronomicalWindowService(),
            sun,
            new CameraSkyBrightnessProvider(_ => Task.FromResult<SkyBrightnessSample?>(null)),
            new NullNotificationService(),
            clock);

    [Fact]
    public async Task Alternative_feasible_filter_is_selected_and_progress_is_preserved_on_partial_success()
    {
        var clock = new FakeClock();
        var fs = new MemoryFs();
        var campaigns = new CampaignService(new JsonCampaignRepository(fs, "/state"), clock);
        var sim = new SkySimulatorOptions { Darkening = true };
        var camera = new SimulatedCameraAcquisitionService(sim, seed: 7);
        var wheel = new SimulatedFilterWheelService(new[] { "L", "Ha" }, sim);
        var sun = new ApproximateSunAltitudeProvider(overrideCalc: _ => -6); // Open for [-20,5]

        var filters = new List<FilterCampaignSettings>
        {
            new() { FilterName = "L", TargetCount = 1, Enabled = true, TargetHistogramFraction = 25000d / 65535d, TargetToleranceFraction = 0.8, MinExposureSeconds = 0.01, MaxExposureSeconds = 30 },
            // Ha's max exposure is deliberately tiny; combined with the seeded history below its
            // *unclamped* required exposure will exceed it, so it must be classified TooLong.
            new() { FilterName = "Ha", TargetCount = 5, Enabled = true, TargetHistogramFraction = 25000d / 65535d, TargetToleranceFraction = 0.8, MinExposureSeconds = 0.01, MaxExposureSeconds = 2 }
        };

        // Seed prior history for Ha (as if captured earlier this campaign) that makes its
        // *unclamped* required exposure land well above MaxExposureSeconds.
        await campaigns.GetOrCreateAsync("flow", "p1", filters, new CampaignOptions());
        await campaigns.AcceptFlatAsync("flow", "Ha", exposureSeconds: 1.0, measuredAdu: 1000);

        var runner = CreateRunner(campaigns, camera, wheel, sun, clock);

        var result = await runner.RunAsync(new SkyFlatSessionRequest
        {
            CampaignKey = "flow",
            ProfileId = "p1",
            Mode = CampaignMode.Evening,
            Strategy = FilterOrderStrategyKind.Adaptive,
            MaxDurationMinutes = 10,
            AllowWaitForSky = false,
            WhenNoFilterFeasible = WhenNoFilterFeasibleAction.PartialSuccess,
            Options = new CampaignOptions
            {
                DryRun = true,
                EveningWindow = new AstronomicalWindowOptions { MinSunAltitudeDegrees = -20, MaxSunAltitudeDegrees = 5 },
                Filters = filters
            },
            Filters = filters
        }, progress: null, CancellationToken.None);

        // L (still feasible) was selected and completed even though Ha (infeasible) was
        // incomplete — the runner must not give up just because ONE candidate is infeasible.
        result.Campaign!.Filters["L"].Accepted.Should().Be(1);
        result.Campaign.Filters["L"].IsComplete.Should().BeTrue();

        // Evening + TooLong ⇒ waiting cannot help ⇒ stop immediately with a clear reason, and
        // Ha's prior progress (seeded above) must not have been lost.
        result.FinalState.Should().Be(SessionState.StoppedByWindow);
        result.StopReason.Should().Be(SessionStopReasons.NoFilterFeasible);
        result.Campaign.Filters["Ha"].Accepted.Should().Be(1);
        result.IsPartialSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Wait_timer_for_astronomical_window_starts_fresh_after_capturing_a_flat()
    {
        var clock = new FakeClock();
        var fs = new MemoryFs();
        var campaigns = new CampaignService(new JsonCampaignRepository(fs, "/state"), clock);
        var sim = new SkySimulatorOptions { Darkening = true };
        var camera = new SimulatedCameraAcquisitionService(sim, seed: 3);
        var wheel = new SimulatedFilterWheelService(new[] { "L" }, sim);

        // Call 0 is the runner's one-time pre-loop "previousAltitude" read (harmless here since
        // Mode is explicit, not Automatic). Call 1 is the first loop iteration: Open -> capture
        // +accept L (TargetCount=1, so it completes immediately). Call 2: TooEarly, first
        // occurrence for this reason -> WaitTracker resets to 0, continues (one real ~2s poll).
        // Call 3+: TooEarly again, but the clock has now jumped forward 10 minutes -> exceeds the
        // small MaxWaitMinutes budget for THIS reason, causing a timeout.
        // If the wait timer were not properly scoped/reset (e.g. reused a single session-wide
        // "waitStarted" timestamp), this would time out on the very first TooEarly check instead
        // of getting a fresh budget.
        var sun = new ScriptedSunAltitudeProvider(clock, call => call switch
        {
            0 => (-6d, TimeSpan.Zero),
            1 => (-6d, TimeSpan.Zero),
            2 => (10d, TimeSpan.Zero),
            _ => (10d, TimeSpan.FromMinutes(10))
        });

        var filters = new List<FilterCampaignSettings>
        {
            // TargetCount=2 so the filter is still incomplete after the one flat captured while
            // the window was open — otherwise the campaign would complete before the TooEarly
            // wait phase is ever reached.
            new() { FilterName = "L", TargetCount = 2, Enabled = true, TargetHistogramFraction = 25000d / 65535d, TargetToleranceFraction = 0.8, MinExposureSeconds = 0.01, MaxExposureSeconds = 30 }
        };

        var runner = CreateRunner(campaigns, camera, wheel, sun, clock);

        var result = await runner.RunAsync(new SkyFlatSessionRequest
        {
            CampaignKey = "wait-reset",
            ProfileId = "p1",
            Mode = CampaignMode.Evening,
            MaxDurationMinutes = 120,
            AllowWaitForSky = true,
            MaxWaitMinutes = 5,
            Options = new CampaignOptions
            {
                DryRun = true,
                EveningWindow = new AstronomicalWindowOptions { MinSunAltitudeDegrees = -20, MaxSunAltitudeDegrees = 5 },
                Filters = filters
            },
            Filters = filters
        }, progress: null, CancellationToken.None);

        result.FinalState.Should().Be(SessionState.StoppedByWindow);
        result.StopReason.Should().Be(SessionStopReasons.AstronomicalWindowWaitTimeout);

        // The flat captured while the window was still open must survive into the final result,
        // proving the later wait/timeout did not roll back or ignore earlier progress.
        result.Campaign!.Filters["L"].Accepted.Should().Be(1);
        result.AcceptedThisSession.Should().Be(1);
    }

    [Fact]
    public async Task Cancellation_during_a_wait_stops_cleanly_and_preserves_progress()
    {
        var clock = new FakeClock();
        var fs = new MemoryFs();
        var campaigns = new CampaignService(new JsonCampaignRepository(fs, "/state"), clock);
        var sim = new SkySimulatorOptions { Darkening = true };
        var camera = new SimulatedCameraAcquisitionService(sim, seed: 11);
        var wheel = new SimulatedFilterWheelService(new[] { "L" }, sim);

        // Call 0 is the runner's one-time pre-loop "previousAltitude" read. Call 1 (first loop
        // iteration): Open -> capture+accept L (TargetCount=2, stays incomplete after 1 accepted).
        // Call 2+: TooEarly forever -> the runner enters a real poll loop; the token is cancelled
        // shortly after, so cancellation must be observed mid-wait rather than only at start.
        var sun = new ScriptedSunAltitudeProvider(clock, call => call <= 1 ? (-6d, TimeSpan.Zero) : (10d, TimeSpan.Zero));

        var filters = new List<FilterCampaignSettings>
        {
            new() { FilterName = "L", TargetCount = 2, Enabled = true, TargetHistogramFraction = 25000d / 65535d, TargetToleranceFraction = 0.8, MinExposureSeconds = 0.01, MaxExposureSeconds = 30 }
        };

        var runner = CreateRunner(campaigns, camera, wheel, sun, clock);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        var result = await runner.RunAsync(new SkyFlatSessionRequest
        {
            CampaignKey = "cancel-wait",
            ProfileId = "p1",
            Mode = CampaignMode.Evening,
            MaxDurationMinutes = 120,
            AllowWaitForSky = true,
            MaxWaitMinutes = 45,
            Options = new CampaignOptions
            {
                DryRun = true,
                EveningWindow = new AstronomicalWindowOptions { MinSunAltitudeDegrees = -20, MaxSunAltitudeDegrees = 5 },
                Filters = filters
            },
            Filters = filters
        }, progress: null, cts.Token);

        result.FinalState.Should().Be(SessionState.Cancelled);
        result.StopReason.Should().Be(SessionStopReasons.Cancelled);
        result.Campaign!.Filters["L"].Accepted.Should().Be(1);
    }
}
