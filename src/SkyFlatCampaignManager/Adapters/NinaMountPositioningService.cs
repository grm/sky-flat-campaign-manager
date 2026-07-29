using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Equipment;
using SkyFlatCampaignManager.Core.Errors;

namespace NINA.Plugin.SkyFlatCampaignManager.Adapters;

/// <summary>
/// Mount pointing adapter. Uses verified ITelescopeMediator APIs only.
/// Never bypasses NINA safety; refuses unsafe proximity to the Sun.
/// </summary>
public sealed class NinaMountPositioningService : IMountPositioningService
{
    private readonly IProfileService _profileService;
    private readonly ITelescopeMediator _telescopeMediator;
    private readonly Action<string>? _log;
    private Coordinates? _initial;
    private bool _restoreRequested;

    public NinaMountPositioningService(IProfileService profileService, ITelescopeMediator telescopeMediator, Action<string>? log = null)
    {
        _profileService = profileService;
        _telescopeMediator = telescopeMediator;
        _log = log;
    }

    public bool IsConnected => _telescopeMediator.GetInfo()?.Connected == true;

    public async Task EnsureSafePointingAsync(MountPointingRequest request, CancellationToken cancellationToken = default)
    {
        _restoreRequested = request.RestoreAtEnd;

        if (!IsConnected)
        {
            return;
        }

        var info = _telescopeMediator.GetInfo();
        _initial = info.Coordinates;

        if (request.Tracking == TrackingMode.DisableTracking)
        {
            _ = _telescopeMediator.SetTrackingEnabled(false);
        }

        if (request.Mode == MountPointingMode.KeepCurrent)
        {
            return;
        }

        var astro = _profileService.ActiveProfile.AstrometrySettings;
        var observer = new ObserverInfo
        {
            Latitude = astro.Latitude,
            Longitude = astro.Longitude,
            Elevation = astro.Elevation
        };

        var sunAltitudeDegrees = AstroUtil.GetSunAltitude(DateTime.Now, observer);

        double targetAlt;
        double targetAz;
        switch (request.Mode)
        {
            case MountPointingMode.Zenith:
                targetAlt = 89;
                targetAz = 0;
                break;
            case MountPointingMode.AltAz:
                targetAlt = request.AltitudeDegrees ?? 80;
                targetAz = request.AzimuthDegrees ?? 180;
                break;
            case MountPointingMode.OffsetFromSun:
                // Point high in the opposite cardinal from the geometric sun altitude trend:
                // evening (sun descending / west-ish) -> east; morning -> west. Conservative fixed az.
                targetAlt = Math.Clamp(Math.Abs(request.SunOffsetDegrees ?? 40), 20, 85);
                targetAz = sunAltitudeDegrees < 0 ? 90 : 270;
                break;
            default:
                return;
        }

        // Conservative separation proxy: do not point while the Sun is above the safety floor
        // and the target altitude is within MinSunSeparationDegrees of the solar altitude.
        var approxSeparation = Math.Abs(targetAlt - sunAltitudeDegrees);
        if (sunAltitudeDegrees > -5 && approxSeparation < request.MinSunSeparationDegrees)
        {
            throw new PluginError(
                $"Refusing to point within {request.MinSunSeparationDegrees}° of the Sun (approx sep {approxSeparation:F1}°, sunAlt {sunAltitudeDegrees:F1}°).",
                ErrorCategory.Safety);
        }

        _log?.Invoke($"Slewing to Alt={targetAlt:F1} Az={targetAz:F1}");
        var coords = new TopocentricCoordinates(
            Angle.ByDegree(targetAz),
            Angle.ByDegree(targetAlt),
            Angle.ByDegree(observer.Latitude),
            Angle.ByDegree(observer.Longitude),
            observer.Elevation);

        var ok = await _telescopeMediator.SlewToTopocentricCoordinates(coords, cancellationToken).ConfigureAwait(false);
        if (!ok)
        {
            throw new PluginError("Mount slew failed or was rejected.", ErrorCategory.Safety);
        }
    }

    public async Task RestoreIfRequestedAsync(CancellationToken cancellationToken = default)
    {
        if (!_restoreRequested || _initial is null || !IsConnected)
        {
            return;
        }

        try
        {
            _ = await _telescopeMediator.SlewToCoordinatesAsync(_initial, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warning($"[{PluginIdentity.ShortName}] Restore pointing failed: {ex.Message}");
        }
    }
}
