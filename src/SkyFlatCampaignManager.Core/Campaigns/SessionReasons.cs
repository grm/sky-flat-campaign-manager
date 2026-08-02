namespace SkyFlatCampaignManager.Core.Campaigns;

/// <summary>
/// Stable string identifiers reported while a session is waiting. These are surfaced in
/// <see cref="SkyFlatSessionProgress.WaitReason"/>, logs, and tests, so treat the values as
/// part of the plugin's effective contract.
/// </summary>
public static class SessionWaitReasons
{
    /// <summary>Sun is on the "not yet open" side of the window; waiting may make it open.</summary>
    public const string AstronomicalWindowNotOpenYet = "AstronomicalWindowNotOpenYet";

    /// <summary>No currently-incomplete filter is exposure-feasible yet, but at least one could become feasible.</summary>
    public const string FilterNotFeasible = "FilterNotFeasible";
}

/// <summary>
/// Stable string identifiers reported when a session stops. Surfaced in
/// <see cref="SkyFlatSessionProgress.StopReason"/> / <see cref="SkyFlatSessionResult.StopReason"/>.
/// </summary>
public static class SessionStopReasons
{
    public const string MaxDuration = "MaxDuration";
    public const string Completed = "Completed";
    public const string MinimumReached = "MinimumReached";
    public const string NoFilters = "NoFilters";
    public const string Cancelled = "Cancelled";
    public const string Faulted = "Faulted";

    /// <summary>Sun hasn't reached the window yet and waiting is disabled (AllowWaitForSky=false).</summary>
    public const string AstronomicalWindowNotOpenYet = "AstronomicalWindowNotOpenYet";

    /// <summary>Waited for the window to open, but MaxWaitMinutes elapsed for this wait reason first.</summary>
    public const string AstronomicalWindowWaitTimeout = "AstronomicalWindowWaitTimeout";

    /// <summary>Generic: window has passed for this session and cannot reopen. Prefer the directional reasons below when mode is known.</summary>
    public const string AstronomicalWindowClosed = "AstronomicalWindowClosed";

    /// <summary>Evening: sun altitude fell below MinSunAltitudeDegrees. Sky is too dark for flats; waiting will not help — this is a normal closed twilight window, not a fault.</summary>
    public const string EveningSkyTooDark = "EveningSkyTooDark";

    /// <summary>Morning: sun altitude rose above MaxSunAltitudeDegrees. Sky is too bright for flats; waiting will not help — this is a normal closed twilight window, not a fault.</summary>
    public const string MorningSkyTooBright = "MorningSkyTooBright";

    /// <summary>No incomplete filter is currently exposure-feasible and none can become feasible by waiting (given the current mode/direction).</summary>
    public const string NoFilterFeasible = "NoFilterFeasible";

    /// <summary>Waited for a filter to become feasible, but MaxWaitMinutes elapsed for this wait reason first.</summary>
    public const string NoFilterFeasibleWaitTimeout = "NoFilterFeasibleWaitTimeout";
}
