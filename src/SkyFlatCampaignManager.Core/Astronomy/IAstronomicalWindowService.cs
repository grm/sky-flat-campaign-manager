using SkyFlatCampaignManager.Core.Campaigns;

namespace SkyFlatCampaignManager.Core.Astronomy;

/// <summary>
/// Direction-aware classification of the sun altitude relative to a configured
/// evening/morning safety window. Unlike a simple "in range" boolean, this distinguishes
/// a window that has <b>not opened yet</b> (waiting may help) from one that has
/// <b>already closed</b> (waiting can never help — the sun keeps moving away from the window).
/// </summary>
public enum AstronomicalWindowState
{
    /// <summary>The sun has not yet reached the window for this mode. Waiting can make it open.</summary>
    TooEarly,

    /// <summary>The sun altitude is within [MinSunAltitudeDegrees, MaxSunAltitudeDegrees]. Flats may run.</summary>
    Open,

    /// <summary>The sun has already passed through and out of the window for this mode. Waiting will never re-open it this session.</summary>
    TooLate
}

public interface IAstronomicalWindowService
{
    /// <summary>
    /// Direction-unaware check retained for callers (e.g. sequencer conditions) that only need
    /// to know "is the window open right now" and don't need TooEarly/TooLate semantics.
    /// Equivalent to <c>Evaluate(mode, ..) == AstronomicalWindowState.Open</c> for either mode,
    /// since the Open range itself does not depend on direction.
    /// </summary>
    bool IsWithinSafetyWindow(double sunAltitudeDegrees, AstronomicalWindowOptions window);

    /// <summary>
    /// Direction-aware evaluation of where the sun altitude sits relative to the window.
    /// </summary>
    /// <param name="mode">Must be <see cref="CampaignMode.Evening"/> or <see cref="CampaignMode.Morning"/> — resolve
    /// <see cref="CampaignMode.Automatic"/> via <see cref="ResolveMode"/> first.</param>
    AstronomicalWindowState Evaluate(CampaignMode mode, double sunAltitudeDegrees, AstronomicalWindowOptions window);

    CampaignMode ResolveMode(CampaignMode requested, double sunAltitudeDegrees, double previousSunAltitudeDegrees);
    bool IsSunTooClose(double separationDegrees, double minimumSafeSeparationDegrees);
}

public sealed class AstronomicalWindowService : IAstronomicalWindowService
{
    public bool IsWithinSafetyWindow(double sunAltitudeDegrees, AstronomicalWindowOptions window)
        => sunAltitudeDegrees >= window.MinSunAltitudeDegrees
           && sunAltitudeDegrees <= window.MaxSunAltitudeDegrees;

    public AstronomicalWindowState Evaluate(CampaignMode mode, double sunAltitudeDegrees, AstronomicalWindowOptions window)
    {
        if (mode == CampaignMode.Automatic)
        {
            throw new ArgumentException(
                "Mode must be resolved to Morning or Evening before evaluating the window. Call ResolveMode first.",
                nameof(mode));
        }

        if (sunAltitudeDegrees >= window.MinSunAltitudeDegrees && sunAltitudeDegrees <= window.MaxSunAltitudeDegrees)
        {
            return AstronomicalWindowState.Open;
        }

        if (mode == CampaignMode.Evening)
        {
            // Evening: sun starts high (bright) and falls. Above max = not dark enough yet (TooEarly).
            // Below min = already too dark (TooLate) — it will only get darker, so waiting cannot help.
            return sunAltitudeDegrees > window.MaxSunAltitudeDegrees
                ? AstronomicalWindowState.TooEarly
                : AstronomicalWindowState.TooLate;
        }

        // Morning: sun starts low (dark) and rises. Below min = not bright enough yet (TooEarly).
        // Above max = already too bright (TooLate) — it will only get brighter, so waiting cannot help.
        return sunAltitudeDegrees < window.MinSunAltitudeDegrees
            ? AstronomicalWindowState.TooEarly
            : AstronomicalWindowState.TooLate;
    }

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
