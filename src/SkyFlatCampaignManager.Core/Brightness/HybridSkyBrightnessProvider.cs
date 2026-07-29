namespace SkyFlatCampaignManager.Core.Brightness;

public sealed class HybridSkyBrightnessProvider : ISkyBrightnessProvider
{
    private readonly ISkyBrightnessProvider _camera;
    private readonly ISkyBrightnessProvider _sqm;
    private readonly Action<string>? _log;

    public HybridSkyBrightnessProvider(ISkyBrightnessProvider camera, ISkyBrightnessProvider sqm, Action<string>? log = null)
    {
        _camera = camera;
        _sqm = sqm;
        _log = log;
    }

    public string Name => "Hybrid";

    public async Task<SkyBrightnessSample?> GetSampleAsync(CancellationToken cancellationToken = default)
    {
        SkyBrightnessSample? sqmSample = null;
        try
        {
            sqmSample = await _sqm.GetSampleAsync(cancellationToken).ConfigureAwait(false);
            if (sqmSample?.IsStale == true)
            {
                _log?.Invoke("SQM sample stale; using camera authority.");
                sqmSample = null;
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"SQM unavailable ({ex.Message}); falling back to camera.");
        }

        var camera = await _camera.GetSampleAsync(cancellationToken).ConfigureAwait(false);
        if (camera is null)
        {
            return sqmSample;
        }

        return new SkyBrightnessSample
        {
            CapturedAtUtc = camera.CapturedAtUtc,
            CameraMedianAdu = camera.CameraMedianAdu,
            CameraExposureSeconds = camera.CameraExposureSeconds,
            SqmMagnitudesPerArcsec2 = sqmSample?.SqmMagnitudesPerArcsec2,
            Source = sqmSample is null ? "Camera(fallback)" : Name,
            IsStale = false
        };
    }
}
