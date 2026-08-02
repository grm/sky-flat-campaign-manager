namespace SkyFlatCampaignManager.Core.Campaigns;

/// <summary>
/// Distinguishes "enough to be usable" from "fully done" for a single filter or the whole
/// campaign. Only <see cref="Complete"/> counts toward marking a multi-day campaign
/// <see cref="CampaignStatus.Completed"/> — <see cref="MinimumReached"/> is informational.
/// </summary>
public enum FilterCompletionStatus
{
    Incomplete,
    MinimumReached,
    Complete
}

public sealed class FilterProgress
{
    public string FilterName { get; set; } = string.Empty;
    public int Target { get; set; }

    /// <summary>
    /// Enough accepted flats for this filter to be usable this session, without being fully
    /// complete. Copied from <see cref="FilterCampaignSettings.MinimumAcceptableCount"/> when the
    /// campaign is created/synced. 0 disables the distinction (Status can only be Incomplete/Complete).
    /// </summary>
    public int MinimumAcceptableCount { get; set; }

    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public double? LastExposureSeconds { get; set; }
    public double? LastMeasuredAdu { get; set; }

    /// <summary>Last measured median as a fraction of full scale (0.0–1.0). See <see cref="Acquisition.ImageStatisticsResult.MaxAdu"/>.</summary>
    public double? LastMeasuredHistogramFraction { get; set; }

    /// <summary>
    /// Sun altitude at the time of the last <b>accepted</b> flat for this filter, persisted
    /// atomically together with the acceptance so it survives a plugin/NINA restart. Used by
    /// <see cref="Filters.ClosestToOptimalWindowStrategy"/>.
    /// </summary>
    public double? LastSunAltitudeDegrees { get; set; }
    public double? LastSqmMagnitudes { get; set; }
    public double? ExposureRatioToReference { get; set; }

    public int Remaining => Math.Max(0, Target - Accepted);
    public bool IsComplete => Accepted >= Target && Target > 0;
    public bool IsIncomplete => Target > 0 && Accepted < Target;
    public bool HasReachedMinimum => MinimumAcceptableCount > 0 && Accepted >= MinimumAcceptableCount;

    public FilterCompletionStatus Status =>
        IsComplete ? FilterCompletionStatus.Complete
        : HasReachedMinimum ? FilterCompletionStatus.MinimumReached
        : FilterCompletionStatus.Incomplete;
}
