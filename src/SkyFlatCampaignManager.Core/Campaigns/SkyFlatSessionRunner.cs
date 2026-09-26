using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Astronomy;
using SkyFlatCampaignManager.Core.Brightness;
using SkyFlatCampaignManager.Core.Equipment;
using SkyFlatCampaignManager.Core.Errors;
using SkyFlatCampaignManager.Core.Filters;
using SkyFlatCampaignManager.Core.Notifications;
using SkyFlatCampaignManager.Core.Utilities;

namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class SkyFlatSessionRequest
{
    public string CampaignKey { get; init; } = "default";
    public string ProfileId { get; init; } = "profile";
    public CampaignMode Mode { get; init; } = CampaignMode.Automatic;
    public FilterOrderStrategyKind Strategy { get; init; } = FilterOrderStrategyKind.Adaptive;
    public double MaxDurationMinutes { get; init; } = 90;
    public bool AllowWaitForSky { get; init; } = true;
    public double MaxWaitMinutes { get; init; } = 45;
    public bool AdaptiveProbeWait { get; init; } = true;
    public double MinProbeWaitSeconds { get; init; } = 5;
    public double MaxProbeWaitSeconds { get; init; } = 30;
    public WhenNoFlatsRequiredAction WhenNoFlatsRequired { get; init; } = WhenNoFlatsRequiredAction.SucceedImmediately;
    public WhenNoFilterFeasibleAction WhenNoFilterFeasible { get; init; } = WhenNoFilterFeasibleAction.Wait;
    public OnFilterErrorAction OnFilterError { get; init; } = OnFilterErrorAction.ContinueNextFilter;
    public bool UseSqm { get; init; }
    public MountPointingRequest Pointing { get; init; } = new();
    public CampaignOptions Options { get; init; } = new();
    public IReadOnlyList<FilterCampaignSettings> Filters { get; init; } = Array.Empty<FilterCampaignSettings>();
    public ISkyFlatSessionEventSink? EventSink { get; init; }
}

public sealed class SkyFlatSessionProgress
{
    public SessionState State { get; set; } = SessionState.Idle;
    public string? CurrentFilter { get; set; }
    public int Accepted { get; set; }
    public int Remaining { get; set; }
    public double? MeasuredAdu { get; set; }
    public double? MeasuredHistogramFraction { get; set; }
    public double? ExposureSeconds { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string? WaitReason { get; set; }
    public string? StopReason { get; set; }
}

public sealed class SkyFlatSessionResult
{
    public SessionState FinalState { get; init; }
    public string StopReason { get; init; } = string.Empty;
    public CampaignState? Campaign { get; init; }
    public int AcceptedThisSession { get; init; }
    public int RejectedThisSession { get; init; }
    public bool IsPartialSuccess => FinalState is SessionState.StoppedByWindow or SessionState.StoppedByTimeout;
    public bool IsSuccess => FinalState is SessionState.Completed || IsPartialSuccess;
}

public sealed class SkyFlatSessionRunner
{
    private const double EveningSkyTrendFactor = 1.05;
    private const double MorningSkyTrendFactor = 0.95;
    private static readonly TimeSpan WindowPollInterval = TimeSpan.FromSeconds(2);

    private readonly ICampaignService _campaigns;
    private readonly ICameraAcquisitionService _camera;
    private readonly IFilterWheelService _filterWheel;
    private readonly IMountPositioningService _mount;
    private readonly IFlatExposureEstimator _estimator;
    private readonly IFlatFrameValidator _validator;
    private readonly IAstronomicalWindowService _windows;
    private readonly ISunAltitudeProvider _sun;
    private readonly ISkyBrightnessProvider _brightness;
    private readonly INotificationService _notifications;
    private readonly IClock _clock;
    private readonly Action<string>? _log;

    public SkyFlatSessionRunner(ICampaignService campaigns, ICameraAcquisitionService camera,
        IFilterWheelService filterWheel, IMountPositioningService mount,
        IFlatExposureEstimator estimator, IFlatFrameValidator validator,
        IAstronomicalWindowService windows, ISunAltitudeProvider sun,
        ISkyBrightnessProvider brightness, INotificationService notifications,
        IClock clock, Action<string>? log = null)
    {
        _campaigns = campaigns; _camera = camera; _filterWheel = filterWheel; _mount = mount;
        _estimator = estimator; _validator = validator; _windows = windows; _sun = sun;
        _brightness = brightness; _notifications = notifications; _clock = clock; _log = log;
    }

    public async Task<SkyFlatSessionResult> RunAsync(SkyFlatSessionRequest request,
        IProgress<SkyFlatSessionProgress>? progress, CancellationToken cancellationToken)
    {
        var p = new SkyFlatSessionProgress();
        var accepted = 0;
        var rejected = 0;
        var started = _clock.UtcNow;
        var previousAltitude = _sun.GetSunAltitudeDegrees(started);
        string? currentFilter = null;
        var rejectionStreak = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sessionEstimates = new Dictionary<string, ExposureEstimateResult>(StringComparer.OrdinalIgnoreCase);
        var unavailableFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var windowWait = new WaitTracker(_clock);
        var filterWait = new WaitTracker(_clock);
        var waitEpisodeActive = false;
        string? eventActiveFilter = null;
        var completedFilterEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Report(SessionState state, string message, string? wait = null, string? stop = null)
        {
            p.State = state; p.StatusMessage = message; p.WaitReason = wait; p.StopReason = stop;
            p.CurrentFilter = currentFilter; progress?.Report(p);
            if (request.Options.DetailedLogging) _log?.Invoke($"[{state}] {message}");
        }

        async Task EmitAsync(
            SkyFlatSessionEventKind kind,
            CampaignMode eventMode,
            CampaignState? eventCampaign,
            string? filter = null,
            string? waitReason = null,
            string? stopReason = null,
            string? message = null,
            double? sunAltitude = null,
            double? exposure = null,
            int? remainingOverride = null,
            Exception? exception = null)
        {
            if (request.EventSink is null) return;
            await request.EventSink.OnEventAsync(new SkyFlatSessionEvent
            {
                Kind = kind,
                CampaignKey = request.CampaignKey,
                Mode = eventMode,
                Campaign = eventCampaign,
                CurrentFilter = filter ?? currentFilter,
                WaitReason = waitReason,
                StopReason = stopReason,
                StatusMessage = message,
                ConfiguredTarget = CampaignMetrics.ConfiguredTarget(request.Filters),
                Remaining = remainingOverride ?? eventCampaign?.TotalRemaining ?? p.Remaining,
                AcceptedThisSession = accepted,
                RejectedThisSession = rejected,
                ExposureSeconds = exposure ?? p.ExposureSeconds,
                MeasuredAdu = p.MeasuredAdu,
                MeasuredHistogramFraction = p.MeasuredHistogramFraction,
                SunAltitudeDegrees = sunAltitude,
                Duration = _clock.UtcNow - started,
                Exception = exception
            }, cancellationToken).ConfigureAwait(false);
        }

        ExposureEstimateResult Estimate(FilterCampaignSettings filter, CampaignState campaign, CampaignMode mode)
        {
            if (sessionEstimates.TryGetValue(filter.FilterName, out var cached)) return cached;
            if (campaign.Filters.TryGetValue(filter.FilterName, out var fp)
                && fp.LastExposureSeconds is { } lastExp && lastExp > 0
                && fp.LastMeasuredAdu is { } lastAdu && lastAdu > 0)
            {
                var target = filter.TargetHistogramFraction * PluginIdentity.LegacyMigrationMaxAdu;
                var trend = mode == CampaignMode.Evening ? EveningSkyTrendFactor : MorningSkyTrendFactor;
                return _estimator.Estimate(lastExp, lastAdu, target, filter.MinExposureSeconds, filter.MaxExposureSeconds, trend);
            }
            var guess = Math.Clamp(1.0, filter.MinExposureSeconds, filter.MaxExposureSeconds);
            return new ExposureEstimateResult { UnclampedExposureSeconds = guess, ClampedExposureSeconds = guess, Feasibility = ExposureFeasibility.Feasible };
        }

        TimeSpan ProbeDelay(IReadOnlyList<FilterCampaignSettings> candidates,
            IReadOnlyDictionary<string, ExposureEstimateResult> estimates)
        {
            var min = Math.Max(1, request.MinProbeWaitSeconds);
            var max = Math.Max(min, request.MaxProbeWaitSeconds);
            if (!request.AdaptiveProbeWait || candidates.Count == 0) return TimeSpan.FromSeconds(min);

            // Frontier = the incomplete filter closest to crossing into the feasible exposure band.
            // ratio=1 means exactly on a boundary; farther away means there is less value in probing
            // every few seconds. The logarithmic scale remains responsive near the boundary while
            // naturally backing off toward the configured maximum for distant filters.
            var distance = double.MaxValue;
            foreach (var f in candidates)
            {
                var e = estimates[f.FilterName];
                var u = Math.Max(0.000001, e.UnclampedExposureSeconds);
                double d = e.Feasibility switch
                {
                    ExposureFeasibility.TooShort => Math.Max(0, Math.Log(Math.Max(1, f.MinExposureSeconds / u), 2)),
                    ExposureFeasibility.TooLong => Math.Max(0, Math.Log(Math.Max(1, u / Math.Max(0.000001, f.MaxExposureSeconds)), 2)),
                    _ => 0
                };
                distance = Math.Min(distance, d);
            }
            if (double.IsInfinity(distance) || distance == double.MaxValue) distance = 0;
            var normalized = Math.Clamp(distance / 3.0, 0, 1); // 8x from boundary reaches max wait.
            return TimeSpan.FromSeconds(min + (max - min) * normalized);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(SessionState.CheckingCampaign, "Evaluating campaign requirement");
            var requirement = await _campaigns.EvaluateRequirementAsync(request.CampaignKey, request.Options, cancellationToken).ConfigureAwait(false);
            var requirementRemaining = CampaignMetrics.FlatsRemaining(requirement, request.Filters);
            if (!requirement.IsRequired && request.WhenNoFlatsRequired == WhenNoFlatsRequiredAction.SucceedImmediately)
            {
                await EmitAsync(SkyFlatSessionEventKind.CampaignNotRequired, request.Mode, requirement.Campaign,
                    stopReason: requirement.Reason, message: "No flats required", remainingOverride: requirementRemaining).ConfigureAwait(false);
                Report(SessionState.Completed, "No flats required", stop: requirement.Reason);
                return Result(SessionState.Completed, requirement.Reason, requirement.Campaign, 0, 0);
            }

            await EmitAsync(SkyFlatSessionEventKind.CampaignRequired, request.Mode, requirement.Campaign,
                stopReason: requirement.Reason, message: $"{requirementRemaining} flats required",
                remainingOverride: requirementRemaining).ConfigureAwait(false);

            var campaign = await _campaigns.GetOrCreateAsync(request.CampaignKey, request.ProfileId, request.Filters, request.Options, cancellationToken).ConfigureAwait(false);
            p.Accepted = campaign.TotalAccepted; p.Remaining = campaign.TotalRemaining;
            Report(SessionState.CheckingEquipment, "Checking equipment");
            if (!_camera.IsConnected) throw new PluginError("Camera is not connected.", ErrorCategory.NonRecoverable);
            if (!_filterWheel.IsConnected) throw new PluginError("Filter wheel is not connected.", ErrorCategory.NonRecoverable);

            var mode = request.Mode;
            if (mode == CampaignMode.Automatic)
                mode = _windows.ResolveMode(CampaignMode.Automatic, _sun.GetSunAltitudeDegrees(_clock.UtcNow), previousAltitude);

            Report(SessionState.CheckingEquipment, $"Mode={mode}; positioning mount if required");
            if (!request.Options.DryRun) await _mount.EnsureSafePointingAsync(request.Pointing, cancellationToken).ConfigureAwait(false);
            var strategy = FilterSelectionStrategyFactory.Create(request.Strategy);

            while (!cancellationToken.IsCancellationRequested)
            {
                if ((_clock.UtcNow - started).TotalMinutes >= request.MaxDurationMinutes)
                {
                    Report(SessionState.StoppedByTimeout, "Max duration reached", stop: SessionStopReasons.MaxDuration);
                    await EmitAsync(SkyFlatSessionEventKind.SessionIncomplete, mode, campaign,
                        stopReason: SessionStopReasons.MaxDuration, message: "Max duration reached").ConfigureAwait(false);
                    return Result(SessionState.StoppedByTimeout, SessionStopReasons.MaxDuration, campaign, accepted, rejected);
                }

                campaign = await _campaigns.GetOrCreateAsync(request.CampaignKey, request.ProfileId, request.Filters, request.Options, cancellationToken).ConfigureAwait(false);
                p.Accepted = campaign.TotalAccepted; p.Remaining = campaign.TotalRemaining;
                if (campaign.IsComplete || campaign.TotalRemaining <= 0)
                {
                    campaign = await _campaigns.MarkCompletedAsync(request.CampaignKey, request.Options, cancellationToken).ConfigureAwait(false);
                    Report(SessionState.Completed, "Campaign complete", stop: SessionStopReasons.Completed);
                    await EmitAsync(SkyFlatSessionEventKind.CampaignCompleted, mode, campaign,
                        stopReason: SessionStopReasons.Completed, message: "Campaign complete").ConfigureAwait(false);
                    _notifications.Success("Sky flat campaign completed.");
                    return Result(SessionState.Completed, SessionStopReasons.Completed, campaign, accepted, rejected);
                }

                var sunAlt = _sun.GetSunAltitudeDegrees(_clock.UtcNow);
                mode = _windows.ResolveMode(mode, sunAlt, previousAltitude); previousAltitude = sunAlt;
                var window = mode == CampaignMode.Morning ? request.Options.MorningWindow : request.Options.EveningWindow;
                var windowState = _windows.Evaluate(mode, sunAlt, window);
                if (windowState != AstronomicalWindowState.Open)
                {
                    if (windowState == AstronomicalWindowState.TooEarly)
                    {
                        if (request.AllowWaitForSky)
                        {
                            var elapsed = windowWait.ElapsedMinutes(SessionWaitReasons.AstronomicalWindowNotOpenYet);
                            if (elapsed < request.MaxWaitMinutes)
                            {
                                var waitMessage = $"Sun altitude {sunAlt:F1}° has not reached the {mode} window [{window.MinSunAltitudeDegrees:F1}°, {window.MaxSunAltitudeDegrees:F1}°] yet";
                                Report(SessionState.WaitingForAstronomicalWindow, waitMessage,
                                    SessionWaitReasons.AstronomicalWindowNotOpenYet);
                                if (!waitEpisodeActive)
                                {
                                    await EmitAsync(SkyFlatSessionEventKind.BeforeWait, mode, campaign,
                                        waitReason: SessionWaitReasons.AstronomicalWindowNotOpenYet,
                                        message: waitMessage, sunAltitude: sunAlt).ConfigureAwait(false);
                                    waitEpisodeActive = true;
                                }
                                await Task.Delay(WindowPollInterval, cancellationToken).ConfigureAwait(false);
                                continue;
                            }
                            Report(SessionState.StoppedByWindow, "Waited for the astronomical window to open, but it did not open in time", stop: SessionStopReasons.AstronomicalWindowWaitTimeout);
                            await EmitAsync(SkyFlatSessionEventKind.SessionIncomplete, mode, campaign,
                                waitReason: SessionWaitReasons.AstronomicalWindowNotOpenYet,
                                stopReason: SessionStopReasons.AstronomicalWindowWaitTimeout,
                                message: "Astronomical window wait timed out", sunAltitude: sunAlt).ConfigureAwait(false);
                            return Result(SessionState.StoppedByWindow, SessionStopReasons.AstronomicalWindowWaitTimeout, campaign, accepted, rejected);
                        }
                        Report(SessionState.StoppedByWindow, $"Astronomical window not open yet (sun altitude {sunAlt:F1}°) and waiting is disabled", stop: SessionStopReasons.AstronomicalWindowNotOpenYet);
                        await EmitAsync(SkyFlatSessionEventKind.SessionIncomplete, mode, campaign,
                            stopReason: SessionStopReasons.AstronomicalWindowNotOpenYet,
                            message: "Astronomical window not open and waiting disabled", sunAltitude: sunAlt).ConfigureAwait(false);
                        return Result(SessionState.StoppedByWindow, SessionStopReasons.AstronomicalWindowNotOpenYet, campaign, accepted, rejected);
                    }

                    var reason = mode == CampaignMode.Evening ? SessionStopReasons.EveningSkyTooDark : SessionStopReasons.MorningSkyTooBright;
                    var message = mode == CampaignMode.Evening
                        ? $"Sun altitude {sunAlt:F1}° is below the evening window minimum ({window.MinSunAltitudeDegrees:F1}°) — sky is already too dark for flats."
                        : $"Sun altitude {sunAlt:F1}° is above the morning window maximum ({window.MaxSunAltitudeDegrees:F1}°) — sky is already too bright for flats.";
                    Report(SessionState.StoppedByWindow, message, stop: reason);
                    await EmitAsync(SkyFlatSessionEventKind.SessionIncomplete, mode, campaign,
                        stopReason: reason, message: message, sunAltitude: sunAlt).ConfigureAwait(false);
                    return Result(SessionState.StoppedByWindow, reason, campaign, accepted, rejected);
                }
                windowWait.Reset();

                Report(SessionState.SelectingFilter, "Selecting next filter");
                var incomplete = request.Filters.Where(f => f.Enabled && !unavailableFilters.Contains(f.FilterName)
                    && campaign.Filters.TryGetValue(f.FilterName, out var fp) && fp.IsIncomplete).ToList();
                if (incomplete.Count == 0)
                {
                    Report(SessionState.Completed, "No incomplete filters", stop: SessionStopReasons.NoFilters);
                    return Result(SessionState.Completed, SessionStopReasons.NoFilters, campaign, accepted, rejected);
                }

                var estimates = incomplete.ToDictionary(f => f.FilterName, f => Estimate(f, campaign, mode), StringComparer.OrdinalIgnoreCase);
                var feasible = incomplete.Where(f => estimates[f.FilterName].Feasibility == ExposureFeasibility.Feasible).ToList();
                FilterCampaignSettings? next = null;
                if (feasible.Count > 0)
                {
                    next = strategy.SelectNext(feasible, campaign, mode, new FilterSelectionContext
                    {
                        CurrentSunAltitudeDegrees = sunAlt,
                        CurrentFilterName = currentFilter,
                        EstimatedExposureSecondsByFilter = feasible.ToDictionary(f => f.FilterName, f => estimates[f.FilterName].ClampedExposureSeconds, StringComparer.OrdinalIgnoreCase)
                    });
                }

                if (next is null)
                {
                    var improving = incomplete.Where(f => ExposureFeasibilityRules.CanImproveByWaiting(mode, estimates[f.FilterName].Feasibility)).ToList();
                    var shouldWait = improving.Count > 0 && request.AllowWaitForSky && request.WhenNoFilterFeasible == WhenNoFilterFeasibleAction.Wait;
                    if (shouldWait)
                    {
                        var elapsed = filterWait.ElapsedMinutes(SessionWaitReasons.FilterNotFeasible);
                        if (elapsed < request.MaxWaitMinutes)
                        {
                            var delay = ProbeDelay(improving, estimates);
                            var frontier = improving.OrderBy(f => Math.Abs(Math.Log(Math.Max(0.000001, estimates[f.FilterName].UnclampedExposureSeconds) /
                                Math.Max(0.000001, estimates[f.FilterName].Feasibility == ExposureFeasibility.TooShort ? f.MinExposureSeconds : f.MaxExposureSeconds)))).First();
                            var waitMessage = $"No filter feasible yet; frontier={frontier.FilterName} ({ExposureFeasibilityRules.Describe(mode, estimates[frontier.FilterName].Feasibility)}), retry in {delay.TotalSeconds:F0}s";
                            currentFilter = frontier.FilterName;
                            p.ExposureSeconds = estimates[frontier.FilterName].ClampedExposureSeconds;
                            Report(SessionState.WaitingForAstronomicalWindow, waitMessage,
                                SessionWaitReasons.FilterNotFeasible);
                            if (!waitEpisodeActive)
                            {
                                await EmitAsync(SkyFlatSessionEventKind.BeforeWait, mode, campaign,
                                    filter: frontier.FilterName, waitReason: SessionWaitReasons.FilterNotFeasible,
                                    message: waitMessage, sunAltitude: sunAlt,
                                    exposure: estimates[frontier.FilterName].ClampedExposureSeconds).ConfigureAwait(false);
                                waitEpisodeActive = true;
                            }
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        Report(SessionState.StoppedByWindow, "Waited for a filter to become exposure-feasible, but none did in time", stop: SessionStopReasons.NoFilterFeasibleWaitTimeout);
                        await EmitAsync(SkyFlatSessionEventKind.SessionIncomplete, mode, campaign,
                            waitReason: SessionWaitReasons.FilterNotFeasible,
                            stopReason: SessionStopReasons.NoFilterFeasibleWaitTimeout,
                            message: "No filter became exposure-feasible in time", sunAltitude: sunAlt).ConfigureAwait(false);
                        return Result(SessionState.StoppedByWindow, SessionStopReasons.NoFilterFeasibleWaitTimeout, campaign, accepted, rejected);
                    }
                    if (request.WhenNoFilterFeasible == WhenNoFilterFeasibleAction.Fail)
                        throw new PluginError($"No filter currently feasible (checked {incomplete.Count} incomplete filter(s)).", ErrorCategory.Session);
                    var detail = string.Join(" ", incomplete.Select(f => $"{f.FilterName}: {ExposureFeasibilityRules.Describe(mode, estimates[f.FilterName].Feasibility)}"));
                    var noFeasibleMessage = $"No incomplete filter is currently exposure-feasible. {detail}";
                    Report(SessionState.StoppedByWindow, noFeasibleMessage, stop: SessionStopReasons.NoFilterFeasible);
                    await EmitAsync(SkyFlatSessionEventKind.SessionIncomplete, mode, campaign,
                        stopReason: SessionStopReasons.NoFilterFeasible,
                        message: noFeasibleMessage, sunAltitude: sunAlt).ConfigureAwait(false);
                    return Result(SessionState.StoppedByWindow, SessionStopReasons.NoFilterFeasible, campaign, accepted, rejected);
                }
                filterWait.Reset();

                if (waitEpisodeActive)
                {
                    await EmitAsync(SkyFlatSessionEventKind.AfterWait, mode, campaign,
                        filter: next.FilterName, message: "Twilight wait complete; resuming flats",
                        sunAltitude: sunAlt, exposure: estimates[next.FilterName].ClampedExposureSeconds).ConfigureAwait(false);
                    waitEpisodeActive = false;
                }

                if (!string.Equals(eventActiveFilter, next.FilterName, StringComparison.OrdinalIgnoreCase))
                {
                    eventActiveFilter = next.FilterName;
                    currentFilter = next.FilterName;
                    p.ExposureSeconds = estimates[next.FilterName].ClampedExposureSeconds;
                    await EmitAsync(SkyFlatSessionEventKind.BeforeFilter, mode, campaign,
                        filter: next.FilterName, message: $"Starting flats for {next.FilterName}",
                        sunAltitude: sunAlt, exposure: estimates[next.FilterName].ClampedExposureSeconds).ConfigureAwait(false);
                }

                CapturedFlatFrame? frame = null;
                try
                {
                    if (!string.Equals(_filterWheel.CurrentFilterName, next.FilterName, StringComparison.OrdinalIgnoreCase))
                    {
                        Report(SessionState.ChangingFilter, $"Changing filter to {next.FilterName}");
                        if (!request.Options.DryRun) await _filterWheel.ChangeFilterAsync(next.FilterName, cancellationToken).ConfigureAwait(false);
                    }
                    currentFilter = next.FilterName;
                    Report(SessionState.EstimatingExposure, $"Estimating exposure for {next.FilterName}");
                    _ = await _brightness.GetSampleAsync(cancellationToken).ConfigureAwait(false);
                    var exposure = estimates[next.FilterName].ClampedExposureSeconds;
                    p.ExposureSeconds = exposure;
                    Report(SessionState.Capturing, $"Capturing probe {next.FilterName} @ {exposure:F3}s (save deferred until accepted)");
                    frame = await _camera.CaptureFlatAsync(new FlatCaptureRequest
                    {
                        FilterName = next.FilterName, ExposureSeconds = exposure, Gain = next.Gain, Offset = next.Offset,
                        BinningX = next.BinningX, BinningY = next.BinningY, SaveImage = !request.Options.DryRun,
                        DryRun = request.Options.DryRun, CampaignId = campaign.CampaignId, SessionMode = mode.ToString()
                    }, cancellationToken).ConfigureAwait(false);

                    p.MeasuredAdu = frame.Statistics.MedianAdu; p.MeasuredHistogramFraction = frame.Statistics.MedianFraction;
                    Report(SessionState.Validating, "Validating probe before save");
                    var validation = _validator.Validate(frame.Statistics, new FlatValidationRequest
                    {
                        TargetHistogramFraction = next.TargetHistogramFraction,
                        TargetToleranceFraction = next.TargetToleranceFraction,
                        MaxSaturationFraction = request.Options.MaxSaturationFraction,
                        ExpectedFilterName = next.FilterName, ActualFilterName = frame.FilterName,
                        ExpectedGain = next.Gain, ActualGain = frame.Gain,
                        ExpectedOffset = next.Offset, ActualOffset = frame.Offset,
                        // The image is intentionally not saved yet. Validation checks whether it is
                        // saveable; the actual save is committed only after all validation passes.
                        ImageSaved = frame.Success,
                        AcquisitionSucceeded = frame.Success
                    });

                    var maxAdu = frame.Statistics.MaxAdu > 0 ? frame.Statistics.MaxAdu : PluginIdentity.LegacyMigrationMaxAdu;
                    sessionEstimates[next.FilterName] = _estimator.Estimate(exposure,
                        frame.Statistics.MedianAdu <= 0 ? 1 : frame.Statistics.MedianAdu,
                        next.TargetHistogramFraction * maxAdu, next.MinExposureSeconds, next.MaxExposureSeconds,
                        mode == CampaignMode.Evening ? EveningSkyTrendFactor : MorningSkyTrendFactor);

                    if (validation.IsAccepted)
                    {
                        Report(SessionState.Persisting, "Flat accepted; committing image to NINA save pipeline");
                        var saved = request.Options.DryRun || await _camera.SaveCapturedFlatAsync(frame, cancellationToken).ConfigureAwait(false);
                        if (!saved)
                        {
                            rejected++;
                            await _campaigns.RejectFlatAsync(request.CampaignKey, next.FilterName, "Accepted probe could not be saved", cancellationToken).ConfigureAwait(false);
                            rejectionStreak[next.FilterName] = rejectionStreak.TryGetValue(next.FilterName, out var ss) ? ss + 1 : 1;
                            _log?.Invoke($"Rejected {next.FilterName}: accepted probe could not be saved");
                            continue;
                        }

                        campaign = await _campaigns.AcceptFlatAsync(request.CampaignKey, next.FilterName, exposure,
                            validation.MeasuredAdu, validation.MeasuredHistogramFraction, sunAlt, cancellationToken).ConfigureAwait(false);
                        accepted++; rejectionStreak[next.FilterName] = 0;
                        p.Accepted = campaign.TotalAccepted; p.Remaining = campaign.TotalRemaining;

                        if (campaign.Filters.TryGetValue(next.FilterName, out var completedProgress)
                            && completedProgress.Remaining == 0
                            && completedFilterEvents.Add(next.FilterName))
                        {
                            await EmitAsync(SkyFlatSessionEventKind.AfterFilter, mode, campaign,
                                filter: next.FilterName, message: $"{next.FilterName} flats complete",
                                sunAltitude: sunAlt, exposure: exposure).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await _camera.DiscardCapturedFlatAsync(frame, cancellationToken).ConfigureAwait(false);
                        rejected++;
                        await _campaigns.RejectFlatAsync(request.CampaignKey, next.FilterName, validation.Reason, cancellationToken).ConfigureAwait(false);
                        rejectionStreak[next.FilterName] = rejectionStreak.TryGetValue(next.FilterName, out var s) ? s + 1 : 1;
                        _log?.Invoke($"Rejected probe {next.FilterName}: {validation.Reason}; image discarded (not saved).");

                        // Do not park a filter merely because twilight has moved outside its exposure
                        // band. The fresh estimate makes it leave the feasible frontier on the next
                        // loop, where the runner either chooses another filter or waits for the sky.
                        var fresh = sessionEstimates[next.FilterName];
                        if (fresh.Feasibility == ExposureFeasibility.Feasible
                            && rejectionStreak[next.FilterName] >= request.Options.MaxRejectedAttemptsPerFilter)
                        {
                            unavailableFilters.Add(next.FilterName); currentFilter = null; rejectionStreak[next.FilterName] = 0;
                            Report(SessionState.SwitchingFilter, $"Too many non-twilight rejects for {next.FilterName}; parking it for this session");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (frame is not null) await _camera.DiscardCapturedFlatAsync(frame, CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
                catch (Exception ex) when (request.OnFilterError == OnFilterErrorAction.ContinueNextFilter)
                {
                    if (frame is not null) await _camera.DiscardCapturedFlatAsync(frame, CancellationToken.None).ConfigureAwait(false);
                    _log?.Invoke($"Filter error on {next.FilterName}: {ex.Message}"); currentFilter = null; rejected++;
                }
            }

            Report(SessionState.Cancelled, "Cancelled", stop: SessionStopReasons.Cancelled);
            return Result(SessionState.Cancelled, SessionStopReasons.Cancelled, campaign, accepted, rejected);
        }
        catch (OperationCanceledException)
        {
            Report(SessionState.Cancelled, "Cancelled", stop: SessionStopReasons.Cancelled);
            var req = await _campaigns.EvaluateRequirementAsync(request.CampaignKey, request.Options, CancellationToken.None).ConfigureAwait(false);
            return Result(SessionState.Cancelled, SessionStopReasons.Cancelled, req.Campaign, accepted, rejected);
        }
        catch (Exception ex)
        {
            Report(SessionState.Faulted, ex.Message, stop: SessionStopReasons.Faulted);
            try
            {
                await EmitAsync(SkyFlatSessionEventKind.Error, request.Mode, null,
                    stopReason: SessionStopReasons.Faulted, message: ex.Message, exception: ex).ConfigureAwait(false);
            }
            catch (Exception eventEx)
            {
                _log?.Invoke($"Error event hook failed: {eventEx.Message}");
            }
            _notifications.Error(ex.Message);
            throw;
        }
        finally
        {
            try { if (!request.Options.DryRun) await _mount.RestoreIfRequestedAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) { _log?.Invoke($"Restore pointing failed: {ex.Message}"); }
        }
    }

    private static SkyFlatSessionResult Result(SessionState state, string reason, CampaignState? campaign, int accepted, int rejected)
        => new() { FinalState = state, StopReason = reason, Campaign = campaign, AcceptedThisSession = accepted, RejectedThisSession = rejected };

    private sealed class WaitTracker
    {
        private readonly IClock _clock; private string? _reason; private DateTime _started;
        public WaitTracker(IClock clock) => _clock = clock;
        public double ElapsedMinutes(string reason)
        {
            var now = _clock.UtcNow;
            if (!string.Equals(_reason, reason, StringComparison.Ordinal)) { _reason = reason; _started = now; return 0; }
            return (now - _started).TotalMinutes;
        }
        public void Reset() => _reason = null;
    }
}
