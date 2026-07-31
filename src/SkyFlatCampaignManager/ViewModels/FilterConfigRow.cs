using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SkyFlatCampaignManager.Core.Campaigns;

namespace NINA.Plugin.SkyFlatCampaignManager.ViewModels;

/// <summary>Editable row for Options → per-filter campaign settings.</summary>
public sealed class FilterConfigRow : INotifyPropertyChanged
{
    private bool _enabled = true;
    private int _targetCount = 50;
    private int _minimumAcceptableCount = 30;
    private double _targetAdu = 25000;
    private double _aduTolerance = 2500;
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

    public double TargetAdu
    {
        get => _targetAdu;
        set { if (Math.Abs(_targetAdu - value) < 0.0001) return; _targetAdu = value; Raise(); }
    }

    public double AduTolerance
    {
        get => _aduTolerance;
        set { if (Math.Abs(_aduTolerance - value) < 0.0001) return; _aduTolerance = value; Raise(); }
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
            TargetAdu = s.TargetAdu,
            AduTolerance = s.AduTolerance,
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

    public FilterCampaignSettings ToSettings() => new()
    {
        FilterName = FilterName,
        Enabled = Enabled,
        TargetCount = TargetCount,
        MinimumAcceptableCount = MinimumAcceptableCount,
        TargetAdu = TargetAdu,
        AduTolerance = AduTolerance,
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
