using NINA.Astrometry;
using NINA.Profile.Interfaces;
using SkyFlatCampaignManager.Core.Astronomy;

namespace NINA.Plugin.SkyFlatCampaignManager.Adapters;

/// <summary>
/// Adapter over NINA.Astrometry.AstroUtil.GetSunAltitude — do not invent alternate solar models.
/// </summary>
public sealed class NinaSunAltitudeProvider : ISunAltitudeProvider
{
    private readonly IProfileService _profileService;

    public NinaSunAltitudeProvider(IProfileService profileService) => _profileService = profileService;

    public double GetSunAltitudeDegrees(DateTime utc)
    {
        var astro = _profileService.ActiveProfile.AstrometrySettings;
        var observer = new ObserverInfo
        {
            Latitude = astro.Latitude,
            Longitude = astro.Longitude,
            Elevation = astro.Elevation
        };
        return AstroUtil.GetSunAltitude(utc.ToLocalTime(), observer);
    }
}
