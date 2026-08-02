using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Campaigns;

namespace NINA.Plugin.SkyFlatCampaignManager.ViewModels;

/// <summary>Editable row for Options → per-filter campaign settings.</summary>
public sealed class FilterConfigRow : INotifyPropertyChanged
{
    private bool _enabled = true;
    private int _targetCount = 50;
    private int _minimumAcceptableCount = 30;

    /// <summary>Target histogram level, displayed and edited as 0-100% of the sensor full scale.</summary>
    private double _targetHistogramPercent = PluginIdentity.DefaultTargetHistogramFraction * 100.0;

    /// <summary>Acceptance tolerance, displayed and edited as a percentage OF THE TARGET (NINA-style), not of full scale.</summary>
    private double _targetTolerancePercent = PluginIdentity.DefaultTargetToleranceFraction * 100.0;

    private double _minExposureSeconds = 0.001;
    private double _maxExposureSeconds = 30;
    private int _gain = -1;
    private int _offset = -1;
    private int _binningX = 1;
    private int _binningY = 1;
    private int _eveningOrder = 100;
    private int _morningOrder = 100;
    private int _priority = 100;

    public FilterConfigRow(string filterName) => FilterName = filterName;

    public string FilterName { get; }

    public bool Enabled
    {
        get => _enabled;
        set { if (_enabled == value) return; _enabled = value; Raise(); }
    }

    public int TargetCount
    {
        get => _targetCount;
        set { if (_targetCount == value) return; _targetCount = value; Raise(); }
    }

    public int MinimumAcceptableCount
    {
        get => _minimumAcceptableCount;
        set { if (_minimumAcceptableCount == value) return; _minimumAcceptableCount = value; Raise(); }
    }

    /// <summary>Target histogram level as 0-100% of full scale ("Target histogram level" in the UI).</summary>
    public double TargetHistogramPercent
    {
        get => _targetHistogramPercent;
        set { if (Math.Abs(_targetHistogramPercent - value) < 0.0001) return; _targetHistogramPercent = value; Raise(); Raise(nameof(TargetAduPreview)); }
    }

    /// <summary>Acceptance tolerance as a percentage OF THE TARGET (e.g. 10 = ±10% of target, NINA-style), not of full scale.</summary>
    public double TargetTolerancePercent
    {
        get => _targetTolerancePercent;
        set { if (Math.Abs(_targetTolerancePercent - value) < 0.0001) return; _targetTolerancePercent = value; Raise(); Raise(nameof(TargetAduPreview)); }
    }

    /// <summary>Read-only diagnostic preview assuming a 16-bit (65535) sensor; the actual acceptance always uses the real captured image's bit depth.</summary>
    public string TargetAduPreview
    {
        get
        {
            var targetAdu = _targetHistogramPercent / 100.0 * PluginIdentity.LegacyMigrationMaxAdu;
            var toleranceAdu = targetAdu * _targetTolerancePercent / 100.0;
            return $"≈{targetAdu:F0} ±{toleranceAdu:F0} ADU @16-bit";
        }
    }

    public double MinExposureSeconds
    {
        get => _minExposureSeconds;
        set { if (Math.Abs(_minExposureSeconds - value) < 0.0000001) return; _minExposureSeconds = value; Raise(); }
    }

    public double MaxExposureSeconds
    {
        get => _maxExposureSeconds;
        set { if (Math.Abs(_maxExposureSeconds - value) < 0.0000001) return; _maxExposureSeconds = value; Raise(); }
    }

    /// <summary>-1 = keep camera current gain.</summary>
    public int Gain
    {
        get => _gain;
        set { if (_gain == value) return; _gain = value; Raise(); }
    }

    /// <summary>-1 = keep camera current offset.</summary>
    public int Offset
    {
        get => _offset;
        set { if (_offset == value) return; _offset = value; Raise(); }
    }

    public int BinningX
    {
        get => _binningX;
        set { if (_binningX == value) return; _binningX = Math.Max(1, value); Raise(); }
    }

    public int BinningY
    {
        get => _binningY;
        set { if (_binningY == value) return; _binningY = Math.Max(1, value); Raise(); }
    }

    public int EveningOrder
    {
        get => _eveningOrder;
        set { if (_eveningOrder == value) return; _eveningOrder = value; Raise(); }
    }

    public int MorningOrder
    {
        get => _morningOrder;
        set { if (_morningOrder == value) return; _morningOrder = value; Raise(); }
    }

    public int Priority
    {
        get => _priority;
        set { if (_priority == value) return; _priority = value; Raise(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;

    private void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static FilterConfigRow FromSettings(FilterCampaignSettings s)
    {
        return new FilterConfigRow(s.FilterName)
        {
            Enabled = s.Enabled,
            TargetCount = s.TargetCount,
            MinimumAcceptableCount = s.MinimumAcceptableCount,
            TargetHistogramPercent = s.TargetHistogramFraction * 100.0,
            TargetTolerancePercent = s.TargetToleranceFraction * 100.0,
            MinExposureSeconds = s.MinExposureSeconds,
            MaxExposureSeconds = s.MaxExposureSeconds,
            Gain = s.Gain,
            Offset = s.Offset,
            BinningX = s.BinningX,
            BinningY = s.BinningY,
            EveningOrder = s.ManualEveningOrder,
            MorningOrder = s.ManualMorningOrder,
            Priority = s.Priority
        };
    }

    public FilterCampaignSettings ToSettings()
    {
        var targetHistogramFraction = Math.Clamp(TargetHistogramPercent / 100.0, 0d, 1d);
        var targetToleranceFraction = Math.Max(0d, TargetTolerancePercent / 100.0);

        // Legacy ADU fields are derived only so old builds / external tools reading this JSON
        // still see a sensible value; acceptance always uses the fraction fields above.
        var legacyTargetAdu = targetHistogramFraction * PluginIdentity.LegacyMigrationMaxAdu;
        var legacyAduTolerance = legacyTargetAdu * targetToleranceFraction;

        return new FilterCampaignSettings
        {
            FilterName = FilterName,
            Enabled = Enabled,
            TargetCount = TargetCount,
            MinimumAcceptableCount = MinimumAcceptableCount,
            TargetHistogramFraction = targetHistogramFraction,
            TargetToleranceFraction = targetToleranceFraction,
            TargetAdu = legacyTargetAdu,
            AduTolerance = legacyAduTolerance,
            MinExposureSeconds = MinExposureSeconds,
            MaxExposureSeconds = MaxExposureSeconds,
            Gain = Gain,
            Offset = Offset,
            BinningX = BinningX,
            BinningY = BinningY,
            ManualEveningOrder = EveningOrder,
            ManualMorningOrder = MorningOrder,
            Priority = Priority
        };
    }
}

/// <summary>Minimal ICommand helper (avoids obsolete NINA.Core.Utility.RelayCommand).</summary>
public sealed class PluginRelayCommand : ICommand
{
    private readonly Action _execute;

    public PluginRelayCommand(Action execute) => _execute = execute;

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
