using System.Globalization;

namespace SkyFlatCampaignManager.Core.Campaigns;

/// <summary>
/// Provider-neutral formatting for lifecycle messages. NINA 3.2 integrations can expose the
/// resolved string through a parent container name (Ground Station's $$INSTRUCTION_SET$$ token);
/// NINA 3.3+ integrations may publish the same values through symbols without changing semantics.
/// </summary>
public static class SkyFlatEventMessageFormatter
{
    public static string FormatDefault(SkyFlatSessionEvent e)
    {
        var modePrefix = e.Mode == CampaignMode.Automatic ? string.Empty : $"{e.Mode} ";
        var filter = e.CurrentFilter ?? string.Empty;
        var filterRemaining = FilterProgress(e)?.Remaining ?? 0;

        return e.Kind switch
        {
            SkyFlatSessionEventKind.CampaignRequired =>
                $"SFCM: {modePrefix}sky flats starting — {e.Remaining} flat(s) remaining",
            SkyFlatSessionEventKind.CampaignNotRequired =>
                "SFCM: sky flats skipped — campaign is current (0 remaining)",
            SkyFlatSessionEventKind.BeforeWait =>
                $"SFCM: waiting for twilight{(string.IsNullOrWhiteSpace(filter) ? "" : $" — next {filter}")} — {e.Remaining} remaining",
            SkyFlatSessionEventKind.AfterWait =>
                $"SFCM: twilight ready — resuming{(string.IsNullOrWhiteSpace(filter) ? "" : $" with {filter}")} — {e.Remaining} remaining",
            SkyFlatSessionEventKind.BeforeFilter =>
                $"SFCM: starting {filter} — {filterRemaining} for this filter, {e.Remaining} total remaining",
            SkyFlatSessionEventKind.AfterFilter =>
                $"SFCM: {filter} complete — {e.Remaining} total remaining",
            SkyFlatSessionEventKind.CampaignCompleted =>
                $"SFCM: sky flat campaign complete — {e.Campaign?.TotalAccepted ?? 0} accepted, 0 remaining",
            SkyFlatSessionEventKind.SessionIncomplete when e.StopReason == SessionStopReasons.EveningStartTooLate =>
                $"SFCM: evening sky flats skipped — twilight too advanced — {e.Remaining} remaining",
            SkyFlatSessionEventKind.SessionIncomplete =>
                $"SFCM: sky-flat session ended incomplete — {e.Remaining} remaining — {e.StopReason ?? "unknown reason"}",
            SkyFlatSessionEventKind.Error =>
                $"SFCM error: {e.StatusMessage ?? e.Exception?.Message ?? "unknown error"}",
            _ => $"SFCM: {e.Kind}"
        };
    }

    public static string ResolveTemplate(string template, SkyFlatSessionEvent e)
    {
        if (string.IsNullOrWhiteSpace(template)) return FormatDefault(e);

        var culture = CultureInfo.InvariantCulture;
        var filter = e.CurrentFilter ?? string.Empty;
        var fp = FilterProgress(e);

        return template
            .Replace("{campaign}", e.CampaignKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{mode}", e.Mode.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{state}", e.Kind.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{remaining}", e.Remaining.ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{required}", e.ConfiguredTarget.ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{accepted}", (e.Campaign?.TotalAccepted ?? 0).ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{sessionAccepted}", e.AcceptedThisSession.ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{sessionRejected}", e.RejectedThisSession.ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{filter}", filter, StringComparison.OrdinalIgnoreCase)
            .Replace("{filterRemaining}", (fp?.Remaining ?? 0).ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{filterAccepted}", (fp?.Accepted ?? 0).ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{filterRequired}", (fp?.Target ?? 0).ToString(culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{exposure}", (e.ExposureSeconds ?? 0).ToString("0.###", culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{adu}", (e.MeasuredAdu ?? 0).ToString("0", culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{histogram}", ((e.MeasuredHistogramFraction ?? 0) * 100.0).ToString("0.0", culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{sunAltitude}", (e.SunAltitudeDegrees ?? double.NaN).ToString("0.00", culture), StringComparison.OrdinalIgnoreCase)
            .Replace("{waitReason}", e.WaitReason ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{stopReason}", e.StopReason ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{duration}", e.Duration.ToString(@"hh\:mm\:ss", culture), StringComparison.OrdinalIgnoreCase);
    }

    private static FilterProgress? FilterProgress(SkyFlatSessionEvent e)
    {
        if (string.IsNullOrWhiteSpace(e.CurrentFilter) || e.Campaign is null) return null;
        return e.Campaign.Filters.TryGetValue(e.CurrentFilter, out var fp) ? fp : null;
    }
}
