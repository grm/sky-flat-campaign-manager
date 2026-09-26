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

    [Fact]
    public void Campaign_container_read_only_status_bindings_are_one_way()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? xamlPath = null;
        while (dir is not null && xamlPath is null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SkyFlatCampaignManager", "Sequencer", "SequencerTemplates.xaml");
            if (File.Exists(candidate)) xamlPath = candidate;
            dir = dir.Parent;
        }

        xamlPath.Should().NotBeNull("the integration test should be running from a repository checkout");
        var xaml = File.ReadAllText(xamlPath!);

        foreach (var property in new[] { "FlatsRequired", "FlatsAccepted", "FlatsRemaining", "CurrentFilter", "ProgressText" })
        {
            xaml.Should().Contain($"{{Binding {property}, Mode=OneWay}}",
                $"'{property}' is read-only and WPF must never attempt to write back to it");
            xaml.Should().NotContain($"{{Binding {property}}}",
                $"an implicit binding mode can become TwoWay for some WPF targets and crash the sequencer UI");
        }
    }

}
