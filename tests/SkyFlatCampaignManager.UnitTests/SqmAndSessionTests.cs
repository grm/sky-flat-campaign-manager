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

public class SqmAndSessionTests
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

    [Fact]
    public async Task Sqm_stale_is_marked()
    {
        var clock = new FakeClock();
        var provider = new SqmSkyBrightnessProvider(
            _ => Task.FromResult<(double?, DateTime?)>((20.1, clock.UtcNow.AddMinutes(-10))),
            clock,
            maxAgeSeconds: 60);
        var sample = await provider.GetSampleAsync();
        sample!.IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task Hybrid_falls_back_when_sqm_throws()
    {
        var camera = new CameraSkyBrightnessProvider(_ => Task.FromResult<SkyBrightnessSample?>(new SkyBrightnessSample
        {
            CapturedAtUtc = DateTime.UtcNow,
            CameraMedianAdu = 20000,
            Source = "Camera"
        }));
        var sqm = new SqmSkyBrightnessProvider(_ => throw new InvalidOperationException("disconnected"), new FakeClock());
        var hybrid = new HybridSkyBrightnessProvider(camera, sqm);
        var sample = await hybrid.GetSampleAsync();
        sample!.CameraMedianAdu.Should().Be(20000);
        sample.Source.Should().Contain("Camera");
    }

    [Fact]
    public async Task Session_runs_without_sqm_in_simulation()
    {
        var clock = new FakeClock();
        var fs = new MemoryFs();
        var repo = new JsonCampaignRepository(fs, "/state");
        var campaigns = new CampaignService(repo, clock);
        var sim = new SkySimulatorOptions { Darkening = true };
        var camera = new SimulatedCameraAcquisitionService(sim, seed: 42);
        var wheel = new SimulatedFilterWheelService(new[] { "L", "Ha" }, sim);
        var sun = new ApproximateSunAltitudeProvider(overrideCalc: _ => -6);
        var brightness = new CameraSkyBrightnessProvider(async ct =>
        {
            var frame = await camera.CaptureFlatAsync(new FlatCaptureRequest
            {
                FilterName = "L",
                ExposureSeconds = 0.5,
                DryRun = true,
                SaveImage = false
            }, ct);
            return new SkyBrightnessSample
            {
                CapturedAtUtc = clock.UtcNow,
                CameraMedianAdu = frame.Statistics.MedianAdu,
                CameraExposureSeconds = 0.5,
                Source = "Camera"
            };
        });

        var runner = new SkyFlatSessionRunner(
            campaigns,
            camera,
            wheel,
            new NoOpMountPositioningService(),
            new ProportionalFlatExposureEstimator(),
            new DefaultFlatFrameValidator(),
            new AstronomicalWindowService(),
            sun,
            brightness,
            new NullNotificationService(),
            clock);

        var filters = new List<FilterCampaignSettings>
        {
            new() { FilterName = "L", TargetCount = 3, Enabled = true, TargetAdu = 25000, AduTolerance = 20000, MinExposureSeconds = 0.01, MaxExposureSeconds = 30 },
            new() { FilterName = "Ha", TargetCount = 2, Enabled = true, TargetAdu = 25000, AduTolerance = 20000, MinExposureSeconds = 0.01, MaxExposureSeconds = 30 }
        };

        var result = await runner.RunAsync(new SkyFlatSessionRequest
        {
            CampaignKey = "sim",
            ProfileId = "p",
            Mode = CampaignMode.Evening,
            Strategy = FilterOrderStrategyKind.Adaptive,
            MaxDurationMinutes = 10,
            AllowWaitForSky = false,
            Options = new CampaignOptions
            {
                DryRun = true,
                SimulationMode = true,
                EveningWindow = new AstronomicalWindowOptions { MinSunAltitudeDegrees = -20, MaxSunAltitudeDegrees = 5 },
                Filters = filters
            },
            Filters = filters
        }, progress: null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.AcceptedThisSession.Should().BeGreaterThan(0);
        result.Campaign!.TotalAccepted.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Cancellation_stops_cleanly()
    {
        var clock = new FakeClock();
        var fs = new MemoryFs();
        var campaigns = new CampaignService(new JsonCampaignRepository(fs, "/state"), clock);
        var sim = new SkySimulatorOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var runner = new SkyFlatSessionRunner(
            campaigns,
            new SimulatedCameraAcquisitionService(sim),
            new SimulatedFilterWheelService(new[] { "L" }, sim),
            new NoOpMountPositioningService(),
            new ProportionalFlatExposureEstimator(),
            new DefaultFlatFrameValidator(),
            new AstronomicalWindowService(),
            new ApproximateSunAltitudeProvider(overrideCalc: _ => -6),
            new CameraSkyBrightnessProvider(_ => Task.FromResult<SkyBrightnessSample?>(null)),
            new NullNotificationService(),
            clock);

        var result = await runner.RunAsync(new SkyFlatSessionRequest
        {
            Filters = new[] { new FilterCampaignSettings { FilterName = "L", TargetCount = 10 } },
            Options = new CampaignOptions { DryRun = true, EveningWindow = new AstronomicalWindowOptions { MinSunAltitudeDegrees = -20, MaxSunAltitudeDegrees = 5 } },
            AllowWaitForSky = false
        }, null, cts.Token);

        result.FinalState.Should().Be(SessionState.Cancelled);
    }

    [Fact]
    public void Sun_safety_check()
    {
        new AstronomicalWindowService().IsSunTooClose(10, 30).Should().BeTrue();
        new AstronomicalWindowService().IsSunTooClose(40, 30).Should().BeFalse();
    }
}
