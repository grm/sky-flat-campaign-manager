using SkyFlatCampaignManager.Core.Campaigns;

namespace SkyFlatCampaignManager.Core.Filters;

public interface IFilterSelectionStrategy
{
    string Name { get; }
    FilterCampaignSettings? SelectNext(
        IReadOnlyList<FilterCampaignSettings> candidates,
        CampaignState campaign,
        CampaignMode mode,
        FilterSelectionContext context);
}

public sealed class FilterSelectionContext
{
    public double? CurrentSunAltitudeDegrees { get; init; }
    public double? LastMeasuredAdu { get; init; }
    public string? CurrentFilterName { get; init; }
    public IReadOnlyDictionary<string, double>? EstimatedExposureSecondsByFilter { get; init; }
}
