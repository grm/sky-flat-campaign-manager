using System.ComponentModel.Composition;
using Newtonsoft.Json;
using NINA.Plugin.SkyFlatCampaignManager.Services;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using SkyFlatCampaignManager.Core.Campaigns;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Conditions;

[ExportMetadata("Name", "Sky Flat Campaign Required")]
[ExportMetadata("Description", "True when no valid campaign exists, the campaign expired, is incomplete, or was invalidated.")]
[ExportMetadata("Icon", "CheckedSVG")]
[ExportMetadata("Category", "Sky Flat Campaign Manager")]
[Export(typeof(ISequenceCondition))]
[JsonObject(MemberSerialization.OptIn)]
public class SkyFlatCampaignRequiredCondition : SequenceCondition
{
    [ImportingConstructor]
    public SkyFlatCampaignRequiredCondition()
    {
        CampaignKey = "default";
        MatchIncomplete = true;
        MatchExpired = true;
        MatchInvalidated = true;
        MatchMissing = true;
        MatchCompleted = false;
    }

    [JsonProperty] public string CampaignKey { get; set; }
    [JsonProperty] public bool MatchIncomplete { get; set; }
    [JsonProperty] public bool MatchExpired { get; set; }
    [JsonProperty] public bool MatchInvalidated { get; set; }
    [JsonProperty] public bool MatchMissing { get; set; }
    [JsonProperty] public bool MatchCompleted { get; set; }

    public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem)
    {
        var svc = PluginServiceFactory.CreateCampaignService();
        var options = PluginServiceFactory.CreateOptionsFromSettings();
        var requirement = svc.EvaluateRequirementAsync(CampaignKey, options).GetAwaiter().GetResult();

        if (requirement.NoCampaign) return MatchMissing;
        if (requirement.IsInvalidated) return MatchInvalidated;
        if (requirement.IsExpired) return MatchExpired;
        if (requirement.IsIncomplete) return MatchIncomplete;
        if (requirement.IsCompleted) return MatchCompleted;
        return requirement.IsRequired;
    }

    public override object Clone() => new SkyFlatCampaignRequiredCondition
    {
        Icon = Icon,
        Name = Name,
        Category = Category,
        Description = Description,
        CampaignKey = CampaignKey,
        MatchIncomplete = MatchIncomplete,
        MatchExpired = MatchExpired,
        MatchInvalidated = MatchInvalidated,
        MatchMissing = MatchMissing,
        MatchCompleted = MatchCompleted
    };

    public override string ToString()
        => $"Category: {Category}, Item: {nameof(SkyFlatCampaignRequiredCondition)}, Key={CampaignKey}";
}
