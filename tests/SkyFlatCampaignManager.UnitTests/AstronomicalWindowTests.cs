using FluentAssertions;
using SkyFlatCampaignManager.Core.Astronomy;
using SkyFlatCampaignManager.Core.Campaigns;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

/// <summary>
/// Direction-aware astronomical window classification. A window [-15°, 0°] behaves very
/// differently depending on whether the sun is approaching it (evening, falling) or has already
/// passed through it going the other way (morning would be rising) — see
/// <see cref="AstronomicalWindowService.Evaluate"/>.
/// </summary>
public class AstronomicalWindowTests
{
    private static readonly AstronomicalWindowOptions Window = new()
    {
        MinSunAltitudeDegrees = -15,
        MaxSunAltitudeDegrees = 0
    };

    private readonly AstronomicalWindowService _service = new();

    [Theory]
    [InlineData(2, AstronomicalWindowState.TooEarly)]   // Sun still high — dusk hasn't started yet.
    [InlineData(0, AstronomicalWindowState.Open)]        // Boundary: at MaxSunAltitudeDegrees.
    [InlineData(-5, AstronomicalWindowState.Open)]
    [InlineData(-15, AstronomicalWindowState.Open)]      // Boundary: at MinSunAltitudeDegrees.
    [InlineData(-16, AstronomicalWindowState.TooLate)]  // Already past astronomical dusk — too dark.
    public void Evening_window_classification(double altitude, AstronomicalWindowState expected)
    {
        _service.Evaluate(CampaignMode.Evening, altitude, Window).Should().Be(expected);
    }

    [Theory]
    [InlineData(-16, AstronomicalWindowState.TooEarly)] // Still deep night — dawn hasn't started yet.
    [InlineData(-15, AstronomicalWindowState.Open)]
    [InlineData(-5, AstronomicalWindowState.Open)]
    [InlineData(0, AstronomicalWindowState.Open)]
    [InlineData(1, AstronomicalWindowState.TooLate)]    // Sun already above the window — too bright.
    public void Morning_window_classification(double altitude, AstronomicalWindowState expected)
    {
        _service.Evaluate(CampaignMode.Morning, altitude, Window).Should().Be(expected);
    }

    [Fact]
    public void Evaluate_requires_resolved_mode()
    {
        var act = () => _service.Evaluate(CampaignMode.Automatic, -5, Window);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-5, true)]
    [InlineData(2, false)]
    [InlineData(-16, false)]
    public void IsWithinSafetyWindow_is_direction_agnostic_open_check(double altitude, bool expectedOpen)
    {
        _service.IsWithinSafetyWindow(altitude, Window).Should().Be(expectedOpen);
        // Must agree with Evaluate for both directions since the Open range itself is symmetric.
        (_service.Evaluate(CampaignMode.Evening, altitude, Window) == AstronomicalWindowState.Open).Should().Be(expectedOpen);
        (_service.Evaluate(CampaignMode.Morning, altitude, Window) == AstronomicalWindowState.Open).Should().Be(expectedOpen);
    }

    [Fact]
    public void ResolveMode_falling_sun_is_evening_rising_sun_is_morning()
    {
        _service.ResolveMode(CampaignMode.Automatic, -5, 0).Should().Be(CampaignMode.Evening);
        _service.ResolveMode(CampaignMode.Automatic, -5, -10).Should().Be(CampaignMode.Morning);
        _service.ResolveMode(CampaignMode.Evening, 5, 0).Should().Be(CampaignMode.Evening);
        _service.ResolveMode(CampaignMode.Morning, 5, 0).Should().Be(CampaignMode.Morning);
    }
}
