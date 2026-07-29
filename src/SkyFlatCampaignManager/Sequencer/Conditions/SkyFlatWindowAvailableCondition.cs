using System.ComponentModel.Composition;
using Newtonsoft.Json;
using NINA.Plugin.SkyFlatCampaignManager.Adapters;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using SkyFlatCampaignManager.Core.Astronomy;
using SkyFlatCampaignManager.Core.Campaigns;
using NINA.Plugin.SkyFlatCampaignManager.Services;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Conditions;

[ExportMetadata("Name", "Sky Flat Window Available")]
[ExportMetadata("Description", "True when the configured solar-altitude safety window for evening or morning is open.")]
[ExportMetadata("Icon", "SunriseSVG")]
[ExportMetadata("Category", "Sky Flat Campaign Manager")]
[Export(typeof(ISequenceCondition))]
[JsonObject(MemberSerialization.OptIn)]
public class SkyFlatWindowAvailableCondition : SequenceCondition
{
    private readonly IProfileService _profileService;

    [ImportingConstructor]
    public SkyFlatWindowAvailableCondition(IProfileService profileService)
    {
        _profileService = profileService;
        Mode = CampaignMode.Automatic;
    }

    private SkyFlatWindowAvailableCondition(SkyFlatWindowAvailableCondition copyMe) : this(copyMe._profileService)
    {
        Icon = copyMe.Icon;
        Name = copyMe.Name;
        Category = copyMe.Category;
        Description = copyMe.Description;
        Mode = copyMe.Mode;
    }

    [JsonProperty] public CampaignMode Mode { get; set; }

    public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem)
    {
        var options = PluginServiceFactory.CreateOptionsFromSettings();
        var sun = new NinaSunAltitudeProvider(_profileService);
        var alt = sun.GetSunAltitudeDegrees(DateTime.UtcNow);
        var windows = new AstronomicalWindowService();
        var mode = Mode;
        if (mode == CampaignMode.Automatic)
        {
            mode = windows.ResolveMode(CampaignMode.Automatic, alt, alt);
        }

        var window = mode == CampaignMode.Morning ? options.MorningWindow : options.EveningWindow;
        return windows.IsWithinSafetyWindow(alt, window);
    }

    public override object Clone() => new SkyFlatWindowAvailableCondition(this);

    public override string ToString()
        => $"Category: {Category}, Item: {nameof(SkyFlatWindowAvailableCondition)}, Mode={Mode}";
}
