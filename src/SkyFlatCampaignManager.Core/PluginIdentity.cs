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
    public const string Version = "0.0.5";
    public const int CurrentSchemaVersion = 1;
    public const int DefaultCampaignValidityDays = 60;

    /// <summary>Legacy raw-ADU target, retained only for backward-compatible migration of pre-fraction settings.</summary>
    public const double DefaultTargetAdu = 25000d;

    /// <summary>Legacy raw-ADU tolerance (fixed ADU count), retained only for backward-compatible migration.</summary>
    public const double DefaultAduTolerance = 2500d;

    /// <summary>Full-scale ADU assumed only when migrating pre-existing TargetAdu/AduTolerance settings that predate normalized histogram fractions. Never assumed for live image validation — see <see cref="Acquisition.ImageStatisticsResult.MaxAdu"/>.</summary>
    public const double LegacyMigrationMaxAdu = 65535d;

    /// <summary>Normalized target histogram level (0.0–1.0 of full scale). Default preserves the legacy 25000/65535 behaviour (~38.15%).</summary>
    public static readonly double DefaultTargetHistogramFraction = DefaultTargetAdu / LegacyMigrationMaxAdu;

    /// <summary>NINA-style tolerance expressed as a fraction OF THE TARGET (not of full scale), e.g. 0.10 = ±10% of target. Default preserves the legacy 2500/25000 behaviour (10%).</summary>
    public const double DefaultTargetToleranceFraction = DefaultAduTolerance / DefaultTargetAdu;

    public const double DefaultMinExposureSeconds = 0.001d;
    public const double DefaultMaxExposureSeconds = 30d;
    public const double DefaultRoiFraction = 0.7d;
    public const double DefaultMaxSaturationFraction = 0.01d;
    public const double DefaultSunSafetySeparationDegrees = 30d;
    public const int DefaultMaxRejectedAttemptsPerFilter = 8;
}
