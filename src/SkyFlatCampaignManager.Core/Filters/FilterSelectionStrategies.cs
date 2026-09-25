using SkyFlatCampaignManager.Core.Campaigns;

namespace SkyFlatCampaignManager.Core.Filters;

public sealed class ManualOrderFilterSelectionStrategy : IFilterSelectionStrategy
{
    public string Name => "Manual";
    public FilterCampaignSettings? SelectNext(IReadOnlyList<FilterCampaignSettings> candidates, CampaignState campaign, CampaignMode mode, FilterSelectionContext context)
        => Incomplete(candidates, campaign)
            .OrderBy(f => mode == CampaignMode.Morning ? f.ManualMorningOrder : f.ManualEveningOrder)
            .ThenBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

    internal static IEnumerable<FilterCampaignSettings> Incomplete(IReadOnlyList<FilterCampaignSettings> candidates, CampaignState campaign)
        => candidates.Where(c => c.Enabled && campaign.Filters.TryGetValue(c.FilterName, out var p) && p.IsIncomplete);
}

public sealed class HighestExposureFirstStrategy : IFilterSelectionStrategy
{
    public string Name => "HighestExposureFirst";
    public FilterCampaignSettings? SelectNext(IReadOnlyList<FilterCampaignSettings> candidates, CampaignState campaign, CampaignMode mode, FilterSelectionContext context)
        => ManualOrderFilterSelectionStrategy.Incomplete(candidates, campaign)
            .OrderByDescending(f => Estimate(f, campaign, context)).ThenBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

    private static double Estimate(FilterCampaignSettings f, CampaignState campaign, FilterSelectionContext context)
    {
        if (context.EstimatedExposureSecondsByFilter?.TryGetValue(f.FilterName, out var e) == true) return e;
        if (campaign.Filters.TryGetValue(f.FilterName, out var p) && p.LastExposureSeconds is { } last) return last;
        return f.MaxExposureSeconds;
    }
}

public sealed class LowestExposureFirstStrategy : IFilterSelectionStrategy
{
    public string Name => "LowestExposureFirst";
    public FilterCampaignSettings? SelectNext(IReadOnlyList<FilterCampaignSettings> candidates, CampaignState campaign, CampaignMode mode, FilterSelectionContext context)
        => ManualOrderFilterSelectionStrategy.Incomplete(candidates, campaign)
            .OrderBy(f => Estimate(f, campaign, context)).ThenBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

    private static double Estimate(FilterCampaignSettings f, CampaignState campaign, FilterSelectionContext context)
    {
        if (context.EstimatedExposureSecondsByFilter?.TryGetValue(f.FilterName, out var e) == true) return e;
        if (campaign.Filters.TryGetValue(f.FilterName, out var p) && p.LastExposureSeconds is { } last) return last;
        return f.MinExposureSeconds;
    }
}

public sealed class UserPriorityFilterSelectionStrategy : IFilterSelectionStrategy
{
    public string Name => "UserPriority";
    public FilterCampaignSettings? SelectNext(IReadOnlyList<FilterCampaignSettings> candidates, CampaignState campaign, CampaignMode mode, FilterSelectionContext context)
        => ManualOrderFilterSelectionStrategy.Incomplete(candidates, campaign).OrderBy(f => f.Priority)
            .ThenBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
}

public sealed class RecommendedMorningEveningStrategy : IFilterSelectionStrategy
{
    private readonly HighestExposureFirstStrategy _high = new();
    private readonly LowestExposureFirstStrategy _low = new();
    public string Name => "RecommendedMorningEvening";
    public FilterCampaignSettings? SelectNext(IReadOnlyList<FilterCampaignSettings> candidates, CampaignState campaign, CampaignMode mode, FilterSelectionContext context)
        => mode == CampaignMode.Morning ? _low.SelectNext(candidates, campaign, mode, context) : _high.SelectNext(candidates, campaign, mode, context);
}

/// <summary>
/// Twilight-aware strategy. Instead of blindly staying on the current filter, select the filter
/// closest to becoming impossible as the sky moves in its known direction. In the morning the
/// danger boundary is MinExposure (sky brightening, exposures shrink); in the evening it is
/// MaxExposure (sky darkening, exposures grow). This naturally produces broadband-first in the
/// morning and narrowband-first in the evening while respecting each filter's configured limits.
/// </summary>
public sealed class AdaptiveFilterSelectionStrategy : IFilterSelectionStrategy
{
    private readonly RecommendedMorningEveningStrategy _fallback = new();
    public string Name => "Adaptive";

    public FilterCampaignSettings? SelectNext(IReadOnlyList<FilterCampaignSettings> candidates, CampaignState campaign, CampaignMode mode, FilterSelectionContext context)
    {
        var incomplete = ManualOrderFilterSelectionStrategy.Incomplete(candidates, campaign).ToList();
        if (incomplete.Count == 0) return null;
        if (context.EstimatedExposureSecondsByFilter is null || context.EstimatedExposureSecondsByFilter.Count == 0)
            return _fallback.SelectNext(candidates, campaign, mode, context);

        double Urgency(FilterCampaignSettings f)
        {
            if (!context.EstimatedExposureSecondsByFilter.TryGetValue(f.FilterName, out var exposure) || exposure <= 0)
                return double.PositiveInfinity;

            return mode == CampaignMode.Morning
                ? exposure / Math.Max(f.MinExposureSeconds, 1e-6)       // 1.0 = about to become too short
                : Math.Max(f.MaxExposureSeconds, 1e-6) / exposure;     // 1.0 = about to become too long
        }

        return incomplete.OrderBy(Urgency)
            .ThenBy(f => mode == CampaignMode.Morning
                ? context.EstimatedExposureSecondsByFilter.TryGetValue(f.FilterName, out var e) ? e : double.MaxValue
                : -(context.EstimatedExposureSecondsByFilter.TryGetValue(f.FilterName, out var e) ? e : 0d))
            .ThenBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}

public sealed class ClosestToOptimalWindowStrategy : IFilterSelectionStrategy
{
    public string Name => "ClosestToOptimalWindow";
    public FilterCampaignSettings? SelectNext(IReadOnlyList<FilterCampaignSettings> candidates, CampaignState campaign, CampaignMode mode, FilterSelectionContext context)
    {
        var incomplete = ManualOrderFilterSelectionStrategy.Incomplete(candidates, campaign).ToList();
        if (incomplete.Count == 0) return null;
        return incomplete.OrderBy(f =>
        {
            var lastAlt = campaign.Filters.TryGetValue(f.FilterName, out var p) ? p.LastSunAltitudeDegrees : null;
            if (lastAlt is null || context.CurrentSunAltitudeDegrees is null) return 999d;
            return Math.Abs(lastAlt.Value - context.CurrentSunAltitudeDegrees.Value);
        }).ThenBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }
}

public static class FilterSelectionStrategyFactory
{
    public static IFilterSelectionStrategy Create(FilterOrderStrategyKind kind) => kind switch
    {
        FilterOrderStrategyKind.Manual => new ManualOrderFilterSelectionStrategy(),
        FilterOrderStrategyKind.HighestExposureFirst => new HighestExposureFirstStrategy(),
        FilterOrderStrategyKind.LowestExposureFirst => new LowestExposureFirstStrategy(),
        FilterOrderStrategyKind.UserPriority => new UserPriorityFilterSelectionStrategy(),
        FilterOrderStrategyKind.RecommendedMorningEvening => new RecommendedMorningEveningStrategy(),
        FilterOrderStrategyKind.ClosestToOptimalWindow => new ClosestToOptimalWindowStrategy(),
        _ => new AdaptiveFilterSelectionStrategy()
    };
}
