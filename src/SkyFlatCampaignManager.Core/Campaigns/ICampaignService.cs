namespace SkyFlatCampaignManager.Core.Campaigns;

public interface ICampaignService
{
    Task<CampaignState> GetOrCreateAsync(string campaignKey, string profileId, IEnumerable<FilterCampaignSettings> filters, CampaignOptions options, CancellationToken ct = default);
    Task<CampaignRequirement> EvaluateRequirementAsync(string campaignKey, CampaignOptions options, CancellationToken ct = default);

    /// <summary>
    /// Atomically records an accepted flat: count, exposure, measured level (ADU and normalized
    /// fraction), and the sun altitude at capture time, in a single persisted save. The sun
    /// altitude must be recorded here (not assigned to the in-memory object after this call
    /// returns) so it survives a plugin/NINA restart — see <see cref="FilterProgress.LastSunAltitudeDegrees"/>.
    /// </summary>
    Task<CampaignState> AcceptFlatAsync(
        string campaignKey,
        string filterName,
        double exposureSeconds,
        double measuredAdu,
        double? measuredHistogramFraction = null,
        double? sunAltitudeDegrees = null,
        CancellationToken ct = default);

    Task RejectFlatAsync(string campaignKey, string filterName, string reason, CancellationToken ct = default);
    Task<CampaignState> MarkCompletedAsync(string campaignKey, CampaignOptions options, CancellationToken ct = default);
    Task<CampaignState> InvalidateAsync(string campaignKey, string reason, CancellationToken ct = default);
    Task<CampaignState> ResetFilterAsync(string campaignKey, string filterName, CancellationToken ct = default);
    Task<CampaignState> ResetAllAsync(string campaignKey, IEnumerable<FilterCampaignSettings> filters, string profileId, CampaignOptions options, CancellationToken ct = default);
    Task ExtendValidityAsync(string campaignKey, DateTime validUntilUtc, CancellationToken ct = default);
}

public sealed class CampaignRequirement
{
    public bool IsRequired { get; init; }
    public bool IsIncomplete { get; init; }
    public bool IsExpired { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsInvalidated { get; init; }
    public bool NoCampaign { get; init; }
    public string Reason { get; init; } = string.Empty;
    public CampaignState? Campaign { get; init; }
}
