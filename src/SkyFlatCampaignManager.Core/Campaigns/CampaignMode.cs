namespace SkyFlatCampaignManager.Core.Campaigns;

public enum CampaignMode
{
    Automatic = 0,
    Evening = 1,
    Morning = 2
}

public enum FilterOrderStrategyKind
{
    Adaptive = 0,
    Manual = 1,
    RecommendedMorningEvening = 2,
    HighestExposureFirst = 3,
    LowestExposureFirst = 4,
    ClosestToOptimalWindow = 5,
    UserPriority = 6
}

public enum MountPointingMode
{
    KeepCurrent = 0,
    Zenith = 1,
    AltAz = 2,
    OffsetFromSun = 3
}

public enum TrackingMode
{
    DisableTracking = 0,
    KeepTracking = 1
}

public enum WhenNoFlatsRequiredAction
{
    SucceedImmediately = 0,
    WaitUntilRequired = 1
}

public enum WhenNoFilterFeasibleAction
{
    PartialSuccess = 0,
    Wait = 1,
    Fail = 2
}

public enum OnFilterErrorAction
{
    Stop = 0,
    ContinueNextFilter = 1
}

public enum SkyBrightnessSourceMode
{
    Camera = 0,
    Sqm = 1,
    Hybrid = 2
}
