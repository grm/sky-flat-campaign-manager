using System.Globalization;
using System.ComponentModel.Composition;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.SkyFlatCampaignManager.Properties;
using NINA.Plugin.SkyFlatCampaignManager.Services;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Equipment;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Containers;

[ExportMetadata("Name", "Sky Flat Campaign Container")]
[ExportMetadata("Description", "Runs a sky-flat campaign with blocking custom event containers for notifications and automation.")]
[ExportMetadata("Icon", "BrightnessSVG")]
[ExportMetadata("Category", "Sky Flat Campaign Manager")]
[Export(typeof(ISequenceItem))]
[Export(typeof(ISequenceContainer))]
[JsonObject(MemberSerialization.OptIn)]
public sealed class SkyFlatCampaignContainer : SequentialContainer, ISkyFlatSessionEventSink
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

    private IProgress<ApplicationStatus>? _activeProgress;

    [JsonProperty] public SkyFlatEventContainer CampaignRequiredContainer { get; set; }
    [JsonProperty] public SkyFlatEventContainer CampaignNotRequiredContainer { get; set; }
    [JsonProperty] public SkyFlatEventContainer BeforeWaitContainer { get; set; }
    [JsonProperty] public SkyFlatEventContainer AfterWaitContainer { get; set; }
    [JsonProperty] public SkyFlatEventContainer BeforeFilterContainer { get; set; }
    [JsonProperty] public SkyFlatEventContainer AfterFilterContainer { get; set; }
    [JsonProperty] public SkyFlatEventContainer CampaignCompletedContainer { get; set; }
    [JsonProperty] public SkyFlatEventContainer SessionIncompleteContainer { get; set; }
    [JsonProperty] public SkyFlatEventContainer ErrorContainer { get; set; }

    [ImportingConstructor]
    public SkyFlatCampaignContainer(
        IProfileService profileService,
        ICameraMediator cameraMediator,
        IFilterWheelMediator filterWheelMediator,
        ITelescopeMediator telescopeMediator,
        IImagingMediator imagingMediator,
        IImageSaveMediator imageSaveMediator,
        IImageHistoryVM imageHistoryVM,
        IWeatherDataMediator weatherDataMediator,
        IApplicationStatusMediator applicationStatusMediator)
        : base()
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
        FilterStrategy = FilterOrderStrategyKind.Adaptive;
        MaxDurationMinutes = 90;
        AllowWaitForSky = true;
        MaxWaitMinutes = 45;
        AdaptiveProbeWait = true;
        MinProbeWaitSeconds = 5;
        MaxProbeWaitSeconds = 30;
        CampaignKey = "default";
        UseSqm = false;
        PointingMode = MountPointingMode.AltAz;
        Tracking = TrackingMode.DisableTracking;
        TargetAltitudeDegrees = 70;
        TargetAzimuthDegrees = 270;
        SunOffsetDegrees = 40;
        RestorePointingAtEnd = false;
        DitherBetweenFrames = false;
        WhenNoFilterFeasible = WhenNoFilterFeasibleAction.Wait;
        OnFilterError = OnFilterErrorAction.ContinueNextFilter;
        SimulationMode = false;

        CampaignRequiredContainer = NewEvent(SkyFlatEventType.CampaignRequired);
        CampaignNotRequiredContainer = NewEvent(SkyFlatEventType.CampaignNotRequired);
        BeforeWaitContainer = NewEvent(SkyFlatEventType.BeforeWait);
        AfterWaitContainer = NewEvent(SkyFlatEventType.AfterWait);
        BeforeFilterContainer = NewEvent(SkyFlatEventType.BeforeFilter);
        AfterFilterContainer = NewEvent(SkyFlatEventType.AfterFilter);
        CampaignCompletedContainer = NewEvent(SkyFlatEventType.CampaignCompleted);
        SessionIncompleteContainer = NewEvent(SkyFlatEventType.SessionIncomplete);
        ErrorContainer = NewEvent(SkyFlatEventType.Error);
    }

    private SkyFlatCampaignContainer(SkyFlatCampaignContainer copyMe) : this(
        copyMe._profileService, copyMe._cameraMediator, copyMe._filterWheelMediator,
        copyMe._telescopeMediator, copyMe._imagingMediator, copyMe._imageSaveMediator,
        copyMe._imageHistoryVM, copyMe._weatherDataMediator, copyMe._applicationStatusMediator)
    {
        CopyMetaData(copyMe);
        Mode = copyMe.Mode;
        FilterStrategy = copyMe.FilterStrategy;
        MaxDurationMinutes = copyMe.MaxDurationMinutes;
        AllowWaitForSky = copyMe.AllowWaitForSky;
        MaxWaitMinutes = copyMe.MaxWaitMinutes;
        AdaptiveProbeWait = copyMe.AdaptiveProbeWait;
        MinProbeWaitSeconds = copyMe.MinProbeWaitSeconds;
        MaxProbeWaitSeconds = copyMe.MaxProbeWaitSeconds;
        CampaignKey = copyMe.CampaignKey;
        UseSqm = copyMe.UseSqm;
        PointingMode = copyMe.PointingMode;
        Tracking = copyMe.Tracking;
        TargetAltitudeDegrees = copyMe.TargetAltitudeDegrees;
        TargetAzimuthDegrees = copyMe.TargetAzimuthDegrees;
        SunOffsetDegrees = copyMe.SunOffsetDegrees;
        RestorePointingAtEnd = copyMe.RestorePointingAtEnd;
        DitherBetweenFrames = copyMe.DitherBetweenFrames;
        WhenNoFilterFeasible = copyMe.WhenNoFilterFeasible;
        OnFilterError = copyMe.OnFilterError;
        SimulationMode = copyMe.SimulationMode;

        CampaignRequiredContainer = CloneEvent(copyMe.CampaignRequiredContainer);
        CampaignNotRequiredContainer = CloneEvent(copyMe.CampaignNotRequiredContainer);
        BeforeWaitContainer = CloneEvent(copyMe.BeforeWaitContainer);
        AfterWaitContainer = CloneEvent(copyMe.AfterWaitContainer);
        BeforeFilterContainer = CloneEvent(copyMe.BeforeFilterContainer);
        AfterFilterContainer = CloneEvent(copyMe.AfterFilterContainer);
        CampaignCompletedContainer = CloneEvent(copyMe.CampaignCompletedContainer);
        SessionIncompleteContainer = CloneEvent(copyMe.SessionIncompleteContainer);
        ErrorContainer = CloneEvent(copyMe.ErrorContainer);
    }

    [JsonProperty] public CampaignMode Mode { get; set; }
    [JsonProperty] public FilterOrderStrategyKind FilterStrategy { get; set; }
    [JsonProperty] public double MaxDurationMinutes { get; set; }
    [JsonProperty] public bool AllowWaitForSky { get; set; }
    [JsonProperty] public double MaxWaitMinutes { get; set; }
    [JsonProperty] public bool AdaptiveProbeWait { get; set; }
    [JsonProperty] public double MinProbeWaitSeconds { get; set; }
    [JsonProperty] public double MaxProbeWaitSeconds { get; set; }
    [JsonProperty] public string CampaignKey { get; set; }
    [JsonProperty] public bool UseSqm { get; set; }
    [JsonProperty] public MountPointingMode PointingMode { get; set; }
    [JsonProperty] public TrackingMode Tracking { get; set; }
    [JsonProperty] public double TargetAltitudeDegrees { get; set; }
    [JsonProperty] public double TargetAzimuthDegrees { get; set; }
    [JsonProperty] public double SunOffsetDegrees { get; set; }
    [JsonProperty] public bool RestorePointingAtEnd { get; set; }
    [JsonProperty] public bool DitherBetweenFrames { get; set; }
    [JsonProperty] public WhenNoFilterFeasibleAction WhenNoFilterFeasible { get; set; }
    [JsonProperty] public OnFilterErrorAction OnFilterError { get; set; }
    [JsonProperty] public bool SimulationMode { get; set; }

    public string ProgressText { get; private set; } = "Idle";
    public SkyFlatEventContext EventContext { get; } = new();

    public int FlatsRemaining => EventContext.TotalRemaining;
    public int FlatsRequired => EventContext.TotalRequired;
    public int FlatsAccepted => EventContext.TotalAccepted;
    public string CurrentFilter => EventContext.Filter;
    public string LastStopReason => EventContext.StopReason;

    private SkyFlatEventContainer NewEvent(SkyFlatEventType type) => new(type, this);

    private SkyFlatEventContainer CloneEvent(SkyFlatEventContainer source)
    {
        var clone = (SkyFlatEventContainer)source.Clone();
        clone.ResetParent(this);
        return clone;
    }

    public override void Initialize()
    {
        foreach (var c in EventContainers()) c.Initialize();
        base.Initialize();
    }

    public override void ResetProgress()
    {
        foreach (var c in EventContainers()) c.ResetProgress();
        ProgressText = "Idle";
        RaisePropertyChanged(nameof(ProgressText));
        base.ResetProgress();
    }

    public override void AfterParentChanged()
    {
        base.AfterParentChanged();
        foreach (var c in EventContainers()) c.ResetParent(this);
    }

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
            _profileService, _cameraMediator, _filterWheelMediator, _telescopeMediator,
            _imagingMediator, _imageSaveMediator, _imageHistoryVM, _weatherDataMediator,
            UseSqm, SimulationMode, m => Logger.Info($"[{PluginIdentity.ShortName}] {m}"));

        _activeProgress = progress;
        EventContext.CampaignKey = string.IsNullOrWhiteSpace(CampaignKey) ? "default" : CampaignKey;
        EventContext.Mode = Mode.ToString();
        SkyFlatEventContextAccessor.Set(EventContext);

        try
        {
            var request = new SkyFlatSessionRequest
            {
                CampaignKey = EventContext.CampaignKey,
                ProfileId = _profileService.ActiveProfile.Id.ToString(),
                Mode = Mode,
                Strategy = FilterStrategy,
                MaxDurationMinutes = MaxDurationMinutes,
                AllowWaitForSky = AllowWaitForSky,
                MaxWaitMinutes = MaxWaitMinutes,
                AdaptiveProbeWait = AdaptiveProbeWait,
                MinProbeWaitSeconds = Math.Max(1, MinProbeWaitSeconds),
                MaxProbeWaitSeconds = Math.Max(Math.Max(1, MinProbeWaitSeconds), MaxProbeWaitSeconds),
                WhenNoFlatsRequired = WhenNoFlatsRequiredAction.SucceedImmediately,
                WhenNoFilterFeasible = WhenNoFilterFeasible,
                OnFilterError = OnFilterError,
                UseSqm = UseSqm,
                Options = options,
                Filters = filters,
                EventSink = this,
                Pointing = new MountPointingRequest
                {
                    Mode = PointingMode,
                    Tracking = Tracking,
                    AltitudeDegrees = TargetAltitudeDegrees,
                    AzimuthDegrees = TargetAzimuthDegrees,
                    SunOffsetDegrees = SunOffsetDegrees,
                    MinSunSeparationDegrees = Settings.Default.SunSafetySeparationDegrees,
                    RestoreAtEnd = RestorePointingAtEnd,
                    DitherBetweenFrames = DitherBetweenFrames
                }
            };

            var progressAdapter = new Progress<SkyFlatSessionProgress>(p =>
            {
                EventContext.ApplyProgress(p);
                var levelText = p.MeasuredHistogramFraction is { } frac
                    ? $"{frac * 100.0:F1}%/{p.MeasuredAdu:F0}ADU"
                    : "n/a";
                ProgressText = $"{p.State}: {p.CurrentFilter} level={levelText} exp={p.ExposureSeconds:F3}s rem={p.Remaining} — {p.StatusMessage}";
                RaisePropertyChanged(nameof(ProgressText));
                RaiseContextProperties();
                progress?.Report(new ApplicationStatus { Status = $"[{PluginIdentity.ShortName}] {ProgressText}" });
                _applicationStatusMediator.StatusUpdate(new ApplicationStatus { Source = PluginIdentity.ShortName, Status = ProgressText });
            });

            var result = await runner.RunAsync(request, progressAdapter, token).ConfigureAwait(false);
            EventContext.AcceptedThisSession = result.AcceptedThisSession;
            EventContext.RejectedThisSession = result.RejectedThisSession;
            EventContext.StopReason = result.StopReason;
            EventContext.ApplyCampaign(result.Campaign);
            ProgressText = $"{result.FinalState}: {result.StopReason} (accepted={result.AcceptedThisSession}, rejected={result.RejectedThisSession}, remaining={EventContext.TotalRemaining})";
            RaisePropertyChanged(nameof(ProgressText));
            RaiseContextProperties();

            if (result.FinalState == SessionState.Faulted)
                throw new SequenceEntityFailedException(ProgressText);
        }
        finally
        {
            _activeProgress = null;
            SkyFlatEventContextAccessor.Clear(EventContext);
        }
    }

    public async Task OnEventAsync(SkyFlatSessionEvent sessionEvent, CancellationToken cancellationToken)
    {
        EventContext.ApplyEvent(sessionEvent);

        var container = sessionEvent.Kind switch
        {
            SkyFlatSessionEventKind.CampaignRequired => CampaignRequiredContainer,
            SkyFlatSessionEventKind.CampaignNotRequired => CampaignNotRequiredContainer,
            SkyFlatSessionEventKind.BeforeWait => BeforeWaitContainer,
            SkyFlatSessionEventKind.AfterWait => AfterWaitContainer,
            SkyFlatSessionEventKind.BeforeFilter => BeforeFilterContainer,
            SkyFlatSessionEventKind.AfterFilter => AfterFilterContainer,
            SkyFlatSessionEventKind.CampaignCompleted => CampaignCompletedContainer,
            SkyFlatSessionEventKind.SessionIncomplete => SessionIncompleteContainer,
            SkyFlatSessionEventKind.Error => ErrorContainer,
            _ => null
        };

        if (container is null) return;

        var defaultMessage = FormatEventMessage(sessionEvent);
        EventContext.EventMessage = string.IsNullOrWhiteSpace(container.MessageTemplate)
            ? defaultMessage
            : ResolveEventMessageTemplate(container.MessageTemplate, sessionEvent);
        RaiseContextProperties();

        // Ground Station's $INSTRUCTION_SET$ token resolves the direct parent container name.
        // Publishing the contextual event text as this Name gives NINA 3.2 users dynamic SFCM
        // values without taking a dependency on Ground Station or NINA 3.3's symbol broker.
        container.Name = EventContext.EventMessage;
        await ExecuteEventContainer(container, _activeProgress, cancellationToken).ConfigureAwait(false);
    }

    private string ResolveEventMessageTemplate(string template, SkyFlatSessionEvent e)
    {
        var culture = CultureInfo.InvariantCulture;
        var filter = e.CurrentFilter ?? EventContext.Filter;
        var filterProgress = !string.IsNullOrWhiteSpace(filter)
            && e.Campaign?.Filters.TryGetValue(filter, out var fp) == true ? fp : null;

        return template
            .Replace("{campaign}", e.CampaignKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{mode}", e.Mode.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{state}", e.Kind.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{remaining}", e.Remaining.ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{required}", e.ConfiguredTarget.ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{accepted}", (e.Campaign?.TotalAccepted ?? EventContext.TotalAccepted).ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{sessionAccepted}", e.AcceptedThisSession.ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{sessionRejected}", e.RejectedThisSession.ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{filter}", filter ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{filterRemaining}", (filterProgress?.Remaining ?? 0).ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{filterAccepted}", (filterProgress?.Accepted ?? 0).ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{filterRequired}", (filterProgress?.Target ?? 0).ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{exposure}", (e.ExposureSeconds ?? 0).ToString("0.###", culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{adu}", (e.MeasuredAdu ?? 0).ToString("0", culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{histogram}", ((e.MeasuredHistogramFraction ?? 0) * 100.0).ToString("0.0", culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{sunAltitude}", (e.SunAltitudeDegrees ?? double.NaN).ToString("0.00", culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{waitReason}", e.WaitReason ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{stopReason}", e.StopReason ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{duration}", e.Duration.ToString(@"hh\:mm\:ss", culture), StringComparison.OrdinalIgnoreCase);
    }

    private string FormatEventMessage(SkyFlatSessionEvent e)
    {
        var mode = e.Mode == CampaignMode.Automatic ? "Sky" : e.Mode.ToString();
        var filter = e.CurrentFilter ?? EventContext.Filter;
        var filterRemaining = 0;
        if (!string.IsNullOrWhiteSpace(filter) && e.Campaign?.Filters.TryGetValue(filter, out var fp) == true)
            filterRemaining = fp.Remaining;

        return e.Kind switch
        {
            SkyFlatSessionEventKind.CampaignRequired =>
                $"SFCM: {mode} sky flats starting — {e.Remaining} flat(s) remaining",
            SkyFlatSessionEventKind.CampaignNotRequired =>
                "SFCM: sky flats skipped — campaign is current (0 remaining)",
            SkyFlatSessionEventKind.BeforeWait =>
                $"SFCM: waiting for twilight{(string.IsNullOrWhiteSpace(filter) ? "" : $" — next {filter}")} — {e.Remaining} remaining",
            SkyFlatSessionEventKind.AfterWait =>
                $"SFCM: twilight ready — resuming{(string.IsNullOrWhiteSpace(filter) ? "" : $" with {filter}")} — {e.Remaining} remaining",
            SkyFlatSessionEventKind.BeforeFilter =>
                $"SFCM: starting {filter} — {filterRemaining} for this filter, {e.Remaining} total remaining",
            SkyFlatSessionEventKind.AfterFilter =>
                $"SFCM: {filter} complete — {e.Remaining} total remaining",
            SkyFlatSessionEventKind.CampaignCompleted =>
                $"SFCM: sky flat campaign complete — {e.Campaign?.TotalAccepted ?? EventContext.TotalAccepted} accepted, 0 remaining",
            SkyFlatSessionEventKind.SessionIncomplete =>
                $"SFCM: sky-flat session ended incomplete — {e.Remaining} remaining — {e.StopReason ?? "unknown reason"}",
            SkyFlatSessionEventKind.Error =>
                $"SFCM error: {e.StatusMessage ?? e.Exception?.Message ?? "unknown error"}",
            _ => $"SFCM: {e.Kind}"
        };
    }

    private async Task ExecuteEventContainer(
        SkyFlatEventContainer container,
        IProgress<ApplicationStatus>? progress,
        CancellationToken token)
    {
        if (container.Items?.Count <= 0) return;
        container.ResetParent(this);
        container.ResetProgress();
        Logger.Info($"[{PluginIdentity.ShortName}] Event container '{container.Name}' starting");
        await container.Execute(progress!, token).ConfigureAwait(false);
        Logger.Info($"[{PluginIdentity.ShortName}] Event container '{container.Name}' finished");
    }

    private IEnumerable<SkyFlatEventContainer> EventContainers()
    {
        yield return CampaignRequiredContainer;
        yield return CampaignNotRequiredContainer;
        yield return BeforeWaitContainer;
        yield return AfterWaitContainer;
        yield return BeforeFilterContainer;
        yield return AfterFilterContainer;
        yield return CampaignCompletedContainer;
        yield return SessionIncompleteContainer;
        yield return ErrorContainer;
    }

    private void RaiseContextProperties()
    {
        RaisePropertyChanged(nameof(FlatsRemaining));
        RaisePropertyChanged(nameof(FlatsRequired));
        RaisePropertyChanged(nameof(FlatsAccepted));
        RaisePropertyChanged(nameof(CurrentFilter));
        RaisePropertyChanged(nameof(LastStopReason));
        RaisePropertyChanged(nameof(EventMessage));
    }

    public string EventMessage => EventContext.EventMessage;

    public override object Clone() => new SkyFlatCampaignContainer(this);

    public override string ToString()
        => $"Category: {Category}, Item: {nameof(SkyFlatCampaignContainer)}, Mode={Mode}, Key={CampaignKey}";
}
