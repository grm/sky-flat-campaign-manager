namespace SkyFlatCampaignManager.Core.Acquisition;

/// <summary>
/// Classification of a required (unclamped) exposure relative to a filter's configured
/// [MinExposureSeconds, MaxExposureSeconds] range. This is evaluated against the exposure the
/// sky <i>actually requires</i> to hit the target — before it is clamped into range — so the
/// caller can tell "the filter needs 0.0003s and we can't go that low" apart from "the filter
/// needs exactly the minimum".
/// </summary>
public enum ExposureFeasibility
{
    /// <summary>Required exposure is below MinExposureSeconds (sky is too bright for this filter at any allowed exposure).</summary>
    TooShort,

    /// <summary>Required exposure is within [MinExposureSeconds, MaxExposureSeconds].</summary>
    Feasible,

    /// <summary>Required exposure is above MaxExposureSeconds (sky is too dark for this filter at any allowed exposure).</summary>
    TooLong
}

/// <summary>
/// Result of estimating the next flat exposure. Carries both the physically-required
/// (unclamped) value used for feasibility decisions, and the clamped value that is safe to
/// actually command the camera with.
/// </summary>
public sealed class ExposureEstimateResult
{
    /// <summary>The exposure the sky actually requires to hit the target, before clamping. Use this for feasibility decisions.</summary>
    public double UnclampedExposureSeconds { get; init; }

    /// <summary>The exposure clamped into [MinExposureSeconds, MaxExposureSeconds]. Use this as the next probe/capture exposure.</summary>
    public double ClampedExposureSeconds { get; init; }

    /// <summary>Whether the unclamped exposure falls inside the configured min/max range.</summary>
    public ExposureFeasibility Feasibility { get; init; }
}

public interface IFlatExposureEstimator
{
    /// <summary>
    /// Legacy convenience method that returns only the clamped exposure. Prefer
    /// <see cref="Estimate"/> when a feasibility decision is required, since this method
    /// cannot distinguish "needs exactly the minimum" from "needs far less than the minimum".
    /// </summary>
    double EstimateNextExposureSeconds(
        double currentExposureSeconds,
        double measuredAdu,
        double targetAdu,
        double minExposureSeconds,
        double maxExposureSeconds,
        double? skyTrendFactor = null);

    /// <summary>
    /// Estimates the next exposure and classifies feasibility using the <b>unclamped</b>
    /// required exposure, so callers can detect "no exposure in range will hit target" instead
    /// of silently clamping into a value that will never validate.
    /// </summary>
    ExposureEstimateResult Estimate(
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
        => Estimate(currentExposureSeconds, measuredAdu, targetAdu, minExposureSeconds, maxExposureSeconds, skyTrendFactor)
            .ClampedExposureSeconds;

    public ExposureEstimateResult Estimate(
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

        double unclamped;
        if (measuredAdu < MinMeasuredAdu)
        {
            var boosted = currentExposureSeconds * MaxScaleUp;
            unclamped = boosted * (skyTrendFactor ?? 1.0);
        }
        else
        {
            var scale = targetAdu / measuredAdu;
            scale = Math.Clamp(scale, MaxScaleDown, MaxScaleUp);
            if (skyTrendFactor is > 0)
            {
                scale *= skyTrendFactor.Value;
            }

            unclamped = currentExposureSeconds * scale;
        }

        var clamped = Clamp(unclamped, minExposureSeconds, maxExposureSeconds);
        var feasibility = unclamped < minExposureSeconds
            ? ExposureFeasibility.TooShort
            : unclamped > maxExposureSeconds
                ? ExposureFeasibility.TooLong
                : ExposureFeasibility.Feasible;

        return new ExposureEstimateResult
        {
            UnclampedExposureSeconds = unclamped,
            ClampedExposureSeconds = clamped,
            Feasibility = feasibility
        };
    }

    private static double Clamp(double value, double min, double max)
        => Math.Min(max, Math.Max(min, value));
}
