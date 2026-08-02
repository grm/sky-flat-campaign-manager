namespace SkyFlatCampaignManager.Core.Acquisition;

public interface IFlatFrameValidator
{
    FlatValidationResult Validate(ImageStatisticsResult stats, FlatValidationRequest request);
}

public sealed class FlatValidationRequest
{
    /// <summary>Target histogram level as a fraction of full scale, 0.0–1.0 (e.g. 0.40 = 40%). This is the "Target histogram level".</summary>
    public double TargetHistogramFraction { get; init; }

    /// <summary>
    /// NINA-style tolerance expressed as a fraction OF THE TARGET, not of full scale and not a
    /// fixed ADU count. E.g. TargetHistogramFraction=0.40 and TargetToleranceFraction=0.10 accepts
    /// 36%–44% of full scale (±10% of the 40% target).
    /// </summary>
    public double TargetToleranceFraction { get; init; }

    public double MaxSaturationFraction { get; init; } = PluginIdentity.DefaultMaxSaturationFraction;
    public string ExpectedFilterName { get; init; } = string.Empty;
    public string ActualFilterName { get; init; } = string.Empty;
    public int ExpectedGain { get; init; }
    public int ActualGain { get; init; }
    public int ExpectedOffset { get; init; }
    public int ActualOffset { get; init; }
    public bool ImageSaved { get; init; } = true;
    public bool AcquisitionSucceeded { get; init; } = true;
}

public sealed class FlatValidationResult
{
    public bool IsAccepted { get; init; }
    public string Reason { get; init; } = string.Empty;

    /// <summary>Measured median ADU (raw, for diagnostics/logs). The validator accepts/rejects on the median, not the mean.</summary>
    public double MeasuredAdu { get; init; }

    /// <summary>Measured median expressed as a fraction of full scale (0.0–1.0). The "Measured median histogram level".</summary>
    public double MeasuredHistogramFraction { get; init; }

    /// <summary>Arithmetic mean ADU — diagnostic only, never used for acceptance.</summary>
    public double MeasuredMeanAdu { get; init; }

    /// <summary>Target expressed in raw ADU for the actual image's bit depth (TargetHistogramFraction × MaxAdu). Diagnostics/logs only.</summary>
    public double TargetAdu { get; init; }

    /// <summary>Tolerance expressed in raw ADU for the actual image's bit depth (TargetAdu × TargetToleranceFraction). Diagnostics/logs only.</summary>
    public double ToleranceAdu { get; init; }
}

/// <summary>
/// Validates flats using the robust <b>median</b> histogram level (not the mean). Levels are
/// normalized to the actual captured image's bit depth (<see cref="ImageStatisticsResult.MaxAdu"/>)
/// rather than assuming 65535, so this also validates correctly for 12-/14-bit acquisition paths.
/// </summary>
public sealed class DefaultFlatFrameValidator : IFlatFrameValidator
{
    public FlatValidationResult Validate(ImageStatisticsResult stats, FlatValidationRequest request)
    {
        var maxAdu = stats.MaxAdu > 0 ? stats.MaxAdu : 65535d;
        var targetAdu = request.TargetHistogramFraction * maxAdu;
        var toleranceAdu = targetAdu * request.TargetToleranceFraction;
        var measuredFraction = stats.MedianAdu / maxAdu;

        if (!request.AcquisitionSucceeded)
        {
            return Reject(stats, targetAdu, toleranceAdu, "Acquisition failed.");
        }

        if (!request.ImageSaved)
        {
            return Reject(stats, targetAdu, toleranceAdu, "Image was not saved.");
        }

        if (stats.IsCorrupted)
        {
            return Reject(stats, targetAdu, toleranceAdu, stats.CorruptionReason ?? "Corrupted image.");
        }

        if (!string.Equals(request.ExpectedFilterName, request.ActualFilterName, StringComparison.OrdinalIgnoreCase))
        {
            return Reject(stats, targetAdu, toleranceAdu, "Filter mismatch.");
        }

        if (request.ExpectedGain >= 0 && request.ActualGain >= 0 && request.ExpectedGain != request.ActualGain)
        {
            return Reject(stats, targetAdu, toleranceAdu, "Gain mismatch.");
        }

        if (request.ExpectedOffset >= 0 && request.ActualOffset >= 0 && request.ExpectedOffset != request.ActualOffset)
        {
            return Reject(stats, targetAdu, toleranceAdu, $"Offset mismatch.");
        }

        if (stats.SaturatedFraction > request.MaxSaturationFraction)
        {
            return Reject(stats, targetAdu, toleranceAdu, $"Saturation {stats.SaturatedFraction:P1} exceeds limit.");
        }

        var delta = Math.Abs(stats.MedianAdu - targetAdu);
        if (delta > toleranceAdu)
        {
            return Reject(stats, targetAdu, toleranceAdu,
                $"Measured median histogram level {measuredFraction:P1} / {stats.MedianAdu:F0} ADU outside target " +
                $"{request.TargetHistogramFraction:P1} / {targetAdu:F0} ADU (tolerance ±{request.TargetToleranceFraction:P0} of target = ±{toleranceAdu:F0} ADU).");
        }

        return new FlatValidationResult
        {
            IsAccepted = true,
            Reason = "Accepted",
            MeasuredAdu = stats.MedianAdu,
            MeasuredHistogramFraction = measuredFraction,
            MeasuredMeanAdu = stats.MeanAdu,
            TargetAdu = targetAdu,
            ToleranceAdu = toleranceAdu
        };
    }

    private static FlatValidationResult Reject(ImageStatisticsResult stats, double targetAdu, double toleranceAdu, string reason)
    {
        var maxAdu = stats.MaxAdu > 0 ? stats.MaxAdu : 65535d;
        return new FlatValidationResult
        {
            IsAccepted = false,
            Reason = reason,
            MeasuredAdu = stats.MedianAdu,
            MeasuredHistogramFraction = stats.MedianAdu / maxAdu,
            MeasuredMeanAdu = stats.MeanAdu,
            TargetAdu = targetAdu,
            ToleranceAdu = toleranceAdu
        };
    }
}
