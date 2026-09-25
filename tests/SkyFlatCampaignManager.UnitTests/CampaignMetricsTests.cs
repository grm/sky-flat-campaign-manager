using FluentAssertions;
using SkyFlatCampaignManager.Core.Campaigns;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

public class CampaignMetricsTests
{
    private static readonly List<FilterCampaignSettings> Filters = new()
    {
        new() { FilterName = "L", Enabled = true, TargetCount = 30 },
        new() { FilterName = "Ha", Enabled = true, TargetCount = 30 },
        new() { FilterName = "Unused", Enabled = false, TargetCount = 30 }
    };

    [Fact]
    public void Missing_campaign_reports_full_configured_target()
    {
        CampaignMetrics.FlatsRemaining(new CampaignRequirement { IsRequired = true, NoCampaign = true }, Filters)
            .Should().Be(60);
    }

    [Fact]
    public void Expired_completed_campaign_reports_fresh_full_target()
    {
        var old = new CampaignState { Status = CampaignStatus.Completed };
        old.Filters["L"] = new FilterProgress { FilterName = "L", Target = 30, Accepted = 30 };
        old.Filters["Ha"] = new FilterProgress { FilterName = "Ha", Target = 30, Accepted = 30 };
        CampaignMetrics.FlatsRemaining(new CampaignRequirement { IsRequired = true, IsExpired = true, Campaign = old }, Filters)
            .Should().Be(60);
    }

    [Fact]
    public void Incomplete_campaign_decrements_with_accepted_flats()
    {
        var state = new CampaignState { Status = CampaignStatus.InProgress };
        state.Filters["L"] = new FilterProgress { FilterName = "L", Target = 30, Accepted = 12 };
        state.Filters["Ha"] = new FilterProgress { FilterName = "Ha", Target = 30, Accepted = 5 };
        CampaignMetrics.FlatsRemaining(new CampaignRequirement { IsRequired = true, IsIncomplete = true, Campaign = state }, Filters)
            .Should().Be(43);
    }

    [Fact]
    public void Valid_completed_campaign_reports_zero()
    {
        var state = new CampaignState { Status = CampaignStatus.Completed };
        state.Filters["L"] = new FilterProgress { FilterName = "L", Target = 30, Accepted = 30 };
        state.Filters["Ha"] = new FilterProgress { FilterName = "Ha", Target = 30, Accepted = 30 };
        CampaignMetrics.FlatsRemaining(new CampaignRequirement { IsCompleted = true, Campaign = state }, Filters)
            .Should().Be(0);
    }
}
