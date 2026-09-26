using SkyFlatCampaignManager.Core.Campaigns;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Containers;

/// <summary>
/// Snapshot exposed by the active Sky Flat Campaign Container to instructions placed in its event
/// containers. The values are deliberately simple CLR types so notification/script instructions can
/// consume them without taking a dependency on SFCM core models.
/// </summary>
public sealed class SkyFlatEventContext
{
    public string CampaignKey { get; internal set; } = "default";
    public string Mode { get; internal set; } = string.Empty;
    public string State { get; internal set; } = string.Empty;
    public string StopReason { get; internal set; } = string.Empty;
    public string WaitReason { get; internal set; } = string.Empty;
    public string Filter { get; internal set; } = string.Empty;
    public int TotalRequired { get; internal set; }
    public int TotalAccepted { get; internal set; }
    public int TotalRemaining { get; internal set; }
    public int FilterRequired { get; internal set; }
    public int FilterAccepted { get; internal set; }
    public int FilterRemaining { get; internal set; }
    public int AcceptedThisSession { get; internal set; }
    public int RejectedThisSession { get; internal set; }
    public double ExposureSeconds { get; internal set; }
    public double MedianAdu { get; internal set; }
    public double HistogramPercent { get; internal set; }
    public double SunAltitudeDegrees { get; internal set; } = double.NaN;
    public TimeSpan Duration { get; internal set; }

    internal void ApplyProgress(SkyFlatSessionProgress p)
    {
        State = p.State.ToString();
        WaitReason = p.WaitReason ?? string.Empty;
        StopReason = p.StopReason ?? string.Empty;
        Filter = p.CurrentFilter ?? string.Empty;
        TotalAccepted = p.Accepted;
        TotalRemaining = p.Remaining;
        ExposureSeconds = p.ExposureSeconds ?? 0;
        MedianAdu = p.MeasuredAdu ?? 0;
        HistogramPercent = (p.MeasuredHistogramFraction ?? 0) * 100.0;
    }

    internal void ApplyCampaign(CampaignState? campaign)
    {
        if (campaign is null) return;
        TotalAccepted = campaign.TotalAccepted;
        TotalRemaining = campaign.TotalRemaining;
        TotalRequired = campaign.Filters.Values.Sum(f => f.TargetCount);
        if (!string.IsNullOrWhiteSpace(Filter) && campaign.Filters.TryGetValue(Filter, out var fp))
        {
            FilterRequired = fp.TargetCount;
            FilterAccepted = fp.AcceptedCount;
            FilterRemaining = fp.Remaining;
        }
    }
}

/// <summary>
/// Process-local accessor used by child instructions and future NINA-expression bindings. A campaign
/// container owns the lifetime: Current is set while it executes and cleared during teardown.
/// </summary>
public static class SkyFlatEventContextAccessor
{
    private static readonly object Gate = new();
    private static SkyFlatEventContext? current;
    public static SkyFlatEventContext? Current { get { lock (Gate) return current; } }
    internal static void Set(SkyFlatEventContext value) { lock (Gate) current = value; }
    internal static void Clear(SkyFlatEventContext value) { lock (Gate) if (ReferenceEquals(current, value)) current = null; }
}
