namespace SkyFlatCampaignManager.Core.Campaigns;

/// <summary>Pure helpers used by NINA conditions/UI to expose live campaign counters.</summary>
public static class CampaignMetrics
{
    public static int ConfiguredTarget(IEnumerable<FilterCampaignSettings> filters) =>
        filters.Where(f => f.Enabled && f.TargetCount > 0).Sum(f => f.TargetCount);

    /// <summary>
    /// Number of accepted flats still required. A missing, expired or invalidated campaign means
    /// a fresh campaign is required, so the full configured target is returned. An active
    /// incomplete campaign returns its persisted remaining count. A valid completed campaign is 0.
    /// </summary>
    public static int FlatsRemaining(CampaignRequirement requirement, IEnumerable<FilterCampaignSettings> filters)
    {
        var configuredTarget = ConfiguredTarget(filters);
        if (requirement.NoCampaign || requirement.IsExpired || requirement.IsInvalidated)
            return configuredTarget;

        if (requirement.Campaign is { } campaign)
            return Math.Max(0, campaign.TotalRemaining);

        return requirement.IsRequired ? configuredTarget : 0;
    }
}
