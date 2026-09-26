using FluentAssertions;
using SkyFlatCampaignManager.Core.Campaigns;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

public class SkyFlatEventMessageFormatterTests
{
    private static SkyFlatSessionEvent Sample(SkyFlatSessionEventKind kind)
    {
        var campaign = new CampaignState
        {
            Filters = new Dictionary<string, FilterProgress>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ha"] = new()
                {
                    FilterName = "Ha",
                    Target = 30,
                    Accepted = 18
                }
            }
        };

        return new SkyFlatSessionEvent
        {
            Kind = kind,
            CampaignKey = "default",
            Mode = CampaignMode.Morning,
            Campaign = campaign,
            CurrentFilter = "Ha",
            ConfiguredTarget = 210,
            Remaining = 72,
            AcceptedThisSession = 12,
            RejectedThisSession = 2,
            ExposureSeconds = 7.425,
            MeasuredAdu = 32740,
            MeasuredHistogramFraction = 0.4995,
            SunAltitudeDegrees = -5.31,
            WaitReason = "FilterNotFeasible",
            StopReason = "MorningSkyTooBright",
            Duration = TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(34)
        };
    }

    [Fact]
    public void Custom_template_resolves_all_public_hook_values()
    {
        var e = Sample(SkyFlatSessionEventKind.BeforeFilter);
        var template = "{campaign}|{mode}|{state}|{remaining}/{required}|{accepted}|{sessionAccepted}/{sessionRejected}|{filter}:{filterAccepted}/{filterRequired}:{filterRemaining}|{exposure}|{adu}|{histogram}|{sunAltitude}|{waitReason}|{stopReason}|{duration}";

        var result = SkyFlatEventMessageFormatter.ResolveTemplate(template, e);

        result.Should().Be("default|Morning|BeforeFilter|72/210|18|12/2|Ha:18/30:12|7.425|32740|50.0|-5.31|FilterNotFeasible|MorningSkyTooBright|00:12:34");
    }

    [Fact]
    public void Blank_template_uses_contextual_default()
    {
        var e = Sample(SkyFlatSessionEventKind.BeforeFilter);

        SkyFlatEventMessageFormatter.ResolveTemplate("", e)
            .Should().Be("SFCM: starting Ha — 12 for this filter, 72 total remaining");
    }

    [Fact]
    public void Skip_default_is_unambiguous()
    {
        var e = new SkyFlatSessionEvent
        {
            Kind = SkyFlatSessionEventKind.CampaignNotRequired,
            CampaignKey = "default",
            Mode = CampaignMode.Morning,
            ConfiguredTarget = 210,
            Remaining = 0
        };

        SkyFlatEventMessageFormatter.FormatDefault(e)
            .Should().Be("SFCM: sky flats skipped — campaign is current (0 remaining)");
    }
    [Fact]
    public void Automatic_mode_default_does_not_repeat_sky_word()
    {
        var e = new SkyFlatSessionEvent
        {
            Kind = SkyFlatSessionEventKind.CampaignRequired,
            CampaignKey = "default",
            Mode = CampaignMode.Automatic,
            ConfiguredTarget = 210,
            Remaining = 140
        };

        SkyFlatEventMessageFormatter.FormatDefault(e)
            .Should().Be("SFCM: sky flats starting — 140 flat(s) remaining");
    }

    [Fact]
    public void Late_evening_start_uses_clear_skip_message()
    {
        var e = new SkyFlatSessionEvent
        {
            Kind = SkyFlatSessionEventKind.SessionIncomplete,
            CampaignKey = "default",
            Mode = CampaignMode.Evening,
            Remaining = 140,
            StopReason = SessionStopReasons.EveningStartTooLate,
            SunAltitudeDegrees = -10.6
        };

        SkyFlatEventMessageFormatter.FormatDefault(e)
            .Should().Be("SFCM: evening sky flats skipped — twilight too advanced — 140 remaining");
    }


}
