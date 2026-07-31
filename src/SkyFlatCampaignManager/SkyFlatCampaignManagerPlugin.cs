using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Plugin.SkyFlatCampaignManager.Properties;
using NINA.Plugin.SkyFlatCampaignManager.Services;
using SkyFlatCampaignManager.Core;

namespace NINA.Plugin.SkyFlatCampaignManager;

/// <summary>
/// Plugin manifest export. Options DataTemplate key must be "{AssemblyTitle}_Options".
/// </summary>
[Export(typeof(IPluginManifest))]
public class SkyFlatCampaignManagerPlugin : PluginBase, INotifyPropertyChanged
{
    private readonly IProfileService _profileService;

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

        _profileService.ProfileChanged += (_, _) => RaisePropertyChanged(nameof(ProfileId));
    }

    public string ProfileId => _profileService.ActiveProfile?.Id.ToString() ?? string.Empty;

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

    public double DefaultTargetAdu
    {
        get => Settings.Default.DefaultTargetAdu;
        set { Settings.Default.DefaultTargetAdu = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
    }

    public double DefaultAduTolerance
    {
        get => Settings.Default.DefaultAduTolerance;
        set { Settings.Default.DefaultAduTolerance = value; CoreUtil.SaveSettings(Settings.Default); RaisePropertyChanged(); }
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

    public override Task Teardown()
    {
        return base.Teardown();
    }
}
