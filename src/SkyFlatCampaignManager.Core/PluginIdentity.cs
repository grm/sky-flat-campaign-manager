namespace SkyFlatCampaignManager.Core;

/// <summary>
/// Centralized product naming. Change here to rename the plugin surface.
/// </summary>
public static class PluginIdentity
{
    public const string DisplayName = "Sky Flat Campaign Manager";
    public const string ShortName = "SFCM";
    public const string SequencerCategory = DisplayName;
    public const string AssemblyTitle = DisplayName;
    public const string NamespaceRoot = "NINA.Plugin.SkyFlatCampaignManager";
    public const string OptionsDataTemplateKey = "Sky Flat Campaign Manager_Options";
    public const string PluginGuid = "60fa0ecc-a71d-49a9-9890-274d3d5ff1d8";
    public const string Version = "0.0.2";
    public const int CurrentSchemaVersion = 1;
    public const int DefaultCampaignValidityDays = 60;
    public const double DefaultTargetAdu = 25000d;
    public const double DefaultAduTolerance = 2500d;
    public const double DefaultMinExposureSeconds = 0.001d;
    public const double DefaultMaxExposureSeconds = 30d;
    public const double DefaultRoiFraction = 0.7d;
    public const double DefaultMaxSaturationFraction = 0.01d;
    public const double DefaultSunSafetySeparationDegrees = 30d;
    public const int DefaultMaxRejectedAttemptsPerFilter = 8;
}
