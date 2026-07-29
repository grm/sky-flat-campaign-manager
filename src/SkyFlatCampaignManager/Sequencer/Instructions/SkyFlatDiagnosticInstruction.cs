using System.ComponentModel.Composition;
using System.Text;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.SkyFlatCampaignManager.Adapters;
using NINA.Plugin.SkyFlatCampaignManager.Services;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Equipment;

namespace NINA.Plugin.SkyFlatCampaignManager.Sequencer.Instructions;

[ExportMetadata("Name", "Sky Flat Diagnostic")]
[ExportMetadata("Description", "Runs camera/filter/SQM/path/sun diagnostics for Sky Flat Campaign Manager.")]
[ExportMetadata("Icon", "Plugin_Test_SVG")]
[ExportMetadata("Category", "Sky Flat Campaign Manager")]
[Export(typeof(ISequenceItem))]
[JsonObject(MemberSerialization.OptIn)]
public class SkyFlatDiagnosticInstruction : SequenceItem
{
    private readonly IProfileService _profileService;
    private readonly ICameraMediator _cameraMediator;
    private readonly IFilterWheelMediator _filterWheelMediator;
    private readonly IWeatherDataMediator _weatherDataMediator;
    private readonly IImagingMediator _imagingMediator;
    private readonly IImageSaveMediator _imageSaveMediator;
    private readonly IImageHistoryVM _imageHistoryVM;

    [ImportingConstructor]
    public SkyFlatDiagnosticInstruction(
        IProfileService profileService,
        ICameraMediator cameraMediator,
        IFilterWheelMediator filterWheelMediator,
        IWeatherDataMediator weatherDataMediator,
        IImagingMediator imagingMediator,
        IImageSaveMediator imageSaveMediator,
        IImageHistoryVM imageHistoryVM)
    {
        _profileService = profileService;
        _cameraMediator = cameraMediator;
        _filterWheelMediator = filterWheelMediator;
        _weatherDataMediator = weatherDataMediator;
        _imagingMediator = imagingMediator;
        _imageSaveMediator = imageSaveMediator;
        _imageHistoryVM = imageHistoryVM;
        TakeTestImage = true;
        TestExposureSeconds = 0.5;
    }

    private SkyFlatDiagnosticInstruction(SkyFlatDiagnosticInstruction copyMe) : this(
        copyMe._profileService,
        copyMe._cameraMediator,
        copyMe._filterWheelMediator,
        copyMe._weatherDataMediator,
        copyMe._imagingMediator,
        copyMe._imageSaveMediator,
        copyMe._imageHistoryVM)
    {
        CopyMetaData(copyMe);
        TakeTestImage = copyMe.TakeTestImage;
        TestExposureSeconds = copyMe.TestExposureSeconds;
    }

    [JsonProperty]
    public bool TakeTestImage { get; set; }

    [JsonProperty]
    public double TestExposureSeconds { get; set; }

    public string LastReport { get; private set; } = string.Empty;

    public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{PluginIdentity.DisplayName} diagnostic");
        sb.AppendLine($"State dir: {PluginServiceFactory.ResolveStateDirectory()}");
        sb.AppendLine($"Camera connected: {_cameraMediator.GetInfo()?.Connected == true}");
        sb.AppendLine($"Filter wheel connected: {_filterWheelMediator.GetInfo()?.Connected == true}");

        var weather = _weatherDataMediator.GetInfo();
        sb.AppendLine($"Weather connected: {weather?.Connected == true}");
        if (weather?.Connected == true && !double.IsNaN(weather.SkyQuality))
        {
            sb.AppendLine($"SQM/SkyQuality: {weather.SkyQuality:F2} mag/arcsec²");
        }
        else
        {
            sb.AppendLine("SQM/SkyQuality: unavailable (optional)");
        }

        var sun = new NinaSunAltitudeProvider(_profileService);
        sb.AppendLine($"Sun altitude: {sun.GetSunAltitudeDegrees(DateTime.UtcNow):F2}°");

        var path = _profileService.ActiveProfile.ImageFileSettings.FilePath;
        sb.AppendLine($"Image path set: {!string.IsNullOrWhiteSpace(path)}");
        sb.AppendLine($"Image path exists: {!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)}");

        if (TakeTestImage && _cameraMediator.GetInfo()?.Connected == true)
        {
            var cam = new NinaCameraAcquisitionService(
                _profileService,
                _cameraMediator,
                _imagingMediator,
                _imageSaveMediator,
                _imageHistoryVM);
            var filter = _filterWheelMediator.GetInfo()?.SelectedFilter?.Name ?? "L";
            var frame = await cam.CaptureFlatAsync(new FlatCaptureRequest
            {
                FilterName = filter,
                ExposureSeconds = TestExposureSeconds,
                SaveImage = false,
                DryRun = false
            }, token).ConfigureAwait(false);
            sb.AppendLine($"Test capture success: {frame.Success}");
            sb.AppendLine($"Measured median ADU: {frame.Statistics.MedianAdu:F0}");
            var est = new ProportionalFlatExposureEstimator().EstimateNextExposureSeconds(
                TestExposureSeconds,
                Math.Max(1, frame.Statistics.MedianAdu),
                PluginIdentity.DefaultTargetAdu,
                0.001,
                30);
            sb.AppendLine($"Estimated exposure for target ADU: {est:F3}s");
        }

        LastReport = sb.ToString();
        RaisePropertyChanged(nameof(LastReport));
        var preview = LastReport.Length > 400 ? LastReport[..400] + "…" : LastReport;
        Notification.ShowInformation(preview);
        progress?.Report(new ApplicationStatus { Status = "SFCM diagnostic complete" });
    }

    public override object Clone() => new SkyFlatDiagnosticInstruction(this);

    public override string ToString()
        => $"Category: {Category}, Item: {nameof(SkyFlatDiagnosticInstruction)}";
}
