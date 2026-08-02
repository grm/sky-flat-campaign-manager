using SkyFlatCampaignManager.Core.Campaigns;

namespace SkyFlatCampaignManager.Core.Acquisition;

/// <summary>
/// Maps an <see cref="ExposureFeasibility"/> classification plus the session's Morning/Evening
/// direction to "would waiting for the sky to change plausibly help?". This is deliberately
/// separate from <see cref="ExposureFeasibility"/> itself, which only knows the exposure is out
/// of range — not which way the sky is trending.
///
/// EVENING (sky darkening): required exposure below minimum means the sky is still too bright —
/// waiting (it gets darker) moves the required exposure up toward the range, so it may help.
/// Required exposure above maximum means the sky is already too dark for this filter — it will
/// only get darker, so waiting cannot help.
///
/// MORNING (sky brightening): required exposure above maximum means the sky is still too dark —
/// waiting (it gets brighter) moves the required exposure down toward the range, so it may help.
/// Required exposure below minimum means the sky is already too bright for this filter — it will
/// only get brighter, so waiting cannot help.
/// </summary>
public static class ExposureFeasibilityRules
{
    public static bool CanImproveByWaiting(CampaignMode mode, ExposureFeasibility feasibility)
    {
        if (feasibility == ExposureFeasibility.Feasible)
        {
            return false;
        }

        return mode switch
        {
            CampaignMode.Evening => feasibility == ExposureFeasibility.TooShort,
            CampaignMode.Morning => feasibility == ExposureFeasibility.TooLong,
            _ => false
        };
    }

    /// <summary>Human-readable explanation matching the direction-aware rule above, for logs/status text.</summary>
    public static string Describe(CampaignMode mode, ExposureFeasibility feasibility) => (mode, feasibility) switch
    {
        (CampaignMode.Evening, ExposureFeasibility.TooShort) => "Sky is still too bright for this filter; waiting may help.",
        (CampaignMode.Evening, ExposureFeasibility.TooLong) => "Sky is already too dark for this filter; waiting will not help.",
        (CampaignMode.Morning, ExposureFeasibility.TooLong) => "Sky is still too dark for this filter; waiting may help.",
        (CampaignMode.Morning, ExposureFeasibility.TooShort) => "Sky is already too bright for this filter; waiting will not help.",
        _ => "Exposure is within range for this filter."
    };
}
