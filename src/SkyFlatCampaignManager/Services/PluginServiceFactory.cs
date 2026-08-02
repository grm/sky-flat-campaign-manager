using System.IO;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.SkyFlatCampaignManager.Adapters;
using NINA.Plugin.SkyFlatCampaignManager.Properties;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Astronomy;
using SkyFlatCampaignManager.Core.Brightness;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Equipment;
using SkyFlatCampaignManager.Core.Notifications;
using SkyFlatCampaignManager.Core.Simulation;
using SkyFlatCampaignManager.Core.Utilities;

namespace NINA.Plugin.SkyFlatCampaignManager.Services;

public static class PluginServiceFactory
{
    public static string ResolveStateDirectory()
    {
        if (!string.IsNullOrWhiteSpace(Settings.Default.StateDirectory))
        {
            return Settings.Default.StateDirectory;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NINA",
            PluginIdentity.ShortName);
    }

    public static CampaignOptions CreateOptionsFromSettings()
    {
        return new CampaignOptions
        {
            PluginEnabled = Settings.Default.PluginEnabled,
            CampaignName = Settings.Default.CampaignName,
            StateDirectory = ResolveStateDirectory(),
            ValidityDays = Settings.Default.ValidityDays,
            AutoStartExpiredCampaign = Settings.Default.AutoStartExpiredCampaign,
            DetailedLogging = Settings.Default.DetailedLogging,
            DryRun = Settings.Default.DryRun,
            SunSafetySeparationDegrees = Settings.Default.SunSafetySeparationDegrees
        };
    }

    public static JsonFilterCampaignConfigRepository CreateFilterConfigRepository()
        => new(new RealFileSystem(), ResolveStateDirectory());

    public static FilterCampaignDefaults CreateFilterDefaults()
    {
        var targetHistogramFraction = Math.Clamp(Settings.Default.DefaultTargetHistogramPercent / 100.0, 0d, 1d);
        var targetToleranceFraction = Math.Max(0d, Settings.Default.DefaultTargetTolerancePercent / 100.0);

        return new FilterCampaignDefaults
        {
            TargetCount = Settings.Default.DefaultTargetCount,
            TargetHistogramFraction = targetHistogramFraction,
            TargetToleranceFraction = targetToleranceFraction,
            // Legacy ADU fields are derived only for backward compatibility with any code/tooling
            // still reading them; live acceptance always uses the fraction fields above.
            TargetAdu = targetHistogramFraction * PluginIdentity.LegacyMigrationMaxAdu,
            AduTolerance = targetHistogramFraction * PluginIdentity.LegacyMigrationMaxAdu * targetToleranceFraction,
            MinExposureSeconds = PluginIdentity.DefaultMinExposureSeconds,
            MaxExposureSeconds = PluginIdentity.DefaultMaxExposureSeconds,
            Gain = -1,
            Offset = -1,
            BinningX = 1,
            BinningY = 1
        };
    }

    public static List<FilterCampaignSettings> CreateFilterSettings(IProfileService profileService, int? targetOverride = null)
    {
        var filters = profileService.ActiveProfile?.FilterWheelSettings?.FilterWheelFilters;
        if (filters is null)
        {
            return new List<FilterCampaignSettings>();
        }

        var profileId = profileService.ActiveProfile?.Id.ToString() ?? "default";
        var repo = CreateFilterConfigRepository();
        var saved = repo.Load(profileId).Filters;

        var names = filters
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .Select(f => f.Name)
            .ToList();

        var defaults = CreateFilterDefaults();

        var seed = new Dictionary<string, FilterCampaignSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Name))
            {
                continue;
            }

            var fw = filter.FlatWizardFilterSettings;
            if (fw is null)
            {
                continue;
            }

            seed[filter.Name] = new FilterCampaignSettings
            {
                FilterName = filter.Name,
                Enabled = true,
                Gain = fw.Gain,
                Offset = fw.Offset,
                BinningX = Math.Max(1, (int)(fw.Binning?.X ?? 1)),
                BinningY = Math.Max(1, (int)(fw.Binning?.Y ?? 1)),
                MinExposureSeconds = fw.MinFlatExposureTime > 0 ? fw.MinFlatExposureTime : PluginIdentity.DefaultMinExposureSeconds,
                MaxExposureSeconds = fw.MaxFlatExposureTime > 0 ? fw.MaxFlatExposureTime : PluginIdentity.DefaultMaxExposureSeconds,
                TargetHistogramFraction = defaults.TargetHistogramFraction,
                TargetToleranceFraction = defaults.TargetToleranceFraction,
                TargetAdu = defaults.TargetAdu,
                AduTolerance = defaults.AduTolerance,
                TargetCount = defaults.TargetCount
            };
        }

        if (targetOverride is int t)
        {
            defaults = new FilterCampaignDefaults
            {
                TargetCount = t,
                TargetHistogramFraction = defaults.TargetHistogramFraction,
                TargetToleranceFraction = defaults.TargetToleranceFraction,
                TargetAdu = defaults.TargetAdu,
                AduTolerance = defaults.AduTolerance,
                MinExposureSeconds = defaults.MinExposureSeconds,
                MaxExposureSeconds = defaults.MaxExposureSeconds,
                Gain = defaults.Gain,
                Offset = defaults.Offset,
                BinningX = defaults.BinningX,
                BinningY = defaults.BinningY
            };
        }

        var merged = JsonFilterCampaignConfigRepository.MergeWithWheel(names, saved, defaults, seed);
        if (targetOverride is int overrideCount)
        {
            foreach (var f in merged)
            {
                f.TargetCount = overrideCount;
            }
        }

        return merged;
    }

    public static void SaveFilterSettings(IProfileService profileService, IEnumerable<FilterCampaignSettings> filters)
    {
        var profileId = profileService.ActiveProfile?.Id.ToString() ?? "default";
        CreateFilterConfigRepository().Save(profileId, filters);
    }

    public static SkyFlatSessionRunner CreateRunner(
        IProfileService profileService,
        ICameraMediator cameraMediator,
        IFilterWheelMediator filterWheelMediator,
        ITelescopeMediator telescopeMediator,
        IImagingMediator imagingMediator,
        IImageSaveMediator imageSaveMediator,
        IImageHistoryVM imageHistoryVM,
        IWeatherDataMediator weatherDataMediator,
        bool useSqm,
        bool simulation,
        Action<string>? log = null)
    {
        var fs = new RealFileSystem();
        var clock = new SystemClock();
        var repo = new JsonCampaignRepository(fs, ResolveStateDirectory());
        var campaigns = new CampaignService(repo, clock);

        ICameraAcquisitionService camera;
        IFilterWheelService wheel;
        IMountPositioningService mount;
        ISunAltitudeProvider sun;
        ISkyBrightnessProvider brightness;

        if (simulation || Settings.Default.DryRun && cameraMediator.GetInfo()?.Connected != true)
        {
            var sim = new SkySimulatorOptions();
            var names = CreateFilterSettings(profileService).Select(f => f.FilterName).ToList();
            if (names.Count == 0) names.Add("L");
            camera = new SimulatedCameraAcquisitionService(sim);
            wheel = new SimulatedFilterWheelService(names, sim);
            mount = new NoOpMountPositioningService();
            sun = new NinaSunAltitudeProvider(profileService);
            brightness = new CameraSkyBrightnessProvider(async ct =>
            {
                var frame = await camera.CaptureFlatAsync(new FlatCaptureRequest { FilterName = names[0], ExposureSeconds = 0.5, DryRun = true, SaveImage = false }, ct);
                return new SkyBrightnessSample
                {
                    CapturedAtUtc = DateTime.UtcNow,
                    CameraMedianAdu = frame.Statistics.MedianAdu,
                    Source = "Camera"
                };
            });
        }
        else
        {
            camera = new NinaCameraAcquisitionService(profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVM, log);
            wheel = new NinaFilterWheelService(profileService, filterWheelMediator);
            mount = new NinaMountPositioningService(profileService, telescopeMediator, log);
            sun = new NinaSunAltitudeProvider(profileService);
            var cameraProvider = new CameraSkyBrightnessProvider(async ct =>
            {
                // Anticipation probe is best-effort; session still validates each flat.
                return new SkyBrightnessSample { CapturedAtUtc = DateTime.UtcNow, Source = "Camera" };
            });

            if (useSqm)
            {
                var sqm = new SqmSkyBrightnessProvider(async ct =>
                {
                    await Task.CompletedTask;
                    var info = weatherDataMediator.GetInfo();
                    if (info is null || !info.Connected || double.IsNaN(info.SkyQuality))
                    {
                        return (null, null);
                    }

                    return (info.SkyQuality, DateTime.UtcNow);
                }, clock, Settings.Default.ValidityDays > 0 ? 120 : 120);

                brightness = new HybridSkyBrightnessProvider(cameraProvider, sqm, log);
            }
            else
            {
                brightness = cameraProvider;
            }
        }

        return new SkyFlatSessionRunner(
            campaigns,
            camera,
            wheel,
            mount,
            new ProportionalFlatExposureEstimator(),
            new DefaultFlatFrameValidator(),
            new AstronomicalWindowService(),
            sun,
            brightness,
            new NinaNotificationService(),
            clock,
            log ?? (m => Logger.Info($"[{PluginIdentity.ShortName}] {m}")));
    }

    public static ICampaignService CreateCampaignService()
    {
        return new CampaignService(new JsonCampaignRepository(new RealFileSystem(), ResolveStateDirectory()), new SystemClock());
    }
}
