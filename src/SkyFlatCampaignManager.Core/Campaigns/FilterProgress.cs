namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class FilterProgress
{
    public string FilterName { get; set; } = string.Empty;
    public int Target { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public double? LastExposureSeconds { get; set; }
    public double? LastMeasuredAdu { get; set; }
    public double? LastSunAltitudeDegrees { get; set; }
    public double? LastSqmMagnitudes { get; set; }
    public double? ExposureRatioToReference { get; set; }

    public int Remaining => Math.Max(0, Target - Accepted);
    public bool IsComplete => Accepted >= Target && Target > 0;
    public bool IsIncomplete => Target > 0 && Accepted < Target;
}
