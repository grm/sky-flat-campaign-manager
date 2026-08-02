using System.ComponentModel.Composition;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.SkyFlatCampaignManager.Properties;
using NINA.Plugin.SkyFlatCampaignManager.Services;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Equipment;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Instructions;

[ExportMetadata("Name", "Run Sky Flat Campaign")]
[ExportMetadata("Description", "Runs an evening/morning multi-day sky flat campaign. Progress is persisted after each accepted flat. SQM optional.")]
[ExportMetadata("Icon", "BrightnessSVG")]
[ExportMetadata("Category", "Sky Flat Campaign Manager")]
[Export(typeof(ISequenceItem))]
[JsonObject(MemberSerialization.OptIn)]
public class RunSkyFlatCampaignInstruction : SequenceItem
{
    private readonly IProfileService _profileService;
    private readonly ICameraMediator _cameraMediator;
    private readonly IFilterWheelMediator _filterWheelMediator;
    private readonly ITelescopeMediator _telescopeMediator;
    private readonly IImagingMediator _imagingMediator;
    private readonly IImageSaveMediator _imageSaveMediator;
    private readonly IImageHistoryVM _imageHistoryVM;
    private readonly IWeatherDataMediator _weatherDataMediator;
    private readonly IApplicationStatusMediator _applicationStatusMediator;

    [ImportingConstructor]
    public RunSkyFlatCampaignInstruction(
        IProfileService profileService,
        ICameraMediator cameraMediator,
        IFilterWheelMediator filterWheelMediator,
        ITelescopeMediator telescopeMediator,
        IImagingMediator imagingMediator,
        IImageSaveMediator imageSaveMediator,
        IImageHistoryVM imageHistoryVM,
        IWeatherDataMediator weatherDataMediator,
        IApplicationStatusMediator applicationStatusMediator)
    {
        _profileService = profileService;
        _cameraMediator = cameraMediator;
        _filterWheelMediator = filterWheelMediator;
        _telescopeMediator = telescopeMediator;
        _imagingMediator = imagingMediator;
        _imageSaveMediator = imageSaveMediator;
        _imageHistoryVM = imageHistoryVM;
        _weatherDataMediator = weatherDataMediator;
        _applicationStatusMediator = applicationStatusMediator;
        Mode = CampaignMode.Automatic;
        Strategy = FilterOrderStrategyKind.Adaptive;
        MaxDurationMinutes = 90;
        AllowWaitForSky = true;
        MaxWaitMinutes = 45;
        CampaignKey = "default";
        UseSqm = false;
        PointingMode = MountPointingMode.KeepCurrent;
        Tracking = TrackingMode.DisableTracking;
        WhenNoFlatsRequired = WhenNoFlatsRequiredAction.SucceedImmediately;
        WhenNoFilterFeasible = WhenNoFilterFeasibleAction.PartialSuccess;
        OnFilterError = OnFilterErrorAction.ContinueNextFilter;
        SimulationMode = false;
    }

    private RunSkyFlatCampaignInstruction(RunSkyFlatCampaignInstruction copyMe) : this(
        copyMe._profileService,
        copyMe._cameraMediator,
        copyMe._filterWheelMediator,
        copyMe._telescopeMediator,
        copyMe._imagingMediator,
        copyMe._imageSaveMediator,
        copyMe._imageHistoryVM,
        copyMe._weatherDataMediator,
        copyMe._applicationStatusMediator)
    {
        CopyMetaData(copyMe);
        Mode = copyMe.Mode;
        Strategy = copyMe.Strategy;
        MaxDurationMinutes = copyMe.MaxDurationMinutes;
        AllowWaitForSky = copyMe.AllowWaitForSky;
        MaxWaitMinutes = copyMe.MaxWaitMinutes;
        CampaignKey = copyMe.CampaignKey;
        UseSqm = copyMe.UseSqm;
        PointingMode = copyMe.PointingMode;
        Tracking = copyMe.Tracking;
        TargetAltitudeDegrees = copyMe.TargetAltitudeDegrees;
        TargetAzimuthDegrees = copyMe.TargetAzimuthDegrees;
        WhenNoFlatsRequired = copyMe.WhenNoFlatsRequired;
        WhenNoFilterFeasible = copyMe.WhenNoFilterFeasible;
        OnFilterError = copyMe.OnFilterError;
        SimulationMode = copyMe.SimulationMode;
    }

    [JsonProperty] public CampaignMode Mode { get; set; }
    [JsonProperty] public FilterOrderStrategyKind Strategy { get; set; }
    [JsonProperty] public double MaxDurationMinutes { get; set; }
    [JsonProperty] public bool AllowWaitForSky { get; set; }
    [JsonProperty] public double MaxWaitMinutes { get; set; }
    [JsonProperty] public string CampaignKey { get; set; }
    [JsonProperty] public bool UseSqm { get; set; }
    [JsonProperty] public MountPointingMode PointingMode { get; set; }
    [JsonProperty] public TrackingMode Tracking { get; set; }
    [JsonProperty] public double TargetAltitudeDegrees { get; set; } = 80;
    [JsonProperty] public double TargetAzimuthDegrees { get; set; }
    [JsonProperty] public WhenNoFlatsRequiredAction WhenNoFlatsRequired { get; set; }
    [JsonProperty] public WhenNoFilterFeasibleAction WhenNoFilterFeasible { get; set; }
    [JsonProperty] public OnFilterErrorAction OnFilterError { get; set; }
    [JsonProperty] public bool SimulationMode { get; set; }

    public string ProgressText { get; private set; } = "Idle";

    public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
    {
        if (!Settings.Default.PluginEnabled)
        {
            ProgressText = "Plugin disabled";
            RaisePropertyChanged(nameof(ProgressText));
            return;
        }

        var options = PluginServiceFactory.CreateOptionsFromSettings();
        options.SimulationMode = SimulationMode;
        options.DryRun = options.DryRun || SimulationMode;
        var filters = PluginServiceFactory.CreateFilterSettings(_profileService);

        var runner = PluginServiceFactory.CreateRunner(
            _profileService,
            _cameraMediator,
            _filterWheelMediator,
            _telescopeMediator,
            _imagingMediator,
            _imageSaveMediator,
            _imageHistoryVM,
            _weatherDataMediator,
            UseSqm,
            SimulationMode,
            m => Logger.Info($"[{PluginIdentity.ShortName}] {m}"));

        var request = new SkyFlatSessionRequest
        {
            CampaignKey = string.IsNullOrWhiteSpace(CampaignKey) ? "default" : CampaignKey,
            ProfileId = _profileService.ActiveProfile.Id.ToString(),
            Mode = Mode,
            Strategy = Strategy,
            MaxDurationMinutes = MaxDurationMinutes,
            AllowWaitForSky = AllowWaitForSky,
            MaxWaitMinutes = MaxWaitMinutes,
            WhenNoFlatsRequired = WhenNoFlatsRequired,
            WhenNoFilterFeasible = WhenNoFilterFeasible,
            OnFilterError = OnFilterError,
            UseSqm = UseSqm,
            Options = options,
            Filters = filters,
            Pointing = new MountPointingRequest
            {
                Mode = PointingMode,
                Tracking = Tracking,
                AltitudeDegrees = TargetAltitudeDegrees,
                AzimuthDegrees = TargetAzimuthDegrees,
                MinSunSeparationDegrees = Settings.Default.SunSafetySeparationDegrees
            }
        };

        var progressAdapter = new Progress<SkyFlatSessionProgress>(p =>
        {
            var levelText = p.MeasuredHistogramFraction is { } frac
                ? $"{frac * 100.0:F1}%/{p.MeasuredAdu:F0}ADU"
                : "n/a";
            ProgressText = $"{p.State}: {p.CurrentFilter} level={levelText} exp={p.ExposureSeconds:F3}s rem={p.Remaining} — {p.StatusMessage}";
            RaisePropertyChanged(nameof(ProgressText));
            progress?.Report(new ApplicationStatus
            {
                Status = $"[{PluginIdentity.ShortName}] {ProgressText}"
            });
            _applicationStatusMediator.StatusUpdate(new ApplicationStatus
            {
                Source = PluginIdentity.ShortName,
                Status = ProgressText
            });
        });

        var result = await runner.RunAsync(request, progressAdapter, token).ConfigureAwait(false);
        ProgressText = $"{result.FinalState}: {result.StopReason} (accepted={result.AcceptedThisSession}, rejected={result.RejectedThisSession})";
        RaisePropertyChanged(nameof(ProgressText));

        // Partial success (window/timeout) completes without throwing — NINA sequencer success.
        if (result.FinalState == SessionState.Faulted)
        {
            throw new SequenceEntityFailedException(ProgressText);
        }
    }

    public override object Clone() => new RunSkyFlatCampaignInstruction(this);

    public override string ToString()
        => $"Category: {Category}, Item: {nameof(RunSkyFlatCampaignInstruction)}, Mode={Mode}, Key={CampaignKey}";
}
