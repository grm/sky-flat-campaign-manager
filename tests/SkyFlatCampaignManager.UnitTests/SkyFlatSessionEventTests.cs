using FluentAssertions;
using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Astronomy;
using SkyFlatCampaignManager.Core.Brightness;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Equipment;
using SkyFlatCampaignManager.Core.Notifications;
using SkyFlatCampaignManager.Core.Simulation;
using SkyFlatCampaignManager.Core.Utilities;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

public class SkyFlatSessionEventTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 9, 26, 5, 30, 0, DateTimeKind.Utc);
        public DateTime LocalNow => UtcNow;
    }

    private sealed class MemoryFs : IFileSystem
    {
        private readonly Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        public bool FileExists(string path) => files.ContainsKey(path);
        public bool DirectoryExists(string path) => true;
        public void CreateDirectory(string path) { }
        public string ReadAllText(string path) => files[path];
        public void WriteAllText(string path, string contents) => files[path] = contents;
        public void WriteAllBytes(string path, byte[] contents) => files[path] = Convert.ToBase64String(contents);
        public byte[] ReadAllBytes(string path) => Convert.FromBase64String(files[path]);
        public void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName)
        {
            if (destinationBackupFileName is not null && files.TryGetValue(destinationFileName, out var old))
                files[destinationBackupFileName] = old;
            files[destinationFileName] = files[sourceFileName];
            files.Remove(sourceFileName);
        }
        public void Delete(string path) => files.Remove(path);
        public void Copy(string source, string destination, bool overwrite) => files[destination] = files[source];
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => files.Keys;
    }

    private sealed class RecordingSink : ISkyFlatSessionEventSink
    {
        public List<SkyFlatSessionEvent> Events { get; } = new();
        public Task OnEventAsync(SkyFlatSessionEvent sessionEvent, CancellationToken cancellationToken)
        {
            Events.Add(sessionEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ScriptedSun : ISunAltitudeProvider
    {
        private readonly double[] values;
        private int index;
        public ScriptedSun(params double[] values) => this.values = values;
        public double GetSunAltitudeDegrees(DateTime utc)
        {
            var i = Math.Min(index++, values.Length - 1);
            return values[i];
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

    private static List<FilterCampaignSettings> OneFlatFilter() =>
    [
        new()
        {
            FilterName = "L",
            Enabled = true,
            TargetCount = 1,
            TargetHistogramFraction = 0.5,
            TargetToleranceFraction = 1.0,
            MinExposureSeconds = 0.01,
            MaxExposureSeconds = 30
        }
    ];

    [Fact]
    public async Task Completed_current_campaign_emits_skip_event_and_does_not_start_filter()
    {
        var clock = new FakeClock();
        var fs = new MemoryFs();
        var campaigns = new CampaignService(new JsonCampaignRepository(fs, "/state"), clock);
        var filters = OneFlatFilter();
        var options = new CampaignOptions { DryRun = true, Filters = filters };
        await campaigns.GetOrCreateAsync("skip", "p1", filters, options);
        await campaigns.AcceptFlatAsync("skip", "L", 1, 32768, 0.5, -6);
        await campaigns.MarkCompletedAsync("skip", options);

        var sim = new SkySimulatorOptions();
        var sink = new RecordingSink();
        var runner = CreateRunner(
            campaigns,
            new SimulatedCameraAcquisitionService(sim, seed: 1),
            new SimulatedFilterWheelService(new[] { "L" }, sim),
            new ApproximateSunAltitudeProvider(overrideCalc: _ => -6),
            clock);

        var result = await runner.RunAsync(new SkyFlatSessionRequest
        {
            CampaignKey = "skip",
            ProfileId = "p1",
            Mode = CampaignMode.Morning,
            EventSink = sink,
            Options = options,
            Filters = filters
        }, null, CancellationToken.None);

        result.FinalState.Should().Be(SessionState.Completed);
        sink.Events.Select(e => e.Kind).Should().Equal(SkyFlatSessionEventKind.CampaignNotRequired);
        sink.Events[0].Remaining.Should().Be(0);
    }

    [Fact]
    public async Task Required_campaign_emits_filter_and_completion_events_in_blocking_order()
    {
        var clock = new FakeClock();
        var fs = new MemoryFs();
        var campaigns = new CampaignService(new JsonCampaignRepository(fs, "/state"), clock);
        var filters = OneFlatFilter();
        var sim = new SkySimulatorOptions { Darkening = false };
        var sink = new RecordingSink();
        var runner = CreateRunner(
            campaigns,
            new SimulatedCameraAcquisitionService(sim, seed: 2),
            new SimulatedFilterWheelService(new[] { "L" }, sim),
            new ApproximateSunAltitudeProvider(overrideCalc: _ => -6),
            clock);

        var result = await runner.RunAsync(new SkyFlatSessionRequest
        {
            CampaignKey = "complete",
            ProfileId = "p1",
            Mode = CampaignMode.Morning,
            EventSink = sink,
            MaxDurationMinutes = 10,
            Options = new CampaignOptions
            {
                DryRun = true,
                MorningWindow = new AstronomicalWindowOptions { MinSunAltitudeDegrees = -12, MaxSunAltitudeDegrees = -1 },
                Filters = filters
            },
            Filters = filters
        }, null, CancellationToken.None);

        result.Campaign!.IsComplete.Should().BeTrue();
        sink.Events.Select(e => e.Kind).Should().Equal(
            SkyFlatSessionEventKind.CampaignRequired,
            SkyFlatSessionEventKind.BeforeFilter,
            SkyFlatSessionEventKind.AfterFilter,
            SkyFlatSessionEventKind.CampaignCompleted);
        sink.Events.First().Remaining.Should().Be(1);
        sink.Events.Last().Remaining.Should().Be(0);
    }

    [Fact]
    public async Task Adaptive_polling_is_one_wait_episode_not_one_event_per_probe()
    {
        var clock = new FakeClock();
        var fs = new MemoryFs();
        var campaigns = new CampaignService(new JsonCampaignRepository(fs, "/state"), clock);
        var filters = OneFlatFilter();
        var sim = new SkySimulatorOptions();
        var sink = new RecordingSink();

        // First call seeds previousAltitude, second loop call is TooEarly (morning sky still
        // darker than the -12° lower bound), then the window opens.
        var sun = new ScriptedSun(-13, -13, -6, -6, -6);
        var runner = CreateRunner(
            campaigns,
            new SimulatedCameraAcquisitionService(sim, seed: 3),
            new SimulatedFilterWheelService(new[] { "L" }, sim),
            sun,
            clock);

        var result = await runner.RunAsync(new SkyFlatSessionRequest
        {
            CampaignKey = "wait-episode",
            ProfileId = "p1",
            Mode = CampaignMode.Morning,
            EventSink = sink,
            AllowWaitForSky = true,
            MaxWaitMinutes = 5,
            MaxDurationMinutes = 10,
            Options = new CampaignOptions
            {
                DryRun = true,
                MorningWindow = new AstronomicalWindowOptions { MinSunAltitudeDegrees = -12, MaxSunAltitudeDegrees = -1 },
                Filters = filters
            },
            Filters = filters
        }, null, CancellationToken.None);

        result.Campaign!.IsComplete.Should().BeTrue();
        sink.Events.Count(e => e.Kind == SkyFlatSessionEventKind.BeforeWait).Should().Be(1);
        sink.Events.Count(e => e.Kind == SkyFlatSessionEventKind.AfterWait).Should().Be(1);
        sink.Events.FindIndex(e => e.Kind == SkyFlatSessionEventKind.BeforeWait)
            .Should().BeLessThan(sink.Events.FindIndex(e => e.Kind == SkyFlatSessionEventKind.AfterWait));
    }
}
