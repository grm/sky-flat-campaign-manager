using NINA.Core.Model.Equipment;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem.FilterWheel;
using SkyFlatCampaignManager.Core.Equipment;

namespace NINA.Plugin.SkyFlatCampaignManager.Adapters;

public sealed class NinaFilterWheelService : IFilterWheelService
{
    private readonly IProfileService _profileService;
    private readonly IFilterWheelMediator _filterWheelMediator;

    public NinaFilterWheelService(IProfileService profileService, IFilterWheelMediator filterWheelMediator)
    {
        _profileService = profileService;
        _filterWheelMediator = filterWheelMediator;
    }

    public bool IsConnected => _filterWheelMediator.GetInfo()?.Connected == true;

    public IReadOnlyList<string> FilterNames
        => _profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters
            .Select(f => f.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList()!;

    public string? CurrentFilterName
    {
        get
        {
            var info = _filterWheelMediator.GetInfo();
            if (info?.Connected != true) return null;
            var selected = info.SelectedFilter;
            return selected?.Name;
        }
    }

    public async Task ChangeFilterAsync(string filterName, CancellationToken cancellationToken = default)
    {
        var filter = _profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters
            .FirstOrDefault(f => string.Equals(f.Name, filterName, StringComparison.OrdinalIgnoreCase));
        if (filter is null)
        {
            throw new InvalidOperationException($"Filter '{filterName}' not found in profile.");
        }

        var switchFilter = new SwitchFilter(_profileService, _filterWheelMediator) { Filter = filter };
        await switchFilter.Execute(null, cancellationToken).ConfigureAwait(false);
    }
}
