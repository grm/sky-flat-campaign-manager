using SkyFlatCampaignManager.Core.Campaigns;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Containers;

public sealed class SkyFlatEventContext
{
    public string CampaignKey { get; internal set; } = "default";
    public string Mode { get; internal set; } = string.Empty;
    public string State { get; internal set; } = string.Empty;
    public string StopReason { get; internal set; } = string.Empty;
    public string WaitReason { get; internal set; } = string.Empty;
    public string EventMessage { get; internal set; } = string.Empty;
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
        TotalRequired = campaign.TotalTarget;
        ApplyFilter(campaign);
    }

    internal void ApplyEvent(SkyFlatSessionEvent e)
    {
        CampaignKey = e.CampaignKey;
        Mode = e.Mode.ToString();
        State = e.Kind.ToString();
        StopReason = e.StopReason ?? string.Empty;
        WaitReason = e.WaitReason ?? string.Empty;
        Filter = e.CurrentFilter ?? string.Empty;
        TotalRequired = e.ConfiguredTarget;
        TotalRemaining = e.Remaining;
        TotalAccepted = e.Campaign?.TotalAccepted ?? 0;
        AcceptedThisSession = e.AcceptedThisSession;
        RejectedThisSession = e.RejectedThisSession;
        ExposureSeconds = e.ExposureSeconds ?? 0;
        MedianAdu = e.MeasuredAdu ?? 0;
        HistogramPercent = (e.MeasuredHistogramFraction ?? 0) * 100.0;
        SunAltitudeDegrees = e.SunAltitudeDegrees ?? double.NaN;
        Duration = e.Duration;

        if (e.Campaign is not null)
        {
            if (TotalRequired <= 0) TotalRequired = e.Campaign.TotalTarget;
            ApplyFilter(e.Campaign);
        }
        else
        {
            FilterRequired = 0;
            FilterAccepted = 0;
            FilterRemaining = 0;
        }
    }

    private void ApplyFilter(CampaignState campaign)
    {
        if (!string.IsNullOrWhiteSpace(Filter) && campaign.Filters.TryGetValue(Filter, out var fp))
        {
            FilterRequired = fp.Target;
            FilterAccepted = fp.Accepted;
            FilterRemaining = fp.Remaining;
        }
        else
        {
            FilterRequired = 0;
            FilterAccepted = 0;
            FilterRemaining = 0;
        }
    }
}

public static class SkyFlatEventContextAccessor
{
    private static readonly object Gate = new();
    private static SkyFlatEventContext? current;
    public static SkyFlatEventContext? Current { get { lock (Gate) return current; } }
    internal static void Set(SkyFlatEventContext value) { lock (Gate) current = value; }
    internal static void Clear(SkyFlatEventContext value) { lock (Gate) if (ReferenceEquals(current, value)) current = null; }
}
