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
    public double? MeasuredAdu { get; set; }
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
        var exposureGuess = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

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
            var waitStarted = _clock.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                if ((_clock.UtcNow - started).TotalMinutes >= request.MaxDurationMinutes)
                {
                    Report(SessionState.StoppedByTimeout, "Max duration reached", stop: "MaxDuration");
                    return Result(SessionState.StoppedByTimeout, "Max duration reached", campaign, accepted, rejected);
                }

                campaign = await _campaigns.GetOrCreateAsync(request.CampaignKey, request.ProfileId, request.Filters, request.Options, cancellationToken)
                    .ConfigureAwait(false);
                sessionProgress.Accepted = campaign.TotalAccepted;
                sessionProgress.Remaining = campaign.TotalRemaining;

                if (campaign.IsComplete || campaign.TotalRemaining <= 0)
                {
                    campaign = await _campaigns.MarkCompletedAsync(request.CampaignKey, request.Options, cancellationToken)
                        .ConfigureAwait(false);
                    Report(SessionState.Completed, "Campaign complete", stop: "Completed");
                    _notifications.Success("Sky flat campaign completed.");
                    return Result(SessionState.Completed, "Campaign complete", campaign, accepted, rejected);
                }

                var sunAlt = _sun.GetSunAltitudeDegrees(_clock.UtcNow);
                mode = _windows.ResolveMode(mode == CampaignMode.Automatic ? CampaignMode.Automatic : mode, sunAlt, previousAltitude);
                previousAltitude = sunAlt;
                window = mode == CampaignMode.Morning ? request.Options.MorningWindow : request.Options.EveningWindow;

                if (!_windows.IsWithinSafetyWindow(sunAlt, window))
                {
                    if (request.AllowWaitForSky && (_clock.UtcNow - waitStarted).TotalMinutes < request.MaxWaitMinutes)
                    {
                        Report(SessionState.WaitingForAstronomicalWindow, $"Sun altitude {sunAlt:F1}° outside window", wait: "AstronomicalWindow");
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    Report(SessionState.StoppedByWindow, "Astronomical safety window closed", stop: "WindowClosed");
                    return Result(SessionState.StoppedByWindow, "Astronomical safety window closed", campaign, accepted, rejected);
                }

                Report(SessionState.SelectingFilter, "Selecting next filter");
                var next = strategy.SelectNext(
                    request.Filters.ToList(),
                    campaign,
                    mode,
                    new FilterSelectionContext
                    {
                        CurrentSunAltitudeDegrees = sunAlt,
                        CurrentFilterName = currentFilter,
                        EstimatedExposureSecondsByFilter = exposureGuess
                    });

                if (next is null)
                {
                    Report(SessionState.Completed, "No incomplete filters", stop: "NoFilters");
                    return Result(SessionState.Completed, "No incomplete filters", campaign, accepted, rejected);
                }

                // Feasibility: estimated exposure within limits
                var guess = exposureGuess.TryGetValue(next.FilterName, out var g)
                    ? g
                    : campaign.Filters.TryGetValue(next.FilterName, out var fp) && fp.LastExposureSeconds is { } last
                        ? last
                        : Math.Clamp(1.0, next.MinExposureSeconds, next.MaxExposureSeconds);

                if (guess < next.MinExposureSeconds || guess > next.MaxExposureSeconds)
                {
                    if (request.AllowWaitForSky && request.WhenNoFilterFeasible == WhenNoFilterFeasibleAction.Wait
                        && (_clock.UtcNow - waitStarted).TotalMinutes < request.MaxWaitMinutes)
                    {
                        Report(SessionState.WaitingForAstronomicalWindow, $"Filter {next.FilterName} not feasible yet", wait: "FilterNotFeasible");
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (request.WhenNoFilterFeasible == WhenNoFilterFeasibleAction.Fail)
                    {
                        throw new PluginError($"No filter currently feasible (tried {next.FilterName}).", ErrorCategory.Session);
                    }

                    Report(SessionState.StoppedByWindow, "No filter currently feasible", stop: "NoFilterFeasible");
                    return Result(SessionState.StoppedByWindow, "No filter currently feasible", campaign, accepted, rejected);
                }

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

                    var exposure = guess;
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

                    var validation = _validator.Validate(frame.Statistics, new FlatValidationRequest
                    {
                        TargetAdu = next.TargetAdu,
                        AduTolerance = next.AduTolerance,
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

                    exposureGuess[next.FilterName] = _estimator.EstimateNextExposureSeconds(
                        exposure,
                        frame.Statistics.MedianAdu <= 0 ? 1 : frame.Statistics.MedianAdu,
                        next.TargetAdu,
                        next.MinExposureSeconds,
                        next.MaxExposureSeconds,
                        skyTrendFactor: mode == CampaignMode.Evening ? 1.05 : 0.95);

                    if (validation.IsAccepted)
                    {
                        Report(SessionState.Persisting, "Persisting accepted flat");
                        campaign = await _campaigns.AcceptFlatAsync(
                            request.CampaignKey,
                            next.FilterName,
                            exposure,
                            validation.MeasuredAdu,
                            cancellationToken).ConfigureAwait(false);

                        if (campaign.Filters.TryGetValue(next.FilterName, out var updated))
                        {
                            updated.LastSunAltitudeDegrees = sunAlt;
                        }

                        await _campaigns.GetOrCreateAsync(request.CampaignKey, request.ProfileId, request.Filters, request.Options, cancellationToken)
                            .ConfigureAwait(false);
                        // Re-save altitude learning
                        // AcceptFlat already persisted counts; learning fields updated via repository reload below if needed.

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
                            currentFilter = null; // force strategy to pick another
                            rejectionStreak[next.FilterName] = 0;
                            Report(SessionState.SwitchingFilter, $"Too many rejects for {next.FilterName}");
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

            Report(SessionState.Cancelled, "Cancelled", stop: "Cancelled");
            return Result(SessionState.Cancelled, "Cancelled", campaign, accepted, rejected);
        }
        catch (OperationCanceledException)
        {
            Report(SessionState.Cancelled, "Cancelled", stop: "Cancelled");
            var campaign = await _campaigns.EvaluateRequirementAsync(request.CampaignKey, request.Options, CancellationToken.None)
                .ConfigureAwait(false);
            return Result(SessionState.Cancelled, "Cancelled", campaign.Campaign, accepted, rejected);
        }
        catch (Exception ex)
        {
            Report(SessionState.Faulted, ex.Message, stop: "Faulted");
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
}
