namespace SkyFlatCampaignManager.Core.Brightness;

/// <summary>
/// Placeholder camera provider used by the Core engine; the NINA adapter supplies real captures.
/// </summary>
public sealed class CameraSkyBrightnessProvider : ISkyBrightnessProvider
{
    private readonly Func<CancellationToken, Task<SkyBrightnessSample?>> _sampler;

    public CameraSkyBrightnessProvider(Func<CancellationToken, Task<SkyBrightnessSample?>> sampler)
    {
        _sampler = sampler;
    }

    public string Name => "Camera";

    public Task<SkyBrightnessSample?> GetSampleAsync(CancellationToken cancellationToken = default)
        => _sampler(cancellationToken);
}
