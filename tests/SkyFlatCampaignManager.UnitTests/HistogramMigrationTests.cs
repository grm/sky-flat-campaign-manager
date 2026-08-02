using FluentAssertions;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Acquisition;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Utilities;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

public class HistogramMigrationTests
{
    private sealed class MemoryFs : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
        public bool FileExists(string path) => _files.ContainsKey(path);
        public bool DirectoryExists(string path) => true;
        public void CreateDirectory(string path) { }
        public string ReadAllText(string path) => _files[path];
        public void WriteAllText(string path, string contents) => _files[path] = contents;
        public void WriteAllBytes(string path, byte[] contents) => _files[path] = Convert.ToBase64String(contents);
        public byte[] ReadAllBytes(string path) => Convert.FromBase64String(_files[path]);
        public void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName)
        {
            if (destinationBackupFileName is not null && _files.TryGetValue(destinationFileName, out var old))
                _files[destinationBackupFileName] = old;
            _files[destinationFileName] = _files[sourceFileName];
            _files.Remove(sourceFileName);
        }
        public void Delete(string path) => _files.Remove(path);
        public void Copy(string source, string destination, bool overwrite) => _files[destination] = _files[source];
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => _files.Keys;

        public void Seed(string path, string contents) => _files[path] = contents;
    }

    [Fact]
    public void Default_fraction_constants_preserve_legacy_25000_over_65535_behaviour()
    {
        PluginIdentity.DefaultTargetHistogramFraction.Should().BeApproximately(0.3815, 0.0005);
        PluginIdentity.DefaultTargetToleranceFraction.Should().BeApproximately(0.10, 0.0001);
    }

    [Fact]
    public void Migrator_converts_legacy_defaults_to_expected_fractions()
    {
        var filter = new FilterCampaignSettings { FilterName = "L", TargetAdu = 25000, AduTolerance = 2500 };
        FilterCampaignConfigMigrator.MigrateLegacyAduToFraction(filter);

        filter.TargetHistogramFraction.Should().BeApproximately(0.3815, 0.0005);
        filter.TargetToleranceFraction.Should().BeApproximately(0.10, 0.0001);
    }

    [Fact]
    public void Migrator_converts_custom_legacy_values()
    {
        // A user who customized TargetAdu/AduTolerance before upgrading must keep equivalent behaviour.
        var filter = new FilterCampaignSettings { FilterName = "Ha", TargetAdu = 30000, AduTolerance = 6000 };
        FilterCampaignConfigMigrator.MigrateLegacyAduToFraction(filter);

        filter.TargetHistogramFraction.Should().BeApproximately(30000d / 65535d, 0.0001);
        filter.TargetToleranceFraction.Should().BeApproximately(0.20, 0.0001); // 6000/30000
    }

    [Fact]
    public void Document_migration_bumps_schema_version_and_only_applies_to_old_documents()
    {
        var oldDoc = new FilterCampaignConfigDocument
        {
            SchemaVersion = 1,
            Filters = { new FilterCampaignSettings { FilterName = "L", TargetAdu = 25000, AduTolerance = 2500 } }
        };
        var migrated = FilterCampaignConfigMigrator.Migrate(oldDoc);
        migrated.SchemaVersion.Should().Be(FilterCampaignConfigMigrator.CurrentSchemaVersion);
        migrated.Filters[0].TargetHistogramFraction.Should().BeApproximately(0.3815, 0.0005);

        // A document already at the current schema must not have its fraction recomputed from ADU
        // (the fraction is authoritative going forward; legacy ADU fields may be stale).
        var newDoc = new FilterCampaignConfigDocument
        {
            SchemaVersion = FilterCampaignConfigMigrator.CurrentSchemaVersion,
            Filters = { new FilterCampaignSettings { FilterName = "L", TargetAdu = 999, AduTolerance = 999, TargetHistogramFraction = 0.5, TargetToleranceFraction = 0.2 } }
        };
        var unchanged = FilterCampaignConfigMigrator.Migrate(newDoc);
        unchanged.Filters[0].TargetHistogramFraction.Should().Be(0.5);
        unchanged.Filters[0].TargetToleranceFraction.Should().Be(0.2);
    }

    [Fact]
    public void Repository_migrates_legacy_json_file_on_load_without_losing_other_settings()
    {
        var fs = new MemoryFs();
        var repo = new JsonFilterCampaignConfigRepository(fs, "/state");
        var legacyJson = """
        {
          "schemaVersion": 1,
          "profileId": "p1",
          "filters": [
            { "filterName": "L", "enabled": true, "targetCount": 40, "minimumAcceptableCount": 20, "targetAdu": 25000, "aduTolerance": 2500, "minExposureSeconds": 0.01, "maxExposureSeconds": 30, "gain": 100, "offset": 10, "binningX": 1, "binningY": 1 }
          ]
        }
        """;
        fs.Seed(repo.GetPath("p1"), legacyJson);

        var doc = repo.Load("p1");

        doc.SchemaVersion.Should().Be(FilterCampaignConfigMigrator.CurrentSchemaVersion);
        var filter = doc.Filters.Single();
        filter.TargetCount.Should().Be(40); // untouched
        filter.Gain.Should().Be(100); // untouched
        filter.TargetHistogramFraction.Should().BeApproximately(0.3815, 0.0005);
        filter.TargetToleranceFraction.Should().BeApproximately(0.10, 0.0001);
    }

    [Fact]
    public async Task Existing_campaign_progress_is_not_invalidated_by_settings_migration()
    {
        // The filter config migration only touches FilterCampaignSettings (the target/tolerance
        // configuration document); it must never reach into or reset campaign progress.
        var fs = new MemoryFs();
        var configRepo = new JsonFilterCampaignConfigRepository(fs, "/state");
        var campaignRepo = new JsonCampaignRepository(fs, "/state");

        var campaign = new CampaignState
        {
            CampaignId = "2026-08-01-default",
            Status = CampaignStatus.InProgress,
            Filters = { ["L"] = new FilterProgress { FilterName = "L", Target = 50, Accepted = 12 } }
        };
        await campaignRepo.SaveAsync("default", campaign);

        configRepo.Save("p1", new[] { new FilterCampaignSettings { FilterName = "L", TargetAdu = 25000, AduTolerance = 2500 } });
        var migratedDoc = configRepo.Load("p1");
        migratedDoc.Filters[0].TargetHistogramFraction.Should().BeGreaterThan(0);

        var reloadedCampaign = await campaignRepo.LoadAsync("default");
        reloadedCampaign!.Filters["L"].Accepted.Should().Be(12);
    }

    [Theory]
    [InlineData(4095)]
    [InlineData(16383)]
    [InlineData(65535)]
    public void Robust_statistics_stamp_the_requested_bit_depth(int bitDepthMax)
    {
        var buf = new ushort[64 * 64];
        Array.Fill(buf, (ushort)(bitDepthMax / 2));
        var stats = RobustImageStatisticsCalculator.Compute(buf, 64, 64, 0.7, maxAdu: (ushort)bitDepthMax);
        stats.MaxAdu.Should().Be(bitDepthMax);
        stats.MedianFraction.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void Robust_statistics_default_to_65535_for_backward_compatibility()
    {
        var buf = new ushort[10 * 10];
        Array.Fill(buf, (ushort)1000);
        var stats = RobustImageStatisticsCalculator.Compute(buf, 10, 10, 1.0);
        stats.MaxAdu.Should().Be(65535);
    }
}
