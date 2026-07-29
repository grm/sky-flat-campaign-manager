namespace SkyFlatCampaignManager.Core.Acquisition;

public interface IFlatFrameValidator
{
    FlatValidationResult Validate(ImageStatisticsResult stats, FlatValidationRequest request);
}

public sealed class FlatValidationRequest
{
    public double TargetAdu { get; init; }
    public double AduTolerance { get; init; }
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
    public double MeasuredAdu { get; init; }
}

public sealed class DefaultFlatFrameValidator : IFlatFrameValidator
{
    public FlatValidationResult Validate(ImageStatisticsResult stats, FlatValidationRequest request)
    {
        if (!request.AcquisitionSucceeded)
        {
            return Reject(stats.MedianAdu, "Acquisition failed.");
        }

        if (!request.ImageSaved)
        {
            return Reject(stats.MedianAdu, "Image was not saved.");
        }

        if (stats.IsCorrupted)
        {
            return Reject(stats.MedianAdu, stats.CorruptionReason ?? "Corrupted image.");
        }

        if (!string.Equals(request.ExpectedFilterName, request.ActualFilterName, StringComparison.OrdinalIgnoreCase))
        {
            return Reject(stats.MedianAdu, "Filter mismatch.");
        }

        if (request.ExpectedGain >= 0 && request.ActualGain >= 0 && request.ExpectedGain != request.ActualGain)
        {
            return Reject(stats.MedianAdu, "Gain mismatch.");
        }

        if (request.ExpectedOffset >= 0 && request.ActualOffset >= 0 && request.ExpectedOffset != request.ActualOffset)
        {
            return Reject(stats.MedianAdu, "Offset mismatch.");
        }

        if (stats.SaturatedFraction > request.MaxSaturationFraction)
        {
            return Reject(stats.MedianAdu, $"Saturation {stats.SaturatedFraction:P1} exceeds limit.");
        }

        var delta = Math.Abs(stats.MedianAdu - request.TargetAdu);
        if (delta > request.AduTolerance)
        {
            return Reject(stats.MedianAdu, $"ADU {stats.MedianAdu:F0} outside tolerance of {request.TargetAdu:F0}±{request.AduTolerance:F0}.");
        }

        return new FlatValidationResult
        {
            IsAccepted = true,
            Reason = "Accepted",
            MeasuredAdu = stats.MedianAdu
        };
    }

    private static FlatValidationResult Reject(double adu, string reason) => new()
    {
        IsAccepted = false,
        Reason = reason,
        MeasuredAdu = adu
    };
}
