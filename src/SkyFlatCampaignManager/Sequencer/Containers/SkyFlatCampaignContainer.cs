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
[ExportMetadata("Description", "Runs a sky-flat campaign and exposes blocking custom event containers at important campaign transitions.")]
[ExportMetadata("Icon", "BrightnessSVG")]
[ExportMetadata("Category", "Sky Flat Campaign Manager")]
[Export(typeof(ISequenceItem))]
[Export(typeof(ISequenceContainer))]
[JsonObject(MemberSerialization.OptIn)]
public sealed class SkyFlatCampaignContainer : SequentialContainer
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
        Strategy = FilterOrderStrategyKind.Adaptive;
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
        Strategy = copyMe.Strategy;
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
    [JsonProperty] public FilterOrderStrategyKind Strategy { get; set; }
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

        var started = DateTime.UtcNow;
        var key = string.IsNullOrWhiteSpace(CampaignKey) ? "default" : CampaignKey;
        var options = PluginServiceFactory.CreateOptionsFromSettings();
        options.SimulationMode = SimulationMode;
        options.DryRun = options.DryRun || SimulationMode;
        var filters = PluginServiceFactory.CreateFilterSettings(_profileService);
        var campaigns = PluginServiceFactory.CreateCampaignService();

        EventContext.CampaignKey = key;
        EventContext.Mode = Mode.ToString();
        SkyFlatEventContextAccessor.Set(EventContext);

        try
        {
            var requirement = await campaigns.EvaluateRequirementAsync(key, options, token).ConfigureAwait(false);
            EventContext.TotalRequired = CampaignMetrics.ConfiguredTarget(filters);
            EventContext.TotalRemaining = CampaignMetrics.FlatsRemaining(requirement, filters);
            EventContext.TotalAccepted = requirement.Campaign?.TotalAccepted ?? 0;
            EventContext.State = requirement.IsRequired ? "CampaignRequired" : "CampaignNotRequired";
            EventContext.StopReason = requirement.Reason;

            if (!requirement.IsRequired)
            {
                await ExecuteEventContainer(CampaignNotRequiredContainer, progress, token).ConfigureAwait(false);
                ProgressText = "Skipped: campaign already complete";
                RaisePropertyChanged(nameof(ProgressText));
                return;
            }

            await ExecuteEventContainer(CampaignRequiredContainer, progress, token).ConfigureAwait(false);

            var runner = PluginServiceFactory.CreateRunner(
                _profileService, _cameraMediator, _filterWheelMediator, _telescopeMediator,
                _imagingMediator, _imageSaveMediator, _imageHistoryVM, _weatherDataMediator,
                UseSqm, SimulationMode, m => Logger.Info($"[{PluginIdentity.ShortName}] {m}"));

            var bridge = new EventProgressBridge(this, campaigns, filters, progress, token);
            var request = new SkyFlatSessionRequest
            {
                CampaignKey = key,
                ProfileId = _profileService.ActiveProfile.Id.ToString(),
                Mode = Mode,
                Strategy = Strategy,
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

            var result = await runner.RunAsync(request, bridge, token).ConfigureAwait(false);
            bridge.FlushPendingFilterCompletion();
            bridge.EndWaitEpisodeIfNeeded();

            EventContext.AcceptedThisSession = result.AcceptedThisSession;
            EventContext.RejectedThisSession = result.RejectedThisSession;
            EventContext.StopReason = result.StopReason;
            EventContext.Duration = DateTime.UtcNow - started;
            EventContext.ApplyCampaign(result.Campaign);

            ProgressText = $"{result.FinalState}: {result.StopReason} (accepted={result.AcceptedThisSession}, rejected={result.RejectedThisSession}, remaining={EventContext.TotalRemaining})";
            RaisePropertyChanged(nameof(ProgressText));

            if (result.Campaign?.IsComplete == true || EventContext.TotalRemaining == 0)
                await ExecuteEventContainer(CampaignCompletedContainer, progress, token).ConfigureAwait(false);
            else if (result.IsPartialSuccess || EventContext.TotalRemaining > 0)
                await ExecuteEventContainer(SessionIncompleteContainer, progress, token).ConfigureAwait(false);

            if (result.FinalState == SessionState.Faulted)
                throw new SequenceEntityFailedException(ProgressText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            EventContext.State = "Faulted";
            EventContext.StopReason = ex.Message;
            EventContext.Duration = DateTime.UtcNow - started;
            try
            {
                await ExecuteEventContainer(ErrorContainer, progress, token).ConfigureAwait(false);
            }
            catch (Exception hookEx)
            {
                Logger.Error(hookEx);
            }
            throw;
        }
        finally
        {
            SkyFlatEventContextAccessor.Clear(EventContext);
        }
    }

    private async Task ExecuteEventContainer(SkyFlatEventContainer container, IProgress<ApplicationStatus> progress, CancellationToken token)
    {
        if (container.Items?.Count <= 0) return;
        container.ResetParent(this);
        container.ResetProgress();
        Logger.Info($"[{PluginIdentity.ShortName}] Event container '{container.Name}' starting");
        await container.Execute(progress, token).ConfigureAwait(false);
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

    public override object Clone() => new SkyFlatCampaignContainer(this);

    public override string ToString()
        => $"Category: {Category}, Item: {nameof(SkyFlatCampaignContainer)}, Mode={Mode}, Key={CampaignKey}";

    private sealed class EventProgressBridge : IProgress<SkyFlatSessionProgress>
    {
        private readonly SkyFlatCampaignContainer owner;
        private readonly ICampaignService campaigns;
        private readonly IReadOnlyList<FilterCampaignSettings> filters;
        private readonly IProgress<ApplicationStatus> outerProgress;
        private readonly CancellationToken token;
        private bool waiting;
        private string? activeFilter;
        private readonly HashSet<string> completedFilterHooks = new(StringComparer.OrdinalIgnoreCase);

        public EventProgressBridge(
            SkyFlatCampaignContainer owner,
            ICampaignService campaigns,
            IReadOnlyList<FilterCampaignSettings> filters,
            IProgress<ApplicationStatus> outerProgress,
            CancellationToken token)
        {
            this.owner = owner;
            this.campaigns = campaigns;
            this.filters = filters;
            this.outerProgress = outerProgress;
            this.token = token;
        }

        public void Report(SkyFlatSessionProgress value)
        {
            token.ThrowIfCancellationRequested();
            owner.EventContext.ApplyProgress(value);

            var levelText = value.MeasuredHistogramFraction is { } frac
                ? $"{frac * 100.0:F1}%/{value.MeasuredAdu:F0}ADU"
                : "n/a";
            owner.ProgressText = $"{value.State}: {value.CurrentFilter} level={levelText} exp={value.ExposureSeconds:F3}s rem={value.Remaining} — {value.StatusMessage}";
            owner.RaisePropertyChanged(nameof(ProgressText));
            outerProgress?.Report(new ApplicationStatus { Status = $"[{PluginIdentity.ShortName}] {owner.ProgressText}" });
            owner._applicationStatusMediator.StatusUpdate(new ApplicationStatus { Source = PluginIdentity.ShortName, Status = owner.ProgressText });

            if (value.State == SessionState.WaitingForAstronomicalWindow)
            {
                if (!waiting)
                {
                    RefreshCampaign();
                    owner.ExecuteEventContainer(owner.BeforeWaitContainer, outerProgress, token).GetAwaiter().GetResult();
                    waiting = true;
                }
                return;
            }

            if (waiting)
            {
                RefreshCampaign();
                owner.ExecuteEventContainer(owner.AfterWaitContainer, outerProgress, token).GetAwaiter().GetResult();
                waiting = false;
            }

            if (value.State == SessionState.SelectingFilter || value.State == SessionState.Completed)
                FlushPendingFilterCompletion();

            if (value.State == SessionState.EstimatingExposure
                && !string.IsNullOrWhiteSpace(value.CurrentFilter)
                && !string.Equals(activeFilter, value.CurrentFilter, StringComparison.OrdinalIgnoreCase))
            {
                activeFilter = value.CurrentFilter;
                RefreshCampaign();
                owner.ExecuteEventContainer(owner.BeforeFilterContainer, outerProgress, token).GetAwaiter().GetResult();
            }
        }

        public void EndWaitEpisodeIfNeeded()
        {
            if (!waiting) return;
            RefreshCampaign();
            owner.ExecuteEventContainer(owner.AfterWaitContainer, outerProgress, token).GetAwaiter().GetResult();
            waiting = false;
        }

        public void FlushPendingFilterCompletion()
        {
            if (string.IsNullOrWhiteSpace(activeFilter) || completedFilterHooks.Contains(activeFilter)) return;
            var campaign = campaigns.GetOrCreateAsync(owner.EventContext.CampaignKey,
                owner._profileService.ActiveProfile.Id.ToString(), filters,
                PluginServiceFactory.CreateOptionsFromSettings(), token).GetAwaiter().GetResult();
            owner.EventContext.ApplyCampaign(campaign);
            owner.EventContext.Filter = activeFilter;
            owner.EventContext.ApplyCampaign(campaign);
            if (!campaign.Filters.TryGetValue(activeFilter, out var fp) || fp.Remaining > 0) return;

            completedFilterHooks.Add(activeFilter);
            owner.ExecuteEventContainer(owner.AfterFilterContainer, outerProgress, token).GetAwaiter().GetResult();
        }

        private void RefreshCampaign()
        {
            var campaign = campaigns.GetOrCreateAsync(owner.EventContext.CampaignKey,
                owner._profileService.ActiveProfile.Id.ToString(), filters,
                PluginServiceFactory.CreateOptionsFromSettings(), token).GetAwaiter().GetResult();
            owner.EventContext.ApplyCampaign(campaign);
        }
    }
}
