using FluentAssertions;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Filters;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

public class FilterStrategyTests
{
    private static (List<FilterCampaignSettings> filters, CampaignState campaign) Setup()
    {
        var filters = new List<FilterCampaignSettings>
        {
            new() { FilterName = "L", Enabled = true, TargetCount = 50, ManualEveningOrder = 3, ManualMorningOrder = 1, Priority = 2, MaxExposureSeconds = 2 },
            new() { FilterName = "Ha", Enabled = true, TargetCount = 50, ManualEveningOrder = 1, ManualMorningOrder = 3, Priority = 1, MaxExposureSeconds = 20 },
            new() { FilterName = "OIII", Enabled = false, TargetCount = 50 }
        };
        var campaign = new CampaignState
        {
            Status = CampaignStatus.InProgress,
            Filters =
            {
                ["L"] = new FilterProgress { FilterName = "L", Target = 50, Accepted = 50, LastExposureSeconds = 0.4 },
                ["Ha"] = new FilterProgress { FilterName = "Ha", Target = 50, Accepted = 10, LastExposureSeconds = 8 },
                ["OIII"] = new FilterProgress { FilterName = "OIII", Target = 50, Accepted = 0, LastExposureSeconds = 10 }
            }
        };
        return (filters, campaign);
    }

    [Fact]
    public void Evening_manual_prefers_configured_order_among_incomplete()
    {
        var (filters, campaign) = Setup();
        var strategy = new ManualOrderFilterSelectionStrategy();
        var next = strategy.SelectNext(filters, campaign, CampaignMode.Evening, new FilterSelectionContext());
        next!.FilterName.Should().Be("Ha");
    }

    [Fact]
    public void Highest_exposure_first()
    {
        var (filters, campaign) = Setup();
        campaign.Filters["L"].Accepted = 0;
        var next = new HighestExposureFirstStrategy().SelectNext(filters, campaign, CampaignMode.Evening, new FilterSelectionContext());
        next!.FilterName.Should().Be("Ha");
    }

    [Fact]
    public void Lowest_exposure_first_morning()
    {
        var (filters, campaign) = Setup();
        campaign.Filters["L"].Accepted = 0;
        var next = new LowestExposureFirstStrategy().SelectNext(filters, campaign, CampaignMode.Morning, new FilterSelectionContext());
        next!.FilterName.Should().Be("L");
    }

    [Fact]
    public void Adaptive_keeps_current_filter()
    {
        var (filters, campaign) = Setup();
        campaign.Filters["L"].Accepted = 0;
        var next = new AdaptiveFilterSelectionStrategy().SelectNext(filters, campaign, CampaignMode.Evening, new FilterSelectionContext
        {
            CurrentFilterName = "L"
        });
        next!.FilterName.Should().Be("L");
    }

    [Fact]
    public void Disabled_and_complete_filters_skipped()
    {
        var (filters, campaign) = Setup();
        var next = new UserPriorityFilterSelectionStrategy().SelectNext(filters, campaign, CampaignMode.Evening, new FilterSelectionContext());
        next!.FilterName.Should().Be("Ha");
    }

    [Fact]
    public void Missing_wheel_filter_not_selected_when_not_in_candidates()
    {
        var (_, campaign) = Setup();
        var onlyL = new List<FilterCampaignSettings> { new() { FilterName = "L", Enabled = true, TargetCount = 50 } };
        campaign.Filters["L"].Accepted = 50;
        var next = new AdaptiveFilterSelectionStrategy().SelectNext(onlyL, campaign, CampaignMode.Evening, new FilterSelectionContext());
        next.Should().BeNull();
    }
}
