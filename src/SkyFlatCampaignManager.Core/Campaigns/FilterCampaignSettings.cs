namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class FilterCampaignSettings
{
    public string FilterName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int TargetCount { get; set; } = 50;

    /// <summary>
    /// Enough accepted flats for the current campaign/session to be considered usable
    /// (<see cref="FilterCompletionStatus.MinimumReached"/>), even though it is not yet fully
    /// complete. Never used to mark the multi-day campaign fully <c>Completed</c> — only
    /// <see cref="TargetCount"/> does that. See <see cref="FilterProgress.Status"/>.
    /// </summary>
    public int MinimumAcceptableCount { get; set; } = 30;

    /// <summary>
    /// Normalized target histogram level, 0.0–1.0 of full scale (displayed as 0–100% in the UI).
    /// This is the primary, authoritative acceptance target. Default (~38.15%) preserves the
    /// legacy 25000/65535 ADU behaviour. See <see cref="Acquisition.FlatValidationRequest.TargetHistogramFraction"/>.
    /// </summary>
    public double TargetHistogramFraction { get; set; } = PluginIdentity.DefaultTargetHistogramFraction;

    /// <summary>
    /// NINA-style tolerance as a fraction OF THE TARGET (not of full scale), e.g. 0.10 = ±10% of
    /// target. Default (0.10) preserves the legacy 2500/25000 ADU behaviour.
    /// </summary>
    public double TargetToleranceFraction { get; set; } = PluginIdentity.DefaultTargetToleranceFraction;

    /// <summary>
    /// Legacy raw-ADU target. Superseded by <see cref="TargetHistogramFraction"/>. Kept settable
    /// only so old JSON configuration files (schema &lt; 2) still deserialize; new code should not
    /// read this for acceptance decisions. See <see cref="FilterCampaignConfigMigrator"/>.
    /// Intentionally not marked [Obsolete] to avoid breaking builds with TreatWarningsAsErrors;
    /// treat it as legacy-only by convention.
    /// </summary>
    public double TargetAdu { get; set; } = PluginIdentity.DefaultTargetAdu;

    /// <summary>
    /// Legacy raw-ADU tolerance (fixed ADU count, not a percentage of target). Superseded by
    /// <see cref="TargetToleranceFraction"/>. Kept settable only for backward-compatible JSON
    /// migration. See <see cref="FilterCampaignConfigMigrator"/>.
    /// </summary>
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
