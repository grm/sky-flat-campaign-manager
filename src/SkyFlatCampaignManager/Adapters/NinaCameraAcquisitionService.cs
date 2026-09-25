using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Equipment;

namespace NINA.Plugin.SkyFlatCampaignManager.Adapters;

/// <summary>Captures flats via the same NINA APIs used by TakeExposure.</summary>
public sealed class NinaCameraAcquisitionService : ICameraAcquisitionService
{
    private readonly IProfileService _profileService;
    private readonly ICameraMediator _cameraMediator;
    private readonly IImagingMediator _imagingMediator;
    private readonly IImageSaveMediator _imageSaveMediator;
    private readonly IImageHistoryVM _imageHistoryVM;
    private readonly Action<string>? _log;

    public NinaCameraAcquisitionService(IProfileService profileService, ICameraMediator cameraMediator,
        IImagingMediator imagingMediator, IImageSaveMediator imageSaveMediator,
        IImageHistoryVM imageHistoryVM, Action<string>? log = null)
    {
        _profileService = profileService;
        _cameraMediator = cameraMediator;
        _imagingMediator = imagingMediator;
        _imageSaveMediator = imageSaveMediator;
        _imageHistoryVM = imageHistoryVM;
        _log = log;
    }

    public bool IsConnected => _cameraMediator.GetInfo()?.Connected == true;

    private double ResolveConfiguredMaxAdu()
    {
        var bitDepth = _profileService.ActiveProfile?.CameraSettings?.BitDepth ?? 16d;
        return bitDepth > 0 ? Math.Pow(2, bitDepth) - 1 : 65535d;
    }

    /// <summary>
    /// NINA/driver combinations can expose statistics in a wider container than the configured
    /// sensor bit depth. Never allow a measured statistic to exceed the full scale used to
    /// normalize it. If that happens, promote to the next common container ceiling (16-bit).
    /// This keeps histogram fractions physically meaningful instead of producing values > 100%.
    /// </summary>
    private double ResolveStatisticsMaxAdu(double observedMax)
    {
        var configured = ResolveConfiguredMaxAdu();
        if (observedMax <= configured * 1.001)
        {
            return configured;
        }

        _log?.Invoke($"Image statistics exceed configured full scale ({observedMax:F0} > {configured:F0}); using 16-bit statistics scale.");
        return 65535d;
    }

    public async Task<CapturedFlatFrame> CaptureFlatAsync(FlatCaptureRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DryRun)
        {
            var dryRunMaxAdu = ResolveConfiguredMaxAdu();
            return new CapturedFlatFrame
            {
                Success = true, Saved = false, FilterName = request.FilterName,
                ExposureSeconds = request.ExposureSeconds, Gain = request.Gain, Offset = request.Offset,
                Statistics = new ImageStatisticsResult { MedianAdu = request.TargetHintAdu(dryRunMaxAdu), MaxAdu = dryRunMaxAdu }
            };
        }

        try
        {
            var capture = new CaptureSequence
            {
                ExposureTime = request.ExposureSeconds,
                Binning = new BinningMode((short)Math.Max(1, request.BinningX), (short)Math.Max(1, request.BinningY)),
                Gain = request.Gain,
                Offset = request.Offset,
                ImageType = CaptureSequence.ImageTypes.FLAT,
                ProgressExposureCount = 0,
                TotalExposureCount = 1
            };

            var progress = new Progress<ApplicationStatus>(s => { });
            var exposureData = await _imagingMediator.CaptureImage(capture, cancellationToken, progress).ConfigureAwait(false);
            var imageData = await exposureData.ToImageData(progress, cancellationToken).ConfigureAwait(false);
            var statsTask = imageData.Statistics;
            var ninaStats = statsTask is null ? null : await statsTask.ConfigureAwait(false);

            ImageStatisticsResult stats;
            if (ninaStats is not null)
            {
                var maxAdu = ResolveStatisticsMaxAdu(ninaStats.Max);
                stats = new ImageStatisticsResult
                {
                    MedianAdu = ninaStats.Median,
                    MeanAdu = ninaStats.Mean,
                    StdDevAdu = ninaStats.StDev,
                    LowPercentileAdu = ninaStats.Min,
                    HighPercentileAdu = ninaStats.Max,
                    // NINA's aggregate statistics expose Max but not a saturated-pixel count.
                    // A single hot/star pixel must not be fabricated into a 2% saturation fraction.
                    // Keep this unknown/zero until a real pixel-count ROI measurement is available.
                    SaturatedFraction = 0,
                    SamplePixelCount = 1,
                    MaxAdu = maxAdu
                };
            }
            else
            {
                stats = new ImageStatisticsResult { IsCorrupted = true, CorruptionReason = "Statistics unavailable." };
            }

            var prepareTask = _imagingMediator.PrepareImage(imageData, new PrepareImageParameters(false, false), cancellationToken);
            imageData.MetaData.GenericHeaders.Add(new StringMetaDataHeader("SFCMCAMP", request.CampaignId ?? "", "Sky Flat Campaign Id"));
            imageData.MetaData.GenericHeaders.Add(new StringMetaDataHeader("SFCMVER", PluginIdentity.Version, "SFCM plugin version"));
            imageData.MetaData.GenericHeaders.Add(new StringMetaDataHeader("SFCMMODE", request.SessionMode ?? "", "Morning/Evening mode"));
            imageData.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("SFCMADU", stats.MedianAdu, "Measured median ADU"));
            imageData.MetaData.GenericHeaders.Add(new DoubleMetaDataHeader("SFCMHISF", stats.MedianFraction, "Measured median histogram level (0-1 fraction of full scale)"));

            var saved = false;
            if (request.SaveImage)
            {
                await _imageSaveMediator.Enqueue(imageData, prepareTask, progress, cancellationToken).ConfigureAwait(false);
                saved = true;
            }

            return new CapturedFlatFrame
            {
                Success = true, Saved = saved, FilterName = request.FilterName,
                ExposureSeconds = request.ExposureSeconds, Gain = request.Gain, Offset = request.Offset,
                Statistics = stats
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Invoke($"Capture failed: {ex.Message}");
            Logger.Error(ex);
            return new CapturedFlatFrame
            {
                Success = false, Saved = false, FilterName = request.FilterName,
                ExposureSeconds = request.ExposureSeconds, Gain = request.Gain, Offset = request.Offset,
                Error = ex.Message,
                Statistics = new ImageStatisticsResult { IsCorrupted = true, CorruptionReason = ex.Message }
            };
        }
    }
}

internal static class FlatCaptureRequestExtensions
{
    public static double TargetHintAdu(this FlatCaptureRequest _, double maxAdu)
        => PluginIdentity.DefaultTargetHistogramFraction * maxAdu;
}
