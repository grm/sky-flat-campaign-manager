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

    /// <summary>Single source of truth for the whole session's time budget. See remarks on <see cref="AstronomicalWindowOptions"/>.</summary>
    public double MaxDurationMinutes { get; init; } = 90;
    public bool AllowWaitForSky { get; init; } = true;

    /// <summary>
    /// Single source of truth for how long to continuously wait for one reason (astronomical
    /// window or filter feasibility) before giving up. Each wait reason gets its own timer that
    /// resets when the reason changes or progress is made — see <see cref="SkyFlatSessionRunner"/>.
    /// </summary>
    public double MaxWaitMinutes { get; init; } = 45;

    public WhenNoFlatsRequiredAction WhenNoFlatsRequired { get; init; } = WhenNoFlatsRequiredAction.SucceedImmediately;
    public WhenNoFilterFeasibleAction WhenNoFilterFeasible { get; init; } = WhenNoFilterFeasibleAction.PartialSuccess;
    public OnFilterErrorAction OnFilterError { get; init; } = OnFilterErrorAction.ContinueNextFilter;
    public bool UseSqm { get; init; }
    public MountPointingRequest Pointing { get; init; } = new();
    public CampaignOptions Options { get; init; } = new();
    public IReadOnlyList<FilterCampaignSettings> Filters { get; init; } = Array.Empty<FilterCampaignSettings>();
}

public sealed class SkyFlatSessionProgress
{
    public SessionState State { get; set; } = SessionState.Idle;
    public string? CurrentFilter { get; set; }
    public int Accepted { get; set; }
    public int Remaining { get; set; }

    /// <summary>Measured median ADU (raw), for diagnostics/logs.</summary>
    public double? MeasuredAdu { get; set; }

    /// <summary>Measured median as a fraction of full scale (0.0–1.0) — the "Measured median histogram level".</summary>
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
    /// <summary>Evening sky is darkening: nudge the next probe exposure slightly longer than pure proportional scaling would.</summary>
    private const double EveningSkyTrendFactor = 1.05;

    /// <summary>Morning sky is brightening: nudge the next probe exposure slightly shorter than pure proportional scaling would.</summary>
    private const double MorningSkyTrendFactor = 0.95;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

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

    public SkyFlatSessionRunner(
        ICampaignService campaigns,
        ICameraAcquisitionService camera,
        IFilterWheelService filterWheel,
        IMountPositioningService mount,
        IFlatExposureEstimator estimator,
        IFlatFrameValidator validator,
        IAstronomicalWindowService windows,
        ISunAltitudeProvider sun,
        ISkyBrightnessProvider brightness,
        INotificationService notifications,
        IClock clock,
        Action<string>? log = null)
    {
        _campaigns = campaigns;
        _camera = camera;
        _filterWheel = filterWheel;
        _mount = mount;
        _estimator = estimator;
        _validator = validator;
        _windows = windows;
        _sun = sun;
        _brightness = brightness;
        _notifications = notifications;
        _clock = clock;
        _log = log;
    }

    public async Task<SkyFlatSessionResult> RunAsync(
        SkyFlatSessionRequest request,
        IProgress<SkyFlatSessionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sessionProgress = new SkyFlatSessionProgress();
        var accepted = 0;
        var rejected = 0;
        var started = _clock.UtcNow;
        var previousAltitude = _sun.GetSunAltitudeDegrees(started);
        string? currentFilter = null;
        var rejectionStreak = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Per-session exposure estimates for filters actually captured this session (post-capture,
        // most accurate). Filters not yet captured this session fall back to a projection from
        // persisted history in EstimateFeasibility.
        var sessionEstimates = new Dictionary<string, ExposureEstimateResult>(StringComparer.OrdinalIgnoreCase);

        // Filters repeatedly rejected this session are parked here so the strategy cannot pick the
        // same broken filter forever. Session-scoped only — cleared on the next run.
        var unavailableFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Independent, resettable wait timers — see the AstronomicalWindow*/FilterNotFeasible reasons.
        // Each represents *continuous* waiting for its own reason, not elapsed time since session start.
        var windowWait = new WaitTracker(_clock);
        var filterWait = new WaitTracker(_clock);

        void Report(SessionState state, string message, string? wait = null, string? stop = null)
        {
            sessionProgress.State = state;
            sessionProgress.StatusMessage = message;
            sessionProgress.WaitReason = wait;
            sessionProgress.StopReason = stop;
            sessionProgress.CurrentFilter = currentFilter;
            progress?.Report(sessionProgress);
            if (request.Options.DetailedLogging)
            {
                _log?.Invoke($"[{state}] {message}");
            }
        }

        ExposureEstimateResult EstimateFeasibility(FilterCampaignSettings filter, CampaignState campaignState, CampaignMode currentMode)
        {
            if (sessionEstimates.TryGetValue(filter.FilterName, out var cached))
            {
                return cached;
            }

            if (campaignState.Filters.TryGetValue(filter.FilterName, out var progress2)
                && progress2.LastExposureSeconds is { } lastExposure && lastExposure > 0
                && progress2.LastMeasuredAdu is { } lastAdu && lastAdu > 0)
            {
                // Historical ADU predates knowledge of the real bit depth for that capture, so the
                // legacy full-scale assumption is the best available projection. This only affects
                // the *feasibility guess* for a filter not yet captured this session — actual
                // acceptance always uses the real captured frame's MaxAdu.
                var targetAdu = filter.TargetHistogramFraction * PluginIdentity.LegacyMigrationMaxAdu;
                var trend = currentMode == CampaignMode.Evening ? EveningSkyTrendFactor : MorningSkyTrendFactor;
                return _estimator.Estimate(lastExposure, lastAdu, targetAdu, filter.MinExposureSeconds, filter.MaxExposureSeconds, trend);
            }

            // No history at all (brand-new filter): optimistically try the default starting guess.
            var defaultGuess = Math.Clamp(1.0, filter.MinExposureSeconds, filter.MaxExposureSeconds);
            return new ExposureEstimateResult
            {
                UnclampedExposureSeconds = defaultGuess,
                ClampedExposureSeconds = defaultGuess,
                Feasibility = ExposureFeasibility.Feasible
            };
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(SessionState.CheckingCampaign, "Evaluating campaign requirement");

            var requirement = await _campaigns.EvaluateRequirementAsync(request.CampaignKey, request.Options, cancellationToken)
                .ConfigureAwait(false);

            if (!requirement.IsRequired)
            {
                if (request.WhenNoFlatsRequired == WhenNoFlatsRequiredAction.SucceedImmediately)
                {
                    Report(SessionState.Completed, "No flats required", stop: requirement.Reason);
                    return new SkyFlatSessionResult
                    {
                        FinalState = SessionState.Completed,
                        StopReason = requirement.Reason,
                        Campaign = requirement.Campaign,
                        AcceptedThisSession = 0,
                        RejectedThisSession = 0
                    };
                }
            }

            var campaign = await _campaigns.GetOrCreateAsync(
                request.CampaignKey,
                request.ProfileId,
                request.Filters,
                request.Options,
                cancellationToken).ConfigureAwait(false);

            sessionProgress.Accepted = campaign.TotalAccepted;
            sessionProgress.Remaining = campaign.TotalRemaining;

            Report(SessionState.CheckingEquipment, "Checking equipment");
            if (!_camera.IsConnected)
            {
                throw new PluginError("Camera is not connected.", ErrorCategory.NonRecoverable);
            }

            if (!_filterWheel.IsConnected)
            {
                throw new PluginError("Filter wheel is not connected.", ErrorCategory.NonRecoverable);
            }

            var mode = request.Mode;
            var window = mode == CampaignMode.Morning ? request.Options.MorningWindow : request.Options.EveningWindow;
            if (mode == CampaignMode.Automatic)
            {
                var altNow = _sun.GetSunAltitudeDegrees(_clock.UtcNow);
                mode = _windows.ResolveMode(CampaignMode.Automatic, altNow, previousAltitude);
                window = mode == CampaignMode.Morning ? request.Options.MorningWindow : request.Options.EveningWindow;
            }

            Report(SessionState.CheckingEquipment, $"Mode={mode}; positioning mount if required");
            if (!request.Options.DryRun)
            {
                await _mount.EnsureSafePointingAsync(request.Pointing, cancellationToken).ConfigureAwait(false);
            }

            var strategy = FilterSelectionStrategyFactory.Create(request.Strategy);

            while (!cancellationToken.IsCancellationRequested)
            {
                if ((_clock.UtcNow - started).TotalMinutes >= request.MaxDurationMinutes)
                {
                    Report(SessionState.StoppedByTimeout, "Max duration reached", stop: SessionStopReasons.MaxDuration);
                    return Result(SessionState.StoppedByTimeout, SessionStopReasons.MaxDuration, campaign, accepted, rejected);
                }

                campaign = await _campaigns.GetOrCreateAsync(request.CampaignKey, request.ProfileId, request.Filters, request.Options, cancellationToken)
                    .ConfigureAwait(false);
                sessionProgress.Accepted = campaign.TotalAccepted;
                sessionProgress.Remaining = campaign.TotalRemaining;

                if (campaign.IsComplete || campaign.TotalRemaining <= 0)
                {
                    campaign = await _campaigns.MarkCompletedAsync(request.CampaignKey, request.Options, cancellationToken)
                        .ConfigureAwait(false);
                    Report(SessionState.Completed, "Campaign complete", stop: SessionStopReasons.Completed);
                    _notifications.Success("Sky flat campaign completed.");
                    return Result(SessionState.Completed, SessionStopReasons.Completed, campaign, accepted, rejected);
                }

                var sunAlt = _sun.GetSunAltitudeDegrees(_clock.UtcNow);
                mode = _windows.ResolveMode(mode, sunAlt, previousAltitude);
                previousAltitude = sunAlt;
                window = mode == CampaignMode.Morning ? request.Options.MorningWindow : request.Options.EveningWindow;

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
                                Report(SessionState.WaitingForAstronomicalWindow,
                                    $"Sun altitude {sunAlt:F1}° has not reached the {mode} window [{window.MinSunAltitudeDegrees:F1}°, {window.MaxSunAltitudeDegrees:F1}°] yet",
                                    wait: SessionWaitReasons.AstronomicalWindowNotOpenYet);
                                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                                continue;
                            }

                            Report(SessionState.StoppedByWindow, "Waited for the astronomical window to open, but it did not open in time",
                                stop: SessionStopReasons.AstronomicalWindowWaitTimeout);
                            return Result(SessionState.StoppedByWindow, SessionStopReasons.AstronomicalWindowWaitTimeout, campaign, accepted, rejected);
                        }

                        // Not open yet and waiting is disabled — stop immediately, do not poll.
                        Report(SessionState.StoppedByWindow,
                            $"Astronomical window not open yet (sun altitude {sunAlt:F1}°) and waiting is disabled",
                            stop: SessionStopReasons.AstronomicalWindowNotOpenYet);
                        return Result(SessionState.StoppedByWindow, SessionStopReasons.AstronomicalWindowNotOpenYet, campaign, accepted, rejected);
                    }

                    // TooLate: the window has already passed this session and physically cannot
                    // reopen (the sun keeps moving the same direction). Stop immediately — never
                    // wait, regardless of AllowWaitForSky. This is a normal closed twilight window,
                    // not a fault.
                    var reason = mode == CampaignMode.Evening ? SessionStopReasons.EveningSkyTooDark : SessionStopReasons.MorningSkyTooBright;
                    var message = mode == CampaignMode.Evening
                        ? $"Sun altitude {sunAlt:F1}° is below the evening window minimum ({window.MinSunAltitudeDegrees:F1}°) — sky is already too dark for flats; this is a normal closed twilight window."
                        : $"Sun altitude {sunAlt:F1}° is above the morning window maximum ({window.MaxSunAltitudeDegrees:F1}°) — sky is already too bright for flats; this is a normal closed twilight window.";
                    Report(SessionState.StoppedByWindow, message, stop: reason);
                    return Result(SessionState.StoppedByWindow, reason, campaign, accepted, rejected);
                }

                // Window is open — any prior "not open yet" wait no longer applies.
                windowWait.Reset();

                Report(SessionState.SelectingFilter, "Selecting next filter");

                var incompleteCandidates = request.Filters
                    .Where(f => f.Enabled
                        && !unavailableFilters.Contains(f.FilterName)
                        && campaign.Filters.TryGetValue(f.FilterName, out var fp) && fp.IsIncomplete)
                    .ToList();

                if (incompleteCandidates.Count == 0)
                {
                    Report(SessionState.Completed, "No incomplete filters", stop: SessionStopReasons.NoFilters);
                    return Result(SessionState.Completed, SessionStopReasons.NoFilters, campaign, accepted, rejected);
                }

                var feasibilityByFilter = incompleteCandidates.ToDictionary(
                    f => f.FilterName,
                    f => EstimateFeasibility(f, campaign, mode),
                    StringComparer.OrdinalIgnoreCase);

                var feasibleCandidates = incompleteCandidates
                    .Where(f => feasibilityByFilter[f.FilterName].Feasibility == ExposureFeasibility.Feasible)
                    .ToList();

                FilterCampaignSettings? next = null;
                if (feasibleCandidates.Count > 0)
                {
                    next = strategy.SelectNext(
                        feasibleCandidates,
                        campaign,
                        mode,
                        new FilterSelectionContext
                        {
                            CurrentSunAltitudeDegrees = sunAlt,
                            CurrentFilterName = currentFilter,
                            EstimatedExposureSecondsByFilter = feasibleCandidates.ToDictionary(
                                f => f.FilterName,
                                f => feasibilityByFilter[f.FilterName].ClampedExposureSeconds,
                                StringComparer.OrdinalIgnoreCase)
                        });
                }

                if (next is null)
                {
                    // Evaluate ALL incomplete filters (not just the one the strategy would have
                    // preferred) before declaring the session stuck.
                    var canImprove = incompleteCandidates.Any(f =>
                        ExposureFeasibilityRules.CanImproveByWaiting(mode, feasibilityByFilter[f.FilterName].Feasibility));
                    var shouldWait = canImprove && request.AllowWaitForSky && request.WhenNoFilterFeasible == WhenNoFilterFeasibleAction.Wait;

                    if (shouldWait)
                    {
                        var elapsed = filterWait.ElapsedMinutes(SessionWaitReasons.FilterNotFeasible);
                        if (elapsed < request.MaxWaitMinutes)
                        {
                            Report(SessionState.WaitingForAstronomicalWindow,
                                "No incomplete filter is exposure-feasible yet, but at least one may become feasible as the sky changes",
                                wait: SessionWaitReasons.FilterNotFeasible);
                            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        Report(SessionState.StoppedByWindow, "Waited for a filter to become exposure-feasible, but none did in time",
                            stop: SessionStopReasons.NoFilterFeasibleWaitTimeout);
                        return Result(SessionState.StoppedByWindow, SessionStopReasons.NoFilterFeasibleWaitTimeout, campaign, accepted, rejected);
                    }

                    if (request.WhenNoFilterFeasible == WhenNoFilterFeasibleAction.Fail)
                    {
                        throw new PluginError(
                            $"No filter currently feasible (checked {incompleteCandidates.Count} incomplete filter(s)).",
                            ErrorCategory.Session);
                    }

                    var detail = string.Join(" ", incompleteCandidates.Select(f =>
                        $"{f.FilterName}: {ExposureFeasibilityRules.Describe(mode, feasibilityByFilter[f.FilterName].Feasibility)}"));
                    Report(SessionState.StoppedByWindow, $"No incomplete filter is currently exposure-feasible. {detail}",
                        stop: SessionStopReasons.NoFilterFeasible);
                    return Result(SessionState.StoppedByWindow, SessionStopReasons.NoFilterFeasible, campaign, accepted, rejected);
                }

                // A feasible filter was found — any prior "no filter feasible" wait no longer applies.
                filterWait.Reset();

                try
                {
                    if (!string.Equals(_filterWheel.CurrentFilterName, next.FilterName, StringComparison.OrdinalIgnoreCase))
                    {
                        Report(SessionState.ChangingFilter, $"Changing filter to {next.FilterName}");
                        if (!request.Options.DryRun)
                        {
                            await _filterWheel.ChangeFilterAsync(next.FilterName, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    currentFilter = next.FilterName;
                    Report(SessionState.EstimatingExposure, $"Estimating exposure for {next.FilterName}");

                    // Optional brightness probe for anticipation only
                    _ = await _brightness.GetSampleAsync(cancellationToken).ConfigureAwait(false);

                    var exposure = feasibilityByFilter[next.FilterName].ClampedExposureSeconds;
                    Report(SessionState.Capturing, $"Capturing {next.FilterName} @ {exposure:F3}s");
                    sessionProgress.ExposureSeconds = exposure;

                    var frame = await _camera.CaptureFlatAsync(new FlatCaptureRequest
                    {
                        FilterName = next.FilterName,
                        ExposureSeconds = exposure,
                        Gain = next.Gain,
                        Offset = next.Offset,
                        BinningX = next.BinningX,
                        BinningY = next.BinningY,
                        SaveImage = !request.Options.DryRun,
                        DryRun = request.Options.DryRun,
                        CampaignId = campaign.CampaignId,
                        SessionMode = mode.ToString()
                    }, cancellationToken).ConfigureAwait(false);

                    Report(SessionState.Validating, "Validating frame");
                    sessionProgress.MeasuredAdu = frame.Statistics.MedianAdu;
                    sessionProgress.MeasuredHistogramFraction = frame.Statistics.MedianFraction;

                    var validation = _validator.Validate(frame.Statistics, new FlatValidationRequest
                    {
                        TargetHistogramFraction = next.TargetHistogramFraction,
                        TargetToleranceFraction = next.TargetToleranceFraction,
                        MaxSaturationFraction = request.Options.MaxSaturationFraction,
                        ExpectedFilterName = next.FilterName,
                        ActualFilterName = frame.FilterName,
                        ExpectedGain = next.Gain,
                        ActualGain = frame.Gain,
                        ExpectedOffset = next.Offset,
                        ActualOffset = frame.Offset,
                        ImageSaved = frame.Saved || request.Options.DryRun,
                        AcquisitionSucceeded = frame.Success
                    });

                    var maxAdu = frame.Statistics.MaxAdu > 0 ? frame.Statistics.MaxAdu : PluginIdentity.LegacyMigrationMaxAdu;
                    var targetAduForEstimator = next.TargetHistogramFraction * maxAdu;
                    var measuredAduForEstimator = frame.Statistics.MedianAdu <= 0 ? 1 : frame.Statistics.MedianAdu;
                    sessionEstimates[next.FilterName] = _estimator.Estimate(
                        exposure,
                        measuredAduForEstimator,
                        targetAduForEstimator,
                        next.MinExposureSeconds,
                        next.MaxExposureSeconds,
                        skyTrendFactor: mode == CampaignMode.Evening ? EveningSkyTrendFactor : MorningSkyTrendFactor);

                    if (validation.IsAccepted)
                    {
                        Report(SessionState.Persisting, "Persisting accepted flat");
                        campaign = await _campaigns.AcceptFlatAsync(
                            request.CampaignKey,
                            next.FilterName,
                            exposure,
                            validation.MeasuredAdu,
                            validation.MeasuredHistogramFraction,
                            sunAlt,
                            cancellationToken).ConfigureAwait(false);

                        accepted++;
                        rejectionStreak[next.FilterName] = 0;
                        sessionProgress.Accepted = campaign.TotalAccepted;
                        sessionProgress.Remaining = campaign.TotalRemaining;
                    }
                    else
                    {
                        rejected++;
                        await _campaigns.RejectFlatAsync(request.CampaignKey, next.FilterName, validation.Reason, cancellationToken)
                            .ConfigureAwait(false);
                        rejectionStreak[next.FilterName] = rejectionStreak.TryGetValue(next.FilterName, out var s) ? s + 1 : 1;
                        _log?.Invoke($"Rejected {next.FilterName}: {validation.Reason}");

                        if (rejectionStreak[next.FilterName] >= request.Options.MaxRejectedAttemptsPerFilter)
                        {
                            unavailableFilters.Add(next.FilterName);
                            currentFilter = null; // force strategy to pick another
                            rejectionStreak[next.FilterName] = 0;
                            Report(SessionState.SwitchingFilter, $"Too many rejects for {next.FilterName}; parking it for the rest of this session");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (request.OnFilterError == OnFilterErrorAction.ContinueNextFilter)
                {
                    _log?.Invoke($"Filter error on {next.FilterName}: {ex.Message}");
                    currentFilter = null;
                    rejected++;
                }
            }

            Report(SessionState.Cancelled, "Cancelled", stop: SessionStopReasons.Cancelled);
            return Result(SessionState.Cancelled, SessionStopReasons.Cancelled, campaign, accepted, rejected);
        }
        catch (OperationCanceledException)
        {
            Report(SessionState.Cancelled, "Cancelled", stop: SessionStopReasons.Cancelled);
            var campaign = await _campaigns.EvaluateRequirementAsync(request.CampaignKey, request.Options, CancellationToken.None)
                .ConfigureAwait(false);
            return Result(SessionState.Cancelled, SessionStopReasons.Cancelled, campaign.Campaign, accepted, rejected);
        }
        catch (Exception ex)
        {
            Report(SessionState.Faulted, ex.Message, stop: SessionStopReasons.Faulted);
            _notifications.Error(ex.Message);
            throw;
        }
        finally
        {
            try
            {
                if (!request.Options.DryRun)
                {
                    await _mount.RestoreIfRequestedAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Restore pointing failed: {ex.Message}");
            }
        }
    }

    private static SkyFlatSessionResult Result(SessionState state, string reason, CampaignState? campaign, int accepted, int rejected)
        => new()
        {
            FinalState = state,
            StopReason = reason,
            Campaign = campaign,
            AcceptedThisSession = accepted,
            RejectedThisSession = rejected
        };

    /// <summary>
    /// Tracks continuous elapsed time for a single named wait reason. Calling
    /// <see cref="ElapsedMinutes"/> with a different reason than last time — or after
    /// <see cref="Reset"/> — starts the timer over at zero, so <c>MaxWaitMinutes</c> always
    /// represents continuous waiting for the *current* reason rather than elapsed session time.
    /// </summary>
    private sealed class WaitTracker
    {
        private readonly IClock _clock;
        private string? _activeReason;
        private DateTime _startedUtc;

        public WaitTracker(IClock clock) => _clock = clock;

        public double ElapsedMinutes(string reason)
        {
            var now = _clock.UtcNow;
            if (!string.Equals(_activeReason, reason, StringComparison.Ordinal))
            {
                _activeReason = reason;
                _startedUtc = now;
                return 0;
            }

            return (now - _startedUtc).TotalMinutes;
        }

        public void Reset() => _activeReason = null;
    }
}
