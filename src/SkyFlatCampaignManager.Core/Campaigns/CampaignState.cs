using System.Text.Json.Serialization;

namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class CampaignState
{
    public int SchemaVersion { get; set; } = PluginIdentity.CurrentSchemaVersion;
    public string CampaignId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string CampaignName { get; set; } = PluginIdentity.DisplayName;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public DateTime? InvalidatedAtUtc { get; set; }
    public string? InvalidationReason { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.None;
    public string? OpticalTrainFingerprint { get; set; }
    public Dictionary<string, FilterProgress> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsIncomplete =>
        Status == CampaignStatus.InProgress && Filters.Values.Any(f => f.IsIncomplete);

    [JsonIgnore]
    public bool IsComplete =>
        Status == CampaignStatus.Completed ||
        (Filters.Count > 0 && Filters.Values.Where(f => f.Target > 0).All(f => f.IsComplete));

    public int TotalAccepted => Filters.Values.Sum(f => f.Accepted);
    public int TotalTarget => Filters.Values.Sum(f => f.Target);
    public int TotalRemaining => Filters.Values.Sum(f => f.Remaining);
}
