using FluentAssertions;
using SkyFlatCampaignManager.Core.Acquisition;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

public class ExposureEstimatorTests
{
    private readonly ProportionalFlatExposureEstimator _estimator = new();

    [Fact]
    public void Scales_up_when_too_dark()
    {
        var next = _estimator.EstimateNextExposureSeconds(1, 5000, 25000, 0.01, 30);
        // Proportional would be 5s but MaxScaleUp caps at 4×.
        next.Should().BeApproximately(4, 0.01);
    }

    [Fact]
    public void Scales_down_when_too_bright()
    {
        var next = _estimator.EstimateNextExposureSeconds(2, 50000, 25000, 0.01, 30);
        next.Should().BeApproximately(1, 0.01);
    }

    [Fact]
    public void Within_tolerance_path_still_returns_bounded_value()
    {
        var next = _estimator.EstimateNextExposureSeconds(1, 25000, 25000, 0.01, 30);
        next.Should().BeApproximately(1, 0.01);
    }

    [Fact]
    public void Zero_adu_boosts_within_cap()
    {
        var next = _estimator.EstimateNextExposureSeconds(1, 0, 25000, 0.01, 30);
        next.Should().Be(4);
    }

    [Fact]
    public void Honors_min_max()
    {
        var next = _estimator.EstimateNextExposureSeconds(0.001, 100, 25000, 0.5, 2);
        // Scale capped at 4× → 0.004, then raised to min exposure 0.5.
        next.Should().Be(0.5);
        var bright = _estimator.EstimateNextExposureSeconds(10, 100, 25000, 0.5, 2);
        bright.Should().Be(2);
    }

    [Fact]
    public void Validator_rejects_saturation_and_out_of_tolerance()
    {
        var validator = new DefaultFlatFrameValidator();
        var stats = new ImageStatisticsResult { MedianAdu = 40000, SaturatedFraction = 0.2 };
        var result = validator.Validate(stats, new FlatValidationRequest
        {
            TargetHistogramFraction = 25000d / 65535d,
            TargetToleranceFraction = 0.10,
            ExpectedFilterName = "L",
            ActualFilterName = "L"
        });
        result.IsAccepted.Should().BeFalse();
    }

    [Fact]
    public void Validator_accepts_good_frame()
    {
        var validator = new DefaultFlatFrameValidator();
        var stats = new ImageStatisticsResult { MedianAdu = 24500, SaturatedFraction = 0 };
        var result = validator.Validate(stats, new FlatValidationRequest
        {
            TargetHistogramFraction = 25000d / 65535d,
            TargetToleranceFraction = 0.10,
            ExpectedFilterName = "Ha",
            ActualFilterName = "Ha",
            ImageSaved = true,
            AcquisitionSucceeded = true
        });
        result.IsAccepted.Should().BeTrue();
    }

    [Fact]
    public void Validator_uses_actual_image_bit_depth_not_65535()
    {
        // 12-bit image: full scale 4095. 40% target = 1638 ADU, ±10% of target = ±163.8 ADU.
        var validator = new DefaultFlatFrameValidator();
        var stats = new ImageStatisticsResult { MedianAdu = 1638, MaxAdu = 4095, SaturatedFraction = 0 };
        var result = validator.Validate(stats, new FlatValidationRequest
        {
            TargetHistogramFraction = 0.40,
            TargetToleranceFraction = 0.10,
            ExpectedFilterName = "L",
            ActualFilterName = "L"
        });
        result.IsAccepted.Should().BeTrue();
        result.MeasuredHistogramFraction.Should().BeApproximately(0.40, 0.001);
        result.TargetAdu.Should().BeApproximately(1638, 0.5);
    }

    [Fact]
    public void Validator_tolerance_is_percentage_of_target_not_of_full_scale()
    {
        // Target 40% of a 16-bit range, tolerance 10% of target -> accepted range is 36%-44%,
        // not 30%-50% (which a fixed-full-scale-percentage-points interpretation would give).
        var validator = new DefaultFlatFrameValidator();
        var maxAdu = 65535d;
        var target = 0.40 * maxAdu;

        var justInside = new ImageStatisticsResult { MedianAdu = 0.361 * maxAdu, MaxAdu = maxAdu };
        var justOutside = new ImageStatisticsResult { MedianAdu = 0.359 * maxAdu, MaxAdu = maxAdu };
        var request = new FlatValidationRequest { TargetHistogramFraction = 0.40, TargetToleranceFraction = 0.10, ExpectedFilterName = "L", ActualFilterName = "L" };

        validator.Validate(justInside, request).IsAccepted.Should().BeTrue();
        validator.Validate(justOutside, request).IsAccepted.Should().BeFalse();
        _ = target;
    }

    [Fact]
    public void Validator_validates_median_and_reports_mean_as_diagnostic_only()
    {
        var validator = new DefaultFlatFrameValidator();
        // Median is within tolerance, mean is not — acceptance must follow the median.
        var stats = new ImageStatisticsResult { MedianAdu = 25000, MeanAdu = 40000, MaxAdu = 65535 };
        var result = validator.Validate(stats, new FlatValidationRequest
        {
            TargetHistogramFraction = 25000d / 65535d,
            TargetToleranceFraction = 0.10,
            ExpectedFilterName = "L",
            ActualFilterName = "L"
        });
        result.IsAccepted.Should().BeTrue();
        result.MeasuredMeanAdu.Should().Be(40000);
    }

    [Fact]
    public void Robust_stats_compute_median()
    {
        var buf = new ushort[100 * 100];
        Array.Fill(buf, (ushort)1000);
        buf[5050] = 60000; // outlier / star
        var stats = RobustImageStatisticsCalculator.Compute(buf, 100, 100, 0.5);
        stats.MedianAdu.Should().Be(1000);
        stats.SamplePixelCount.Should().BeGreaterThan(0);
    }
}
