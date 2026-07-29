namespace SkyFlatCampaignManager.Core.Campaigns;

/// <summary>Explicit session state machine for a sky-flat run.</summary>
public enum SessionState
{
    Idle,
    CheckingCampaign,
    CheckingEquipment,
    WaitingForAstronomicalWindow,
    MeasuringSky,
    SelectingFilter,
    ChangingFilter,
    EstimatingExposure,
    Capturing,
    Validating,
    Persisting,
    SwitchingFilter,
    Completed,
    StoppedByWindow,
    StoppedByTimeout,
    Cancelled,
    Faulted
}
