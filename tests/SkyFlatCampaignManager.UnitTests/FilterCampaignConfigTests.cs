using FluentAssertions;
using SkyFlatCampaignManager.Core.Campaigns;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

public class FilterCampaignConfigTests
{
    [Fact]
    public void Merge_overlays_saved_settings_by_filter_name()
    {
        var defaults = new FilterCampaignDefaults { TargetCount = 50, TargetAdu = 25000, Gain = -1 };
        var saved = new List<FilterCampaignSettings>
        {
            new() { FilterName = "Ha", TargetCount = 40, Gain = 120, Offset = 30, TargetAdu = 28000, Enabled = true }
        };

        var merged = JsonFilterCampaignConfigRepository.MergeWithWheel(
            new[] { "L", "Ha", "OIII" },
            saved,
            defaults);

        merged.Should().HaveCount(3);
        merged[0].FilterName.Should().Be("L");
        merged[0].Gain.Should().Be(-1);
        merged[0].TargetCount.Should().Be(50);

        merged[1].FilterName.Should().Be("Ha");
        merged[1].Gain.Should().Be(120);
        merged[1].Offset.Should().Be(30);
        merged[1].TargetCount.Should().Be(40);
        merged[1].TargetAdu.Should().Be(28000);

        merged[2].FilterName.Should().Be("OIII");
        merged[2].Gain.Should().Be(-1);
    }

    [Fact]
    public void Merge_uses_seed_when_no_saved_entry()
    {
        var defaults = new FilterCampaignDefaults { TargetCount = 50, Gain = -1 };
        var seed = new Dictionary<string, FilterCampaignSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["SII"] = new FilterCampaignSettings { FilterName = "SII", Gain = 200, Offset = 10, TargetCount = 35 }
        };

        var merged = JsonFilterCampaignConfigRepository.MergeWithWheel(
            new[] { "SII" },
            saved: null,
            defaults,
            seed);

        merged.Should().ContainSingle();
        merged[0].Gain.Should().Be(200);
        merged[0].Offset.Should().Be(10);
        merged[0].TargetCount.Should().Be(35);
    }
}
