namespace SkyFlatCampaignManager.Core.Campaigns;

/// <summary>
/// High-level, blocking lifecycle events emitted by <see cref="SkyFlatSessionRunner"/>.
/// Implementations are awaited before acquisition continues, which makes them suitable for NINA
/// custom event containers (notifications, scripts, etc.) while keeping provider-specific code out
/// of the Core assembly.
/// </summary>
public enum SkyFlatSessionEventKind
{
    CampaignRequired,
    CampaignNotRequired,
    BeforeWait,
    AfterWait,
    BeforeFilter,
    AfterFilter,
    CampaignCompleted,
    SessionIncomplete,
    Error
}

public sealed class SkyFlatSessionEvent
{
    public SkyFlatSessionEventKind Kind { get; init; }
    public string CampaignKey { get; init; } = "default";
    public CampaignMode Mode { get; init; } = CampaignMode.Automatic;
    public CampaignState? Campaign { get; init; }
    public string? CurrentFilter { get; init; }
    public string? WaitReason { get; init; }
    public string? StopReason { get; init; }
    public string? StatusMessage { get; init; }
    public int ConfiguredTarget { get; init; }
    public int Remaining { get; init; }
    public int AcceptedThisSession { get; init; }
    public int RejectedThisSession { get; init; }
    public double? ExposureSeconds { get; init; }
    public double? MeasuredAdu { get; init; }
    public double? MeasuredHistogramFraction { get; init; }
    public double? SunAltitudeDegrees { get; init; }
    public TimeSpan Duration { get; init; }
    public Exception? Exception { get; init; }
}

public interface ISkyFlatSessionEventSink
{
    Task OnEventAsync(SkyFlatSessionEvent sessionEvent, CancellationToken cancellationToken);
}
