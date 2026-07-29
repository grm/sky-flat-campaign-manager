using SkyFlatCampaignManager.Core.Utilities;

namespace SkyFlatCampaignManager.Core.Brightness;

public sealed class SqmSkyBrightnessProvider : ISkyBrightnessProvider
{
    private readonly Func<CancellationToken, Task<(double? Sqm, DateTime? CapturedAtUtc)>> _reader;
    private readonly IClock _clock;
    private readonly double _maxAgeSeconds;

    public SqmSkyBrightnessProvider(
        Func<CancellationToken, Task<(double? Sqm, DateTime? CapturedAtUtc)>> reader,
        IClock clock,
        double maxAgeSeconds = 120)
    {
        _reader = reader;
        _clock = clock;
        _maxAgeSeconds = maxAgeSeconds;
    }

    public string Name => "SQM";

    public async Task<SkyBrightnessSample?> GetSampleAsync(CancellationToken cancellationToken = default)
    {
        var (sqm, captured) = await _reader(cancellationToken).ConfigureAwait(false);
        if (sqm is null || double.IsNaN(sqm.Value))
        {
            return null;
        }

        var at = captured ?? _clock.UtcNow;
        var age = (_clock.UtcNow - at).TotalSeconds;
        return new SkyBrightnessSample
        {
            CapturedAtUtc = at,
            SqmMagnitudesPerArcsec2 = sqm,
            Source = Name,
            IsStale = age > _maxAgeSeconds
        };
    }
}
