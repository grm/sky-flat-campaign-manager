using System.ComponentModel.Composition;
using Newtonsoft.Json;
using NINA.Plugin.SkyFlatCampaignManager.Services;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using SkyFlatCampaignManager.Core.Campaigns;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Conditions;

[ExportMetadata("Name", "Sky Flats Remaining")]
[ExportMetadata("Description", "True while the selected campaign still needs accepted flats. Missing/expired/invalidated campaigns expose the full configured target; the count decreases after each accepted flat.")]
[ExportMetadata("Icon", "CheckedSVG")]
[ExportMetadata("Category", "Sky Flat Campaign Manager")]
[Export(typeof(ISequenceCondition))]
[JsonObject(MemberSerialization.OptIn)]
public class SkyFlatFlatsRemainingCondition : SequenceCondition
{
    private readonly IProfileService _profileService;

    [ImportingConstructor]
    public SkyFlatFlatsRemainingCondition(IProfileService profileService)
    {
        _profileService = profileService;
        CampaignKey = "default";
        MinimumRemaining = 1;
    }

    private SkyFlatFlatsRemainingCondition(SkyFlatFlatsRemainingCondition copyMe) : this(copyMe._profileService)
    {
        Icon = copyMe.Icon;
        Name = copyMe.Name;
        Category = copyMe.Category;
        Description = copyMe.Description;
        CampaignKey = copyMe.CampaignKey;
        MinimumRemaining = copyMe.MinimumRemaining;
    }

    [JsonProperty] public string CampaignKey { get; set; }
    [JsonProperty] public int MinimumRemaining { get; set; }

    /// <summary>Live informational value refreshed every time NINA evaluates the condition.</summary>
    public int Remaining { get; private set; }

    public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem)
    {
        var service = PluginServiceFactory.CreateCampaignService();
        var options = PluginServiceFactory.CreateOptionsFromSettings();
        var filters = PluginServiceFactory.CreateFilterSettings(_profileService);
        var requirement = service.EvaluateRequirementAsync(CampaignKey, options).GetAwaiter().GetResult();
        Remaining = CampaignMetrics.FlatsRemaining(requirement, filters);
        RaisePropertyChanged(nameof(Remaining));
        return Remaining >= Math.Max(1, MinimumRemaining);
    }

    public override object Clone() => new SkyFlatFlatsRemainingCondition(this);

    public override string ToString() =>
        $"Category: {Category}, Item: {nameof(SkyFlatFlatsRemainingCondition)}, Key={CampaignKey}, Remaining={Remaining}";
}
