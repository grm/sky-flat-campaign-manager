namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class FilterCampaignSettings
{
    public string FilterName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int TargetCount { get; set; } = 50;
    public int MinimumAcceptableCount { get; set; } = 30;
    public double TargetAdu { get; set; } = PluginIdentity.DefaultTargetAdu;
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
