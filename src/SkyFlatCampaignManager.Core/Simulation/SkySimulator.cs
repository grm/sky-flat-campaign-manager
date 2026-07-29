using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Equipment;

namespace SkyFlatCampaignManager.Core.Simulation;

public sealed class SkySimulatorOptions
{
    public bool Darkening { get; set; } = true;
    public bool Clouds { get; set; }
    public bool CameraFault { get; set; }
    public bool FilterWheelFault { get; set; }
    public double BaseSkyAduPerSecond { get; set; } = 8000;
    public Dictionary<string, double> FilterTransmission { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["L"] = 1.0,
        ["R"] = 0.35,
        ["G"] = 0.4,
        ["B"] = 0.3,
        ["Ha"] = 0.05,
        ["OIII"] = 0.06,
        ["SII"] = 0.04
    };
}

public sealed class SimulatedCameraAcquisitionService : ICameraAcquisitionService
{
    private readonly SkySimulatorOptions _options;
    private readonly Random _random;
    private double _skyFactor = 1.0;

    public SimulatedCameraAcquisitionService(SkySimulatorOptions options, int? seed = null)
    {
        _options = options;
        _random = seed is null ? new Random() : new Random(seed.Value);
    }

    public bool IsConnected => !_options.CameraFault;

    public Task<CapturedFlatFrame> CaptureFlatAsync(FlatCaptureRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_options.CameraFault)
        {
            return Task.FromResult(new CapturedFlatFrame
            {
                Success = false,
                Error = "Simulated camera fault"
            });
        }

        if (_options.Darkening)
        {
            _skyFactor *= 0.97;
        }
        else
        {
            _skyFactor *= 1.03;
        }

        if (_options.Clouds && _random.NextDouble() < 0.2)
        {
            _skyFactor *= 0.5 + _random.NextDouble() * 0.4;
        }

        var transmission = _options.FilterTransmission.TryGetValue(request.FilterName, out var t) ? t : 0.5;
        var expectedMean = Math.Max(1, _options.BaseSkyAduPerSecond * transmission * request.ExposureSeconds * _skyFactor);
        var noise = (_random.NextDouble() - 0.5) * expectedMean * 0.02;
        var median = Math.Clamp(expectedMean + noise, 0, 65535);

        var width = 64;
        var height = 64;
        var buffer = new ushort[width * height];
        for (var i = 0; i < buffer.Length; i++)
        {
            var star = _random.NextDouble() < 0.002 ? 20000 : 0;
            var v = median + (_random.NextDouble() - 0.5) * 200 + star;
            buffer[i] = (ushort)Math.Clamp(v, 0, 65535);
        }

        var stats = RobustImageStatisticsCalculator.Compute(buffer, width, height, 0.7);
        return Task.FromResult(new CapturedFlatFrame
        {
            Success = true,
            Saved = !request.DryRun,
            FilterName = request.FilterName,
            ExposureSeconds = request.ExposureSeconds,
            Gain = request.Gain,
            Offset = request.Offset,
            Statistics = stats
        });
    }
}

public sealed class SimulatedFilterWheelService : IFilterWheelService
{
    private readonly SkySimulatorOptions _options;
    private readonly List<string> _filters;

    public SimulatedFilterWheelService(IEnumerable<string> filters, SkySimulatorOptions options)
    {
        _filters = filters.ToList();
        _options = options;
        CurrentFilterName = _filters.FirstOrDefault();
    }

    public bool IsConnected => !_options.FilterWheelFault;
    public IReadOnlyList<string> FilterNames => _filters;
    public string? CurrentFilterName { get; private set; }

    public Task ChangeFilterAsync(string filterName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_options.FilterWheelFault)
        {
            throw new InvalidOperationException("Simulated filter wheel fault");
        }

        if (!_filters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Filter '{filterName}' not found.");
        }

        CurrentFilterName = filterName;
        return Task.CompletedTask;
    }
}

public sealed class NoOpMountPositioningService : IMountPositioningService
{
    public bool IsConnected => true;
    public Task EnsureSafePointingAsync(MountPointingRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task RestoreIfRequestedAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
