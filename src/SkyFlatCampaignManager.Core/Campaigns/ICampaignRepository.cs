namespace SkyFlatCampaignManager.Core.Campaigns;

public interface ICampaignRepository
{
    Task<CampaignState?> LoadAsync(string campaignKey, CancellationToken cancellationToken = default);
    Task SaveAsync(string campaignKey, CampaignState state, CancellationToken cancellationToken = default);
    Task DeleteAsync(string campaignKey, CancellationToken cancellationToken = default);
}
