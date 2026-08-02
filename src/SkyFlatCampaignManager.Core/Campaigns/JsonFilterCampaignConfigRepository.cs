using System.Text.Json;
using System.Text.Json.Serialization;
using SkyFlatCampaignManager.Core.Utilities;

namespace SkyFlatCampaignManager.Core.Campaigns;

/// <summary>
/// Defaults applied when a filter wheel entry has no saved campaign config yet.
/// </summary>
public sealed class FilterCampaignDefaults
{
    public int TargetCount { get; init; } = 50;
    public double TargetHistogramFraction { get; init; } = PluginIdentity.DefaultTargetHistogramFraction;
    public double TargetToleranceFraction { get; init; } = PluginIdentity.DefaultTargetToleranceFraction;

    /// <summary>Legacy raw-ADU default, retained only for callers that have not migrated to fractions.</summary>
    public double TargetAdu { get; init; } = PluginIdentity.DefaultTargetAdu;

    /// <summary>Legacy raw-ADU tolerance default, retained only for callers that have not migrated to fractions.</summary>
    public double AduTolerance { get; init; } = PluginIdentity.DefaultAduTolerance;
    public double MinExposureSeconds { get; init; } = PluginIdentity.DefaultMinExposureSeconds;
    public double MaxExposureSeconds { get; init; } = PluginIdentity.DefaultMaxExposureSeconds;
    public int Gain { get; init; } = -1;
    public int Offset { get; init; } = -1;
    public int BinningX { get; init; } = 1;
    public int BinningY { get; init; } = 1;
}

/// <summary>
/// On-disk document for per-filter campaign settings (gain, histogram target, counts, etc.).
/// </summary>
public sealed class FilterCampaignConfigDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string ProfileId { get; set; } = string.Empty;
    public List<FilterCampaignSettings> Filters { get; set; } = new();
}

/// <summary>
/// Migrates on-disk filter campaign configuration documents to the current schema.
/// Schema 1 → 2: raw <c>TargetAdu</c>/<c>AduTolerance</c> are converted to normalized
/// <c>TargetHistogramFraction</c>/<c>TargetToleranceFraction</c> (NINA-style, percentage of
/// target). Existing accepted/rejected campaign progress is never touched by this migration —
/// it only affects the acceptance target for future flats.
/// </summary>
public static class FilterCampaignConfigMigrator
{
    public const int CurrentSchemaVersion = 2;

    public static FilterCampaignConfigDocument Migrate(FilterCampaignConfigDocument document)
    {
        if (document.SchemaVersion < 2)
        {
            foreach (var filter in document.Filters)
            {
                MigrateLegacyAduToFraction(filter);
            }
        }

        document.SchemaVersion = CurrentSchemaVersion;
        return document;
    }

    /// <summary>
    /// Converts a single filter's legacy <c>TargetAdu</c>/<c>AduTolerance</c> into
    /// <c>TargetHistogramFraction</c>/<c>TargetToleranceFraction</c> using the legacy 65535
    /// full-scale assumption (the only assumption ever made for pre-existing settings; live
    /// validation always uses the actual captured image's bit depth instead).
    /// </summary>
    public static void MigrateLegacyAduToFraction(FilterCampaignSettings filter)
    {
        if (filter.TargetAdu > 0)
        {
            filter.TargetHistogramFraction = Math.Clamp(filter.TargetAdu / PluginIdentity.LegacyMigrationMaxAdu, 0d, 1d);
        }

        filter.TargetToleranceFraction = filter.TargetAdu > 0 && filter.AduTolerance > 0
            ? filter.AduTolerance / filter.TargetAdu
            : PluginIdentity.DefaultTargetToleranceFraction;
    }
}

/// <summary>
/// Loads/saves per-profile filter campaign configuration as JSON next to campaign state.
/// </summary>
public sealed class JsonFilterCampaignConfigRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IFileSystem _fs;
    private readonly AtomicFileWriter _writer;
    private readonly string _directory;

    public JsonFilterCampaignConfigRepository(IFileSystem fs, string directory)
    {
        _fs = fs;
        _directory = directory;
        _writer = new AtomicFileWriter(fs);
    }

    public string GetPath(string profileId)
    {
        var safe = string.IsNullOrWhiteSpace(profileId)
            ? "default"
            : string.Join("_", profileId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_directory, $"filter-config.{safe}.json");
    }

    public FilterCampaignConfigDocument Load(string profileId)
    {
        var path = GetPath(profileId);
        if (!_fs.FileExists(path))
        {
            return new FilterCampaignConfigDocument { ProfileId = profileId };
        }

        try
        {
            var json = _fs.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<FilterCampaignConfigDocument>(json, JsonOptions);
            if (doc is null)
            {
                return new FilterCampaignConfigDocument { ProfileId = profileId };
            }

            doc.ProfileId = profileId;
            doc.Filters ??= new List<FilterCampaignSettings>();
            return FilterCampaignConfigMigrator.Migrate(doc);
        }
        catch (JsonException)
        {
            return new FilterCampaignConfigDocument { ProfileId = profileId };
        }
    }

    public void Save(string profileId, IEnumerable<FilterCampaignSettings> filters)
    {
        if (!_fs.DirectoryExists(_directory))
        {
            _fs.CreateDirectory(_directory);
        }

        var doc = new FilterCampaignConfigDocument
        {
            SchemaVersion = FilterCampaignConfigMigrator.CurrentSchemaVersion,
            ProfileId = profileId,
            Filters = filters.Select(Clone).ToList()
        };
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        _writer.WriteAtomic(GetPath(profileId), json);
    }

    /// <summary>
    /// Builds the effective filter list: wheel order, overlaying saved settings by name.
    /// Optional <paramref name="seedByName"/> supplies first-time defaults (e.g. Flat Wizard gain).
    /// </summary>
    public static List<FilterCampaignSettings> MergeWithWheel(
        IEnumerable<string> wheelFilterNames,
        IReadOnlyList<FilterCampaignSettings>? saved,
        FilterCampaignDefaults defaults,
        IReadOnlyDictionary<string, FilterCampaignSettings>? seedByName = null)
    {
        var byName = (saved ?? Array.Empty<FilterCampaignSettings>())
            .Where(f => !string.IsNullOrWhiteSpace(f.FilterName))
            .GroupBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var result = new List<FilterCampaignSettings>();
        var order = 0;
        foreach (var name in wheelFilterNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            order++;
            if (byName.TryGetValue(name, out var existing))
            {
                var copy = Clone(existing);
                copy.FilterName = name;
                if (copy.ManualEveningOrder <= 0) copy.ManualEveningOrder = order;
                if (copy.ManualMorningOrder <= 0) copy.ManualMorningOrder = order;
                if (copy.Priority <= 0) copy.Priority = order;
                result.Add(copy);
                continue;
            }

            FilterCampaignSettings? seed = null;
            seedByName?.TryGetValue(name, out seed);

            result.Add(new FilterCampaignSettings
            {
                FilterName = name,
                Enabled = seed?.Enabled ?? true,
                TargetCount = seed?.TargetCount > 0 ? seed.TargetCount : defaults.TargetCount,
                MinimumAcceptableCount = seed?.MinimumAcceptableCount > 0
                    ? seed.MinimumAcceptableCount
                    : Math.Max(1, (int)(defaults.TargetCount * 0.6)),
                TargetHistogramFraction = seed?.TargetHistogramFraction > 0 ? seed.TargetHistogramFraction : defaults.TargetHistogramFraction,
                TargetToleranceFraction = seed?.TargetToleranceFraction > 0 ? seed.TargetToleranceFraction : defaults.TargetToleranceFraction,
                TargetAdu = seed?.TargetAdu > 0 ? seed.TargetAdu : defaults.TargetAdu,
                AduTolerance = seed?.AduTolerance > 0 ? seed.AduTolerance : defaults.AduTolerance,
                MinExposureSeconds = seed?.MinExposureSeconds > 0 ? seed.MinExposureSeconds : defaults.MinExposureSeconds,
                MaxExposureSeconds = seed?.MaxExposureSeconds > 0 ? seed.MaxExposureSeconds : defaults.MaxExposureSeconds,
                Gain = seed?.Gain ?? defaults.Gain,
                Offset = seed?.Offset ?? defaults.Offset,
                BinningX = seed?.BinningX > 0 ? seed.BinningX : defaults.BinningX,
                BinningY = seed?.BinningY > 0 ? seed.BinningY : defaults.BinningY,
                ReadoutMode = seed?.ReadoutMode,
                ManualEveningOrder = seed?.ManualEveningOrder > 0 ? seed.ManualEveningOrder : order,
                ManualMorningOrder = seed?.ManualMorningOrder > 0 ? seed.ManualMorningOrder : order,
                Priority = seed?.Priority > 0 ? seed.Priority : order
            });
        }

        return result;
    }

    private static FilterCampaignSettings Clone(FilterCampaignSettings src) => new()
    {
        FilterName = src.FilterName,
        Enabled = src.Enabled,
        TargetCount = src.TargetCount,
        MinimumAcceptableCount = src.MinimumAcceptableCount,
        TargetHistogramFraction = src.TargetHistogramFraction,
        TargetToleranceFraction = src.TargetToleranceFraction,
        TargetAdu = src.TargetAdu,
        AduTolerance = src.AduTolerance,
        MinExposureSeconds = src.MinExposureSeconds,
        MaxExposureSeconds = src.MaxExposureSeconds,
        Gain = src.Gain,
        Offset = src.Offset,
        BinningX = src.BinningX,
        BinningY = src.BinningY,
        ReadoutMode = src.ReadoutMode,
        ManualEveningOrder = src.ManualEveningOrder,
        ManualMorningOrder = src.ManualMorningOrder,
        Priority = src.Priority
    };
}
