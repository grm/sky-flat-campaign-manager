namespace SkyFlatCampaignManager.Core.Acquisition;

public interface IFlatExposureEstimator
{
    double EstimateNextExposureSeconds(
        double currentExposureSeconds,
        double measuredAdu,
        double targetAdu,
        double minExposureSeconds,
        double maxExposureSeconds,
        double? skyTrendFactor = null);
}

public sealed class ProportionalFlatExposureEstimator : IFlatExposureEstimator
{
    public const double MaxScaleUp = 4.0;
    public const double MaxScaleDown = 0.25;
    public const double MinMeasuredAdu = 1.0;

    public double EstimateNextExposureSeconds(
        double currentExposureSeconds,
        double measuredAdu,
        double targetAdu,
        double minExposureSeconds,
        double maxExposureSeconds,
        double? skyTrendFactor = null)
    {
        if (currentExposureSeconds <= 0)
        {
            currentExposureSeconds = Math.Max(minExposureSeconds, 0.1);
        }

        if (measuredAdu < MinMeasuredAdu)
        {
            var boosted = currentExposureSeconds * MaxScaleUp;
            return Clamp(boosted * (skyTrendFactor ?? 1.0), minExposureSeconds, maxExposureSeconds);
        }

        var scale = targetAdu / measuredAdu;
        scale = Math.Clamp(scale, MaxScaleDown, MaxScaleUp);
        if (skyTrendFactor is > 0)
        {
            scale *= skyTrendFactor.Value;
        }

        return Clamp(currentExposureSeconds * scale, minExposureSeconds, maxExposureSeconds);
    }

    private static double Clamp(double value, double min, double max)
        => Math.Min(max, Math.Max(min, value));
}
