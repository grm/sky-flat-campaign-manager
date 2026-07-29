using FluentAssertions;
using SkyFlatCampaignManager.Core;
using SkyFlatCampaignManager.Core.Campaigns;
using SkyFlatCampaignManager.Core.Utilities;
using Xunit;

namespace SkyFlatCampaignManager.UnitTests;

public class CampaignServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 8, 1, 3, 0, 0, DateTimeKind.Utc);
        public DateTime LocalNow => UtcNow.ToLocalTime();
    }

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
            {
                _files[destinationBackupFileName] = old;
            }
            _files[destinationFileName] = _files[sourceFileName];
            _files.Remove(sourceFileName);
        }
        public void Delete(string path) => _files.Remove(path);
        public void Copy(string source, string destination, bool overwrite) => _files[destination] = _files[source];
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => _files.Keys.Where(k => k.StartsWith(path, StringComparison.OrdinalIgnoreCase));
    }

    private static (CampaignService svc, FakeClock clock, JsonCampaignRepository repo) Create()
    {
        var clock = new FakeClock();
        var repo = new JsonCampaignRepository(new MemoryFs(), "/state");
        return (new CampaignService(repo, clock), clock, repo);
    }

    private static List<FilterCampaignSettings> Filters() => new()
    {
        new() { FilterName = "L", TargetCount = 50, Enabled = true },
        new() { FilterName = "Ha", TargetCount = 50, Enabled = true }
    };

    [Fact]
    public async Task Creates_campaign()
    {
        var (svc, _, _) = Create();
        var state = await svc.GetOrCreateAsync("default", "p1", Filters(), new CampaignOptions());
        state.Status.Should().Be(CampaignStatus.InProgress);
        state.Filters.Should().HaveCount(2);
        state.TotalRemaining.Should().Be(100);
    }

    [Fact]
    public async Task Incomplete_campaign_is_required()
    {
        var (svc, _, _) = Create();
        await svc.GetOrCreateAsync("default", "p1", Filters(), new CampaignOptions());
        await svc.AcceptFlatAsync("default", "L", 1, 25000);
        var req = await svc.EvaluateRequirementAsync("default", new CampaignOptions());
        req.IsRequired.Should().BeTrue();
        req.IsIncomplete.Should().BeTrue();
    }

    [Fact]
    public async Task Completes_and_expires_after_validity()
    {
        var (svc, clock, _) = Create();
        var options = new CampaignOptions { ValidityDays = 60 };
        await svc.GetOrCreateAsync("default", "p1", Filters(), options);
        for (var i = 0; i < 50; i++)
        {
            await svc.AcceptFlatAsync("default", "L", 1, 25000);
            await svc.AcceptFlatAsync("default", "Ha", 5, 25000);
        }

        var completed = await svc.MarkCompletedAsync("default", options);
        completed.Status.Should().Be(CampaignStatus.Completed);
        completed.ValidUntilUtc.Should().NotBeNull();

        clock.UtcNow = clock.UtcNow.AddDays(61);
        var req = await svc.EvaluateRequirementAsync("default", options);
        req.IsExpired.Should().BeTrue();
        req.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Invalidate_marks_required()
    {
        var (svc, _, _) = Create();
        await svc.GetOrCreateAsync("default", "p1", Filters(), new CampaignOptions());
        await svc.InvalidateAsync("default", "Optical train remount");
        var req = await svc.EvaluateRequirementAsync("default", new CampaignOptions());
        req.IsInvalidated.Should().BeTrue();
        req.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Persists_and_resumes()
    {
        var fs = new MemoryFs();
        var clock = new FakeClock();
        var repo = new JsonCampaignRepository(fs, "/state");
        var svc = new CampaignService(repo, clock);
        await svc.GetOrCreateAsync("default", "p1", Filters(), new CampaignOptions());
        await svc.AcceptFlatAsync("default", "L", 0.4, 24000);

        var svc2 = new CampaignService(new JsonCampaignRepository(fs, "/state"), clock);
        var loaded = await svc2.GetOrCreateAsync("default", "p1", Filters(), new CampaignOptions());
        loaded.Filters["L"].Accepted.Should().Be(1);
        loaded.Filters["Ha"].Accepted.Should().Be(0);
    }

    [Fact]
    public async Task Corrupted_file_falls_back_to_bak()
    {
        var fs = new MemoryFs();
        var clock = new FakeClock();
        var repo = new JsonCampaignRepository(fs, "/state");
        var svc = new CampaignService(repo, clock);
        await svc.GetOrCreateAsync("default", "p1", Filters(), new CampaignOptions());
        await svc.AcceptFlatAsync("default", "L", 1, 25000);

        var path = Path.Combine("/state", "default.campaign.json");
        fs.WriteAllText(path + ".bak", fs.ReadAllText(path));
        fs.WriteAllText(path, "{ not-json");

        var loaded = await repo.LoadAsync("default");
        loaded.Should().NotBeNull();
        loaded!.Filters["L"].Accepted.Should().Be(1);
    }

    [Fact]
    public void Schema_migrator_sets_version()
    {
        var state = new CampaignState { SchemaVersion = 0 };
        var migrated = CampaignSchemaMigrator.Migrate(state);
        migrated.SchemaVersion.Should().Be(PluginIdentity.CurrentSchemaVersion);
    }

    [Fact]
    public async Task Reset_single_filter()
    {
        var (svc, _, _) = Create();
        await svc.GetOrCreateAsync("default", "p1", Filters(), new CampaignOptions());
        await svc.AcceptFlatAsync("default", "L", 1, 25000);
        var reset = await svc.ResetFilterAsync("default", "L");
        reset.Filters["L"].Accepted.Should().Be(0);
    }
}
