using FluentAssertions;
using SkyFlatCampaignManager.Core;
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
}
