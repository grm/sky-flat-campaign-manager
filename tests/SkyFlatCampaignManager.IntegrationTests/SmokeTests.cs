using FluentAssertions;
using SkyFlatCampaignManager.Core;
using NINA.Plugin.SkyFlatCampaignManager.Sequencer.Containers;
using Xunit;

namespace SkyFlatCampaignManager.IntegrationTests;

public class SmokeTests
{
    [Fact]
    public void Plugin_identity_is_centralized()
    {
        PluginIdentity.DisplayName.Should().Be("Sky Flat Campaign Manager");
        PluginIdentity.PluginGuid.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Sky_flat_event_container_clone_preserves_event_and_template_without_parent()
    {
        var source = new SkyFlatEventContainer
        {
            EventType = SkyFlatEventType.CampaignRequired,
            MessageTemplate = "Starting {remaining} flats"
        };

        var clone = (SkyFlatEventContainer)source.Clone();

        clone.EventType.Should().Be(SkyFlatEventType.CampaignRequired);
        clone.MessageTemplate.Should().Be("Starting {remaining} flats");
        clone.Items.Should().BeEmpty();
    }
}
