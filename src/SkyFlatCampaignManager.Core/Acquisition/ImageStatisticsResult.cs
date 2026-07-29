namespace SkyFlatCampaignManager.Core.Acquisition;

public sealed class ImageStatisticsResult
{
    public double MedianAdu { get; init; }
    public double MeanAdu { get; init; }
    public double LowPercentileAdu { get; init; }
    public double HighPercentileAdu { get; init; }
    public double StdDevAdu { get; init; }
    public double SaturatedFraction { get; init; }
    public double TooDarkFraction { get; init; }
    public int SamplePixelCount { get; init; }
    public bool IsCorrupted { get; init; }
    public string? CorruptionReason { get; init; }
}
