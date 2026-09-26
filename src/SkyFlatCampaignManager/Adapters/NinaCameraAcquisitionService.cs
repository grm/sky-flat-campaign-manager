using System.Collections.Concurrent;
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
/// Captures flats via the same NINA APIs used by TakeExposure. Captures are measured first and are
/// only submitted to NINA's save queue after the core runner accepts them. Rejected probes therefore
/// neither create files nor enter normal NINA image history through the save pipeline.
/// </summary>
public sealed class NinaCameraAcquisitionService : ICameraAcquisitionService
{
    private readonly IProfileService _profileService;
    private readonly ICameraMediator _cameraMediator;
    private readonly IImagingMediator _imagingMediator;
    private readonly IImageSaveMediator _imageSaveMediator;
    private readonly IImageHistoryVM _imageHistoryVM;
    private readonly Action<string>? _log;
    private readonly ConcurrentDictionary<string, Func<CancellationToken, Task<bool>>> _pendingSaves = new();

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

            string? deferredToken = null;
            if (request.SaveImage)
            {
                deferredToken = Guid.NewGuid().ToString("N");
                _pendingSaves[deferredToken] = async ct =>
                {
                    await _imageSaveMediator.Enqueue(imageData, prepareTask, progress, ct).ConfigureAwait(false);
                    return true;
                };
            }

            return new CapturedFlatFrame
            {
                Success = true, Saved = false, DeferredSaveToken = deferredToken,
                FilterName = request.FilterName, ExposureSeconds = request.ExposureSeconds,
                Gain = request.Gain, Offset = request.Offset, Statistics = stats
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

    public async Task<bool> SaveCapturedFlatAsync(CapturedFlatFrame frame, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(frame.DeferredSaveToken))
        {
            return frame.Success;
        }

        if (!_pendingSaves.TryRemove(frame.DeferredSaveToken, out var save))
        {
            _log?.Invoke($"Deferred save token {frame.DeferredSaveToken} is no longer available.");
            return false;
        }

        try
        {
            return await save(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Invoke($"Deferred flat save failed: {ex.Message}");
            Logger.Error(ex);
            return false;
        }
    }

    public Task DiscardCapturedFlatAsync(CapturedFlatFrame frame, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(frame.DeferredSaveToken))
        {
            _pendingSaves.TryRemove(frame.DeferredSaveToken, out _);
        }
        return Task.CompletedTask;
    }
}

internal static class FlatCaptureRequestExtensions
{
    public static double TargetHintAdu(this FlatCaptureRequest _, double maxAdu)
        => PluginIdentity.DefaultTargetHistogramFraction * maxAdu;
}
