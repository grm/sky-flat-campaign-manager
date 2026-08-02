using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Plugin.SkyFlatCampaignManager.Properties;
using NINA.Plugin.SkyFlatCampaignManager.Services;
using NINA.Plugin.SkyFlatCampaignManager.ViewModels;
using SkyFlatCampaignManager.Core;

namespace NINA.Plugin.SkyFlatCampaignManager;

/// <summary>
/// Plugin manifest export. Options DataTemplate key must be "{AssemblyTitle}_Options".
/// </summary>
[Export(typeof(IPluginManifest))]
public class SkyFlatCampaignManagerPlugin : PluginBase, INotifyPropertyChanged
{
    private readonly IProfileService _profileService;
    private bool _suppressFilterSave;
    private string _filterConfigStatus = string.Empty;

    [ImportingConstructor]
    public SkyFlatCampaignManagerPlugin(IProfileService profileService)
    {
        _profileService = profileService;
        if (Settings.Default.UpdateSettings)
        {
            Settings.Default.Upgrade();
            Settings.Default.UpdateSettings = false;
            CoreUtil.SaveSettings(Settings.Default);
        }

        ReloadFilterConfigCommand = new PluginRelayCommand(ReloadFilterConfig);
        SaveFilterConfigCommand = new PluginRelayCommand(SaveFilterConfig);
        ApplyDefaultsToFiltersCommand = new PluginRelayCommand(ApplyDefaultsToFilters);

        _profileService.ProfileChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(ProfileId));
            ReloadFilterConfig();
        };

        ReloadFilterConfig();
    }

    public string ProfileId => _profileService.ActiveProfile?.Id.ToString() ?? string.Empty;

    public ObservableCollection<FilterConfigRow> FilterConfigs { get; } = new();

    public ICommand ReloadFilterConfigCommand { get; }
    public ICommand SaveFilterConfigCommand { get; }
    public ICommand ApplyDefaultsToFiltersCommand { get; }

    public string FilterConfigStatus
    {
        get => _filterConfigStatus;
        private set { _filterConfigStatus = value; RaisePropertyChanged(); }
    }

    public bool PluginEnabled
    {
        get => Settings.Default.PluginEnabled;
        set { Settings.Default.PluginEnabled = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public int ValidityDays
    {
        get => Settings.Default.ValidityDays;
        set { Settings.Default.ValidityDays = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public bool AutoStartExpiredCampaign
    {
        get => Settings.Default.AutoStartExpiredCampaign;
        set { Settings.Default.AutoStartExpiredCampaign = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public string CampaignName
    {
        get => Settings.Default.CampaignName;
        set { Settings.Default.CampaignName = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public string StateDirectory
    {
        get => Settings.Default.StateDirectory ?? string.Empty;
        set
        {
            Settings.Default.StateDirectory = value ?? string.Empty;
            CoreUtil.SaveSettings(Settings.Default);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(EffectiveStateDirectory));
            ReloadFilterConfig();
        }
    }

    /// <summary>Resolved campaign state folder (default when StateDirectory is blank).</summary>
    public string EffectiveStateDirectory => PluginServiceFactory.ResolveStateDirectory();

    public bool DetailedLogging
    {
        get => Settings.Default.DetailedLogging;
        set { Settings.Default.DetailedLogging = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public bool DryRun
    {
        get => Settings.Default.DryRun;
        set { Settings.Default.DryRun = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public int DefaultTargetCount
    {
        get => Settings.Default.DefaultTargetCount;
        set { Settings.Default.DefaultTargetCount = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    /// <summary>Default target histogram level, 0-100% of full scale ("Target histogram level" in the UI).</summary>
    public double DefaultTargetHistogramPercent
    {
        get => Settings.Default.DefaultTargetHistogramPercent;
        set { Settings.Default.DefaultTargetHistogramPercent = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    /// <summary>Default acceptance tolerance as a percentage OF THE TARGET (NINA-style), not of full scale.</summary>
    public double DefaultTargetTolerancePercent
    {
        get => Settings.Default.DefaultTargetTolerancePercent;
        set { Settings.Default.DefaultTargetTolerancePercent = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public double SunSafetySeparationDegrees
    {
        get => Settings.Default.SunSafetySeparationDegrees;
        set { Settings.Default.SunSafetySeparationDegrees = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public string DisplayName => PluginIdentity.DisplayName;
    public string PluginVersion => PluginIdentity.Version;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void ReloadFilterConfig()
    {
        _suppressFilterSave = true;
        try
        {
            FilterConfigs.Clear();
            foreach (var settings in PluginServiceFactory.CreateFilterSettings(_profileService))
            {
                var row = FilterConfigRow.FromSettings(settings);
                row.Changed += (_, _) => OnFilterRowChanged();
                FilterConfigs.Add(row);
            }

            FilterConfigStatus = FilterConfigs.Count == 0
                ? "No filters in the active NINA profile filter wheel."
                : $"Loaded {FilterConfigs.Count} filter(s) from profile + saved config.";
        }
        finally
        {
            _suppressFilterSave = false;
        }
    }

    private void OnFilterRowChanged()
    {
        if (_suppressFilterSave)
        {
            return;
        }

        SaveFilterConfig();
    }

    private void SaveFilterConfig()
    {
        var list = FilterConfigs.Select(r => r.ToSettings()).ToList();
        PluginServiceFactory.SaveFilterSettings(_profileService, list);
        FilterConfigStatus = $"Saved {list.Count} filter(s) → {EffectiveStateDirectory}";
    }

    private void ApplyDefaultsToFilters()
    {
        _suppressFilterSave = true;
        try
        {
            foreach (var row in FilterConfigs)
            {
                row.TargetCount = DefaultTargetCount;
                row.TargetHistogramPercent = DefaultTargetHistogramPercent;
                row.TargetTolerancePercent = DefaultTargetTolerancePercent;
                row.MinimumAcceptableCount = Math.Max(1, (int)(DefaultTargetCount * 0.6));
            }
        }
        finally
        {
            _suppressFilterSave = false;
        }

        SaveFilterConfig();
        FilterConfigStatus = "Applied global defaults to all filters (gain/offset unchanged).";
    }

    public override Task Teardown()
    {
        return base.Teardown();
    }
}
