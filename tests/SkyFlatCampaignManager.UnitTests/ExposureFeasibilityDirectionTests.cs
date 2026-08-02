using FluentAssertions;
using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Campaigns;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

/// <summary>
/// The estimator's clamped output alone can never prove infeasibility (it is clamped by
/// construction), so feasibility must be decided from the <b>unclamped</b> required exposure —
/// and whether waiting can help depends on the Morning/Evening direction, not on the
/// classification alone. See <see cref="ExposureFeasibilityRules"/>.
/// </summary>
public class ExposureFeasibilityDirectionTests
{
    private readonly ProportionalFlatExposureEstimator _estimator = new();

    [Fact]
    public void Clamped_result_alone_cannot_reveal_infeasibility()
    {
        // Sky is far too bright for this filter: measured ADU is huge even at the minimum
        // exposure. The clamped output sits right at MinExposureSeconds — indistinguishable from
        // "exactly needs the minimum" unless you also look at the unclamped value.
        var result = _estimator.Estimate(currentExposureSeconds: 1, measuredAdu: 500_000, targetAdu: 25000, minExposureSeconds: 1, maxExposureSeconds: 30);
        result.ClampedExposureSeconds.Should().Be(1);
        result.UnclampedExposureSeconds.Should().BeLessThan(1);
        result.Feasibility.Should().Be(ExposureFeasibility.TooShort);
    }

    [Fact]
    public void Evening_required_exposure_below_minimum_is_too_short_and_waiting_may_help()
    {
        // Sky still bright: even the shortest useful exposure would overexpose past target.
        var result = _estimator.Estimate(currentExposureSeconds: 1, measuredAdu: 200_000, targetAdu: 25000, minExposureSeconds: 1, maxExposureSeconds: 30);
        result.Feasibility.Should().Be(ExposureFeasibility.TooShort);
        ExposureFeasibilityRules.CanImproveByWaiting(CampaignMode.Evening, result.Feasibility).Should().BeTrue();
    }

    [Fact]
    public void Evening_required_exposure_above_maximum_is_too_long_and_waiting_will_not_help()
    {
        // Sky already too dark for this filter: even the longest allowed exposure undershoots target.
        var result = _estimator.Estimate(currentExposureSeconds: 20, measuredAdu: 500, targetAdu: 25000, minExposureSeconds: 1, maxExposureSeconds: 30);
        result.Feasibility.Should().Be(ExposureFeasibility.TooLong);
        ExposureFeasibilityRules.CanImproveByWaiting(CampaignMode.Evening, result.Feasibility).Should().BeFalse();
    }

    [Fact]
    public void Morning_required_exposure_above_maximum_is_too_long_and_waiting_may_help()
    {
        // Sky still dark: even the longest allowed exposure undershoots target — brightening sky
        // will bring the required exposure down over time.
        var result = _estimator.Estimate(currentExposureSeconds: 20, measuredAdu: 500, targetAdu: 25000, minExposureSeconds: 1, maxExposureSeconds: 30);
        result.Feasibility.Should().Be(ExposureFeasibility.TooLong);
        ExposureFeasibilityRules.CanImproveByWaiting(CampaignMode.Morning, result.Feasibility).Should().BeTrue();
    }

    [Fact]
    public void Morning_required_exposure_below_minimum_is_too_short_and_waiting_will_not_help()
    {
        // Sky already too bright for this filter: waiting only brightens it further.
        var result = _estimator.Estimate(currentExposureSeconds: 1, measuredAdu: 200_000, targetAdu: 25000, minExposureSeconds: 1, maxExposureSeconds: 30);
        result.Feasibility.Should().Be(ExposureFeasibility.TooShort);
        ExposureFeasibilityRules.CanImproveByWaiting(CampaignMode.Morning, result.Feasibility).Should().BeFalse();
    }

    [Fact]
    public void Feasible_exposure_never_reports_improvement_possible()
    {
        var result = _estimator.Estimate(currentExposureSeconds: 1, measuredAdu: 25000, targetAdu: 25000, minExposureSeconds: 0.01, maxExposureSeconds: 30);
        result.Feasibility.Should().Be(ExposureFeasibility.Feasible);
        ExposureFeasibilityRules.CanImproveByWaiting(CampaignMode.Evening, result.Feasibility).Should().BeFalse();
        ExposureFeasibilityRules.CanImproveByWaiting(CampaignMode.Morning, result.Feasibility).Should().BeFalse();
    }

    [Fact]
    public void Legacy_EstimateNextExposureSeconds_matches_Estimate_clamped_value()
    {
        var legacy = _estimator.EstimateNextExposureSeconds(2, 50000, 25000, 0.01, 30);
        var full = _estimator.Estimate(2, 50000, 25000, 0.01, 30);
        legacy.Should().Be(full.ClampedExposureSeconds);
    }
}
