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
    public const string Version = "0.0.8";
    public const int CurrentSchemaVersion = 1;
    public const int DefaultCampaignValidityDays = 60;

    /// <summary>Legacy raw-ADU target, retained only for backward-compatible migration of pre-fraction settings.</summary>
    public const double DefaultTargetAdu = 25000d;

    /// <summary>Legacy raw-ADU tolerance (fixed ADU count), retained only for backward-compatible migration.</summary>
    public const double DefaultAduTolerance = 2500d;

    /// <summary>Full-scale ADU assumed only when migrating pre-existing TargetAdu/AduTolerance settings that predate normalized histogram fractions. Never assumed for live image validation.</summary>
    public const double LegacyMigrationMaxAdu = 65535d;

    /// <summary>Normalized target histogram level. Mid-scale is a robust default for modern CMOS flats and leaves ample headroom.</summary>
    public const double DefaultTargetHistogramFraction = 0.50d;

    /// <summary>NINA-style tolerance expressed as a fraction OF THE TARGET (not of full scale).</summary>
    public const double DefaultTargetToleranceFraction = 0.10d;

    /// <summary>Conservative default: avoid ultra-short flats where shutter/driver timing and illumination stability can dominate.</summary>
    public const double DefaultMinExposureSeconds = 0.5d;
    public const double DefaultMaxExposureSeconds = 30d;
    public const double DefaultRoiFraction = 0.7d;
    public const double DefaultMaxSaturationFraction = 0.01d;
    public const double DefaultSunSafetySeparationDegrees = 30d;
    public const int DefaultMaxRejectedAttemptsPerFilter = 8;
}
