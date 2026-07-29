using SkyFlatCampaignManager.Core.Campaigns;

namespace SkyFlatCampaignManager.Core.Astronomy;

public interface IAstronomicalWindowService
{
    bool IsWithinSafetyWindow(double sunAltitudeDegrees, AstronomicalWindowOptions window);
    CampaignMode ResolveMode(CampaignMode requested, double sunAltitudeDegrees, double previousSunAltitudeDegrees);
    bool IsSunTooClose(double separationDegrees, double minimumSafeSeparationDegrees);
}

public sealed class AstronomicalWindowService : IAstronomicalWindowService
{
    public bool IsWithinSafetyWindow(double sunAltitudeDegrees, AstronomicalWindowOptions window)
        => sunAltitudeDegrees >= window.MinSunAltitudeDegrees
           && sunAltitudeDegrees <= window.MaxSunAltitudeDegrees;

    public CampaignMode ResolveMode(CampaignMode requested, double sunAltitudeDegrees, double previousSunAltitudeDegrees)
    {
        if (requested != CampaignMode.Automatic)
        {
            return requested;
        }

        // Falling sun -> evening; rising sun -> morning.
        return sunAltitudeDegrees <= previousSunAltitudeDegrees ? CampaignMode.Evening : CampaignMode.Morning;
    }

    public bool IsSunTooClose(double separationDegrees, double minimumSafeSeparationDegrees)
        => separationDegrees < minimumSafeSeparationDegrees;
}

public interface ISunAltitudeProvider
{
    double GetSunAltitudeDegrees(DateTime utc);
}

/// <summary>
/// Simple analytical approximation for unit tests / simulation.
/// Production uses NINA AstroUtil adapter.
/// </summary>
public sealed class ApproximateSunAltitudeProvider : ISunAltitudeProvider
{
    private readonly double _latitudeDegrees;
    private readonly Func<DateTime, double> _override;

    public ApproximateSunAltitudeProvider(double latitudeDegrees = 45, Func<DateTime, double>? overrideCalc = null)
    {
        _latitudeDegrees = latitudeDegrees;
        _override = overrideCalc ?? Default;
    }

    public double GetSunAltitudeDegrees(DateTime utc) => _override(utc);

    private double Default(DateTime utc)
    {
        // Rough day-cycle sine for simulation only.
        var hour = utc.ToUniversalTime().TimeOfDay.TotalHours;
        var solarHourAngle = (hour - 12) * 15.0;
        var alt = Math.Sin((90 - Math.Abs(solarHourAngle)) * Math.PI / 180.0) * (90 - Math.Abs(_latitudeDegrees - 23.5));
        return Math.Clamp(alt - 30, -90, 90);
    }
}
