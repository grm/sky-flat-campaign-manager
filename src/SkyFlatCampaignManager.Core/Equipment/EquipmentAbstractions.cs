using SkyFlatCampaignManager.Core.Acquisition;

namespace SkyFlatCampaignManager.Core.Equipment;

public interface ICameraAcquisitionService
{
    bool IsConnected { get; }
    Task<CapturedFlatFrame> CaptureFlatAsync(FlatCaptureRequest request, CancellationToken cancellationToken = default);
}

public sealed class FlatCaptureRequest
{
    public string FilterName { get; init; } = string.Empty;
    public double ExposureSeconds { get; init; }
    public int Gain { get; init; } = -1;
    public int Offset { get; init; } = -1;
    public int BinningX { get; init; } = 1;
    public int BinningY { get; init; } = 1;
    public bool SaveImage { get; init; } = true;
    public bool DryRun { get; init; }
    public string? CampaignId { get; init; }
    public string? SessionMode { get; init; }
}

public sealed class CapturedFlatFrame
{
    public bool Success { get; init; }
    public bool Saved { get; init; }
    public string FilterName { get; init; } = string.Empty;
    public double ExposureSeconds { get; init; }
    public int Gain { get; init; }
    public int Offset { get; init; }
    public ImageStatisticsResult Statistics { get; init; } = new();
    public string? Error { get; init; }
}

public interface IFilterWheelService
{
    bool IsConnected { get; }
    IReadOnlyList<string> FilterNames { get; }
    Task ChangeFilterAsync(string filterName, CancellationToken cancellationToken = default);
    string? CurrentFilterName { get; }
}

public interface IMountPositioningService
{
    bool IsConnected { get; }
    Task EnsureSafePointingAsync(MountPointingRequest request, CancellationToken cancellationToken = default);
    Task RestoreIfRequestedAsync(CancellationToken cancellationToken = default);
}

public sealed class MountPointingRequest
{
    public Campaigns.MountPointingMode Mode { get; init; }
    public Campaigns.TrackingMode Tracking { get; init; }
    public double? AltitudeDegrees { get; init; }
    public double? AzimuthDegrees { get; init; }
    public double? SunOffsetDegrees { get; init; }
    public double MinSunSeparationDegrees { get; init; } = PluginIdentity.DefaultSunSafetySeparationDegrees;
    public bool DitherBetweenFrames { get; init; }
    public bool RestoreAtEnd { get; init; }
}

public interface IEquipmentHealthCheck
{
    EquipmentHealthResult Check();
}

public sealed class EquipmentHealthResult
{
    public bool Ok { get; init; }
    public List<string> Issues { get; init; } = new();
}
