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

/// <summary>
/// Solar-altitude safety window for one mode (evening or morning). This is the single source of
/// truth for <b>where</b> the window is (min/max sun altitude). "How long to wait" and "how long
/// the whole session may run" are session-level, not per-window, concerns — see
/// <see cref="SkyFlatSessionRequest.MaxWaitMinutes"/> and <see cref="SkyFlatSessionRequest.MaxDurationMinutes"/>.
/// A previous revision duplicated <c>MaxDurationMinutes</c>/<c>MaxWaitMinutes</c> here and also
/// exposed <c>SafetyMarginMinutes</c>/<c>EarliestLocalTime</c>/<c>LatestLocalTime</c>, none of
/// which had any runtime effect; they were removed rather than left as dead configuration.
/// </summary>
public sealed class AstronomicalWindowOptions
{
    public double MinSunAltitudeDegrees { get; set; } = -12;
    public double MaxSunAltitudeDegrees { get; set; } = 0;

    public static AstronomicalWindowOptions CreateEveningDefaults() => new()
    {
        MinSunAltitudeDegrees = -15,
        MaxSunAltitudeDegrees = 0
    };

    public static AstronomicalWindowOptions CreateMorningDefaults() => new()
    {
        MinSunAltitudeDegrees = -15,
        MaxSunAltitudeDegrees = 0
    };
}
