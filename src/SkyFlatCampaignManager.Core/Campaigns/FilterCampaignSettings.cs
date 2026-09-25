namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class FilterCampaignSettings
{
    public string FilterName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int TargetCount { get; set; } = 30;

    /// <summary>Enough accepted flats for the current campaign/session to be considered usable.</summary>
    public int MinimumAcceptableCount { get; set; } = 20;

    /// <summary>Normalized target histogram level, 0.0–1.0 of full scale.</summary>
    public double TargetHistogramFraction { get; set; } = PluginIdentity.DefaultTargetHistogramFraction;

    /// <summary>NINA-style tolerance as a fraction OF THE TARGET.</summary>
    public double TargetToleranceFraction { get; set; } = PluginIdentity.DefaultTargetToleranceFraction;

    /// <summary>Legacy raw-ADU target. Retained only for backward-compatible JSON migration.</summary>
    public double TargetAdu { get; set; } = PluginIdentity.DefaultTargetAdu;

    /// <summary>Legacy raw-ADU tolerance. Retained only for backward-compatible JSON migration.</summary>
    public double AduTolerance { get; set; } = PluginIdentity.DefaultAduTolerance;

    public double MinExposureSeconds { get; set; } = PluginIdentity.DefaultMinExposureSeconds;
    public double MaxExposureSeconds { get; set; } = PluginIdentity.DefaultMaxExposureSeconds;
    public int Gain { get; set; } = -1;
    public int Offset { get; set; } = -1;
    public int BinningX { get; set; } = 1;
    public int BinningY { get; set; } = 1;
    public int? ReadoutMode { get; set; }
    public int ManualEveningOrder { get; set; } = 100;
    public int ManualMorningOrder { get; set; } = 100;
    public int Priority { get; set; } = 100;
}
