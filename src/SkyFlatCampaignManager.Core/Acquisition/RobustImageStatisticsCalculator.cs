namespace SkyFlatCampaignManager.Core.Acquisition;

/// <summary>
/// Computes robust ADU statistics from a flat buffer using a central ROI.
/// </summary>
public static class RobustImageStatisticsCalculator
{
    /// <param name="maxAdu">
    /// Effective full-scale ADU for this image (e.g. 4095 for 12-bit, 16383 for 14-bit, 65535
    /// for 16-bit). Stamped onto the result so downstream normalized-fraction validation does
    /// not have to assume 16-bit. Defaults to 65535 for backward compatibility.
    /// </param>
    /// <param name="saturationLevel">
    /// ADU at/above which a pixel is considered saturated. Defaults to <paramref name="maxAdu"/>
    /// when not specified — a sensor can saturate below its nominal digitization ceiling.
    /// </param>
    public static ImageStatisticsResult Compute(
        ReadOnlySpan<ushort> pixels,
        int width,
        int height,
        double roiFraction,
        ushort maxAdu = 65535,
        ushort? saturationLevel = null,
        ushort tooDarkLevel = 100)
    {
        var effectiveSaturationLevel = saturationLevel ?? maxAdu;
        if (width <= 0 || height <= 0 || pixels.Length < width * height)
        {
            return new ImageStatisticsResult
            {
                IsCorrupted = true,
                CorruptionReason = "Invalid image dimensions or buffer."
            };
        }

        roiFraction = Math.Clamp(roiFraction, 0.1, 1.0);
        var roiWidth = Math.Max(1, (int)(width * roiFraction));
        var roiHeight = Math.Max(1, (int)(height * roiFraction));
        var x0 = (width - roiWidth) / 2;
        var y0 = (height - roiHeight) / 2;

        var sample = new List<ushort>(roiWidth * roiHeight);
        long saturated = 0;
        long tooDark = 0;
        double sum = 0;
        double sumSq = 0;

        for (var y = y0; y < y0 + roiHeight; y++)
        {
            var row = y * width;
            for (var x = x0; x < x0 + roiWidth; x++)
            {
                var v = pixels[row + x];
                sample.Add(v);
                sum += v;
                sumSq += (double)v * v;
                if (v >= effectiveSaturationLevel) saturated++;
                if (v <= tooDarkLevel) tooDark++;
            }
        }

        if (sample.Count == 0)
        {
            return new ImageStatisticsResult
            {
                IsCorrupted = true,
                CorruptionReason = "Empty ROI."
            };
        }

        sample.Sort();
        var n = sample.Count;
        var median = n % 2 == 1
            ? sample[n / 2]
            : (sample[n / 2 - 1] + sample[n / 2]) / 2.0;
        var pLow = sample[(int)(n * 0.1)];
        var pHigh = sample[(int)(n * 0.9)];
        var mean = sum / n;
        var variance = Math.Max(0, (sumSq / n) - (mean * mean));
        var std = Math.Sqrt(variance);

        return new ImageStatisticsResult
        {
            MedianAdu = median,
            MeanAdu = mean,
            LowPercentileAdu = pLow,
            HighPercentileAdu = pHigh,
            StdDevAdu = std,
            SaturatedFraction = saturated / (double)n,
            TooDarkFraction = tooDark / (double)n,
            SamplePixelCount = n,
            MaxAdu = maxAdu
        };
    }
}
