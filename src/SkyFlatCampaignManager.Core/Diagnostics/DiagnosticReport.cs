namespace SkyFlatCampaignManager.Core.Diagnostics;

public sealed class DiagnosticReport
{
    public DateTime GeneratedAtUtc { get; init; }
    public List<DiagnosticCheck> Checks { get; init; } = new();
    public bool AllPassed => Checks.All(c => c.Passed);
}

public sealed class DiagnosticCheck
{
    public string Name { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Detail { get; init; } = string.Empty;
}
