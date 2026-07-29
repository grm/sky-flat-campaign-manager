namespace SkyFlatCampaignManager.Core.Brightness;

public interface ISkyBrightnessProvider
{
    string Name { get; }
    Task<SkyBrightnessSample?> GetSampleAsync(CancellationToken cancellationToken = default);
}

public sealed class SkyBrightnessSample
{
    public DateTime CapturedAtUtc { get; init; }
    public double? CameraMedianAdu { get; init; }
    public double? CameraExposureSeconds { get; init; }
    public double? SqmMagnitudesPerArcsec2 { get; init; }
    public string Source { get; init; } = string.Empty;
    public bool IsStale { get; init; }
}
