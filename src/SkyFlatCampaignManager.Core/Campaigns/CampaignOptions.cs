namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class CampaignOptions
{
    public bool PluginEnabled { get; set; } = true;
    public string CampaignName { get; set; } = "Default";
    public string StateDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public int ValidityDays { get; set; } = PluginIdentity.DefaultCampaignValidityDays;
    public bool AutoStartExpiredCampaign { get; set; } = true;
    public bool DetailedLogging { get; set; } = true;
    public bool DryRun { get; set; }
    public bool SimulationMode { get; set; }
    public double RoiFraction { get; set; } = PluginIdentity.DefaultRoiFraction;
    public double MaxSaturationFraction { get; set; } = PluginIdentity.DefaultMaxSaturationFraction;
    public int MaxRejectedAttemptsPerFilter { get; set; } = PluginIdentity.DefaultMaxRejectedAttemptsPerFilter;
    public double SunSafetySeparationDegrees { get; set; } = PluginIdentity.DefaultSunSafetySeparationDegrees;
    public bool InvalidateOnFingerprintChange { get; set; }
    public bool WarnOnFingerprintChange { get; set; } = true;
    public SkyBrightnessSourceMode BrightnessSource { get; set; } = SkyBrightnessSourceMode.Camera;
    public double SqmMaxAgeSeconds { get; set; } = 120;
    public List<FilterCampaignSettings> Filters { get; set; } = new();

    public AstronomicalWindowOptions EveningWindow { get; set; } = AstronomicalWindowOptions.CreateEveningDefaults();
    public AstronomicalWindowOptions MorningWindow { get; set; } = AstronomicalWindowOptions.CreateMorningDefaults();
}

public sealed class AstronomicalWindowOptions
{
    public double MinSunAltitudeDegrees { get; set; } = -12;
    public double MaxSunAltitudeDegrees { get; set; } = 0;
    public double MaxDurationMinutes { get; set; } = 90;
    public double MaxWaitMinutes { get; set; } = 45;
    public double SafetyMarginMinutes { get; set; } = 5;
    public TimeSpan? EarliestLocalTime { get; set; }
    public TimeSpan? LatestLocalTime { get; set; }

    public static AstronomicalWindowOptions CreateEveningDefaults() => new()
    {
        MinSunAltitudeDegrees = -15,
        MaxSunAltitudeDegrees = 0,
        MaxDurationMinutes = 90,
        MaxWaitMinutes = 60
    };

    public static AstronomicalWindowOptions CreateMorningDefaults() => new()
    {
        MinSunAltitudeDegrees = -15,
        MaxSunAltitudeDegrees = 0,
        MaxDurationMinutes = 90,
        MaxWaitMinutes = 60
    };
}
