using System.ComponentModel.Composition;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Plugin.SkyFlatCampaignManager.Services;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using SkyFlatCampaignManager.Core.Campaigns;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Instructions;

public enum ResetCampaignAction
{
    Invalidate = 0,
    ResetAll = 1,
    ResetFilter = 2,
    ForceNew = 3,
    ExtendValidity = 4
}

[ExportMetadata("Name", "Reset or Invalidate Sky Flat Campaign")]
[ExportMetadata("Description", "Creates, invalidates, or resets a sky flat campaign / filter.")]
[ExportMetadata("Icon", "StopSVG")]
[ExportMetadata("Category", "Sky Flat Campaign Manager")]
[Export(typeof(ISequenceItem))]
[JsonObject(MemberSerialization.OptIn)]
public class ResetSkyFlatCampaignInstruction : SequenceItem
{
    private readonly IProfileService _profileService;

    [ImportingConstructor]
    public ResetSkyFlatCampaignInstruction(IProfileService profileService)
    {
        _profileService = profileService;
        CampaignKey = "default";
        Action = ResetCampaignAction.Invalidate;
        Reason = "Manual invalidation";
        FilterName = string.Empty;
        ExtendValidityDays = 60;
    }

    private ResetSkyFlatCampaignInstruction(ResetSkyFlatCampaignInstruction copyMe) : this(copyMe._profileService)
    {
        CopyMetaData(copyMe);
        CampaignKey = copyMe.CampaignKey;
        Action = copyMe.Action;
        Reason = copyMe.Reason;
        FilterName = copyMe.FilterName;
        ExtendValidityDays = copyMe.ExtendValidityDays;
    }

    [JsonProperty] public string CampaignKey { get; set; }
    [JsonProperty] public ResetCampaignAction Action { get; set; }
    [JsonProperty] public string Reason { get; set; }
    [JsonProperty] public string FilterName { get; set; }
    [JsonProperty] public int ExtendValidityDays { get; set; }

    public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
    {
        var svc = PluginServiceFactory.CreateCampaignService();
        var options = PluginServiceFactory.CreateOptionsFromSettings();
        var filters = PluginServiceFactory.CreateFilterSettings(_profileService);
        var profileId = _profileService.ActiveProfile.Id.ToString();

        switch (Action)
        {
            case ResetCampaignAction.Invalidate:
                await svc.InvalidateAsync(CampaignKey, Reason, token).ConfigureAwait(false);
                break;
            case ResetCampaignAction.ResetFilter:
                await svc.ResetFilterAsync(CampaignKey, FilterName, token).ConfigureAwait(false);
                break;
            case ResetCampaignAction.ResetAll:
            case ResetCampaignAction.ForceNew:
                await svc.ResetAllAsync(CampaignKey, filters, profileId, options, token).ConfigureAwait(false);
                break;
            case ResetCampaignAction.ExtendValidity:
                await svc.ExtendValidityAsync(CampaignKey, DateTime.UtcNow.AddDays(ExtendValidityDays), token).ConfigureAwait(false);
                break;
        }

        progress?.Report(new ApplicationStatus { Status = $"SFCM: {Action} done for {CampaignKey}" });
    }

    public override object Clone() => new ResetSkyFlatCampaignInstruction(this);

    public override string ToString()
        => $"Category: {Category}, Item: {nameof(ResetSkyFlatCampaignInstruction)}, Action={Action}";
}
