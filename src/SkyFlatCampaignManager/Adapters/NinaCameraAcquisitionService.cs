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

/// <summary>
/// Captures flats via IImagingMediator.CaptureImage and saves via IImageSaveMediator.Enqueue
/// — same APIs used by NINA TakeExposure.
/// </summary>
public sealed class NinaCameraAcquisitionService : ICameraAcquisitionService
{
    private readonly IProfileService _profileService;
    private readonly ICameraMediator _cameraMediator;
    private readonly IImagingMediator _imagingMediator;
    private readonly IImageSaveMediator _imageSaveMediator;
    private readonly IImageHistoryVM _imageHistoryVM;
    private readonly Action<string>? _log;

    public NinaCameraAcquisitionService(
        IProfileService profileService,
        ICameraMediator cameraMediator,
        IImagingMediator imagingMediator,
        IImageSaveMediator imageSaveMediator,
        IImageHistoryVM imageHistoryVM,
        Action<string>? log = null)
    {
        _profileService = profileService;
        _cameraMediator = cameraMediator;
        _imagingMediator = imagingMediator;
        _imageSaveMediator = imageSaveMediator;
        _imageHistoryVM = imageHistoryVM;
        _log = log;
    }

    public bool IsConnected => _cameraMediator.GetInfo()?.Connected == true;

    /// <summary>
    /// Effective full-scale ADU for the active camera profile, derived from the "Bit Depth"
    /// equipment setting (<c>ICameraSettings.BitDepth</c> — as reported in the image header /
    /// camera info panel per NINA docs). ZWO/QHY/SBIG/FLI/PlayerOne/Atik drivers rescale to this
    /// configured depth (commonly 16-bit); other drivers report their native sensor depth. Never
    /// assume 65535 — always read the profile setting, since 12-bit (4095) and 14-bit (16383)
    /// sensors are common.
    /// </summary>
    private double ResolveMaxAdu()
    {
        var bitDepth = _profileService.ActiveProfile?.CameraSettings?.BitDepth ?? 16d;
        return bitDepth > 0 ? Math.Pow(2, bitDepth) - 1 : 65535d;
    }

    public async Task<CapturedFlatFrame> CaptureFlatAsync(FlatCaptureRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DryRun)
        {
            var dryRunMaxAdu = ResolveMaxAdu();
            return new CapturedFlatFrame
            {
                Success = true,
                Saved = false,
                FilterName = request.FilterName,
                ExposureSeconds = request.ExposureSeconds,
                Gain = request.Gain,
                Offset = request.Offset,
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

            // Prefer NINA statistics (Median) — verified IImageStatistics API.
            var statsTask = imageData.Statistics;
            var ninaStats = statsTask is null ? null : await statsTask.ConfigureAwait(false);

            ImageStatisticsResult stats;
            if (ninaStats is not null)
            {
                var maxAdu = ResolveMaxAdu();
                stats = new ImageStatisticsResult
                {
                    MedianAdu = ninaStats.Median,
                    MeanAdu = ninaStats.Mean,
                    StdDevAdu = ninaStats.StDev,
                    LowPercentileAdu = ninaStats.Min,
                    HighPercentileAdu = ninaStats.Max,
                    SaturatedFraction = ninaStats.Max >= maxAdu * 0.99 ? 0.02 : 0,
                    SamplePixelCount = 1,
                    MaxAdu = maxAdu
                };
            }
            else
            {
                stats = new ImageStatisticsResult { IsCorrupted = true, CorruptionReason = "Statistics unavailable." };
            }

            var prepareTask = _imagingMediator.PrepareImage(imageData, new PrepareImageParameters(false, false), cancellationToken);

            // Campaign metadata as generic FITS headers
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
                Success = true,
                Saved = saved,
                FilterName = request.FilterName,
                ExposureSeconds = request.ExposureSeconds,
                Gain = request.Gain,
                Offset = request.Offset,
                Statistics = stats
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Capture failed: {ex.Message}");
            Logger.Error(ex);
            return new CapturedFlatFrame
            {
                Success = false,
                Saved = false,
                FilterName = request.FilterName,
                ExposureSeconds = request.ExposureSeconds,
                Gain = request.Gain,
                Offset = request.Offset,
                Error = ex.Message,
                Statistics = new ImageStatisticsResult { IsCorrupted = true, CorruptionReason = ex.Message }
            };
        }
    }
}

internal static class FlatCaptureRequestExtensions
{
    /// <summary>Dry-run placeholder median (no real capture happens), derived from the normalized default target and the caller-supplied full-scale ADU.</summary>
    public static double TargetHintAdu(this FlatCaptureRequest _, double maxAdu) => PluginIdentity.DefaultTargetHistogramFraction * maxAdu;
}
