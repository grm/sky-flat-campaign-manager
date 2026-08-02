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

    /// <summary>
    /// Effective full-scale ADU value for this image/acquisition path (e.g. 4095 for 12-bit,
    /// 16383 for 14-bit, 65535 for 16-bit). Sourced from camera/image metadata when available.
    /// Defaults to 65535 for callers that have not been updated to report the real bit depth —
    /// do not assume this default reflects every camera.
    /// </summary>
    public double MaxAdu { get; init; } = 65535d;

    /// <summary>Median expressed as a fraction of <see cref="MaxAdu"/> (0.0–1.0).</summary>
    public double MedianFraction => MaxAdu > 0 ? MedianAdu / MaxAdu : 0d;

    /// <summary>Arithmetic mean expressed as a fraction of <see cref="MaxAdu"/> (0.0–1.0). Diagnostic only — acceptance uses the median.</summary>
    public double MeanFraction => MaxAdu > 0 ? MeanAdu / MaxAdu : 0d;
}
