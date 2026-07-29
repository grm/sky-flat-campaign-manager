using System.Text.Json;
using System.Text.Json.Serialization;
using SkyFlatCampaignManager.Core.Utilities;

namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class JsonCampaignRepository : ICampaignRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IFileSystem _fs;
    private readonly AtomicFileWriter _writer;
    private readonly string _directory;

    public JsonCampaignRepository(IFileSystem fs, string directory)
    {
        _fs = fs;
        _directory = directory;
        _writer = new AtomicFileWriter(fs);
    }

    public Task<CampaignState?> LoadAsync(string campaignKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(campaignKey);
        if (!_fs.FileExists(path))
        {
            var bak = path + ".bak";
            if (_fs.FileExists(bak))
            {
                path = bak;
            }
            else
            {
                return Task.FromResult<CampaignState?>(null);
            }
        }

        try
        {
            var json = _fs.ReadAllText(path);
            var state = JsonSerializer.Deserialize<CampaignState>(json, JsonOptions);
            if (state is null)
            {
                return Task.FromResult<CampaignState?>(null);
            }

            state = CampaignSchemaMigrator.Migrate(state);
            return Task.FromResult<CampaignState?>(state);
        }
        catch (JsonException)
        {
            var bak = GetPath(campaignKey) + ".bak";
            if (_fs.FileExists(bak))
            {
                try
                {
                    var json = _fs.ReadAllText(bak);
                    var state = JsonSerializer.Deserialize<CampaignState>(json, JsonOptions);
                    return Task.FromResult(state is null ? null : CampaignSchemaMigrator.Migrate(state));
                }
                catch (JsonException)
                {
                    return Task.FromResult<CampaignState?>(null);
                }
            }

            return Task.FromResult<CampaignState?>(null);
        }
    }

    public Task SaveAsync(string campaignKey, CampaignState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_fs.DirectoryExists(_directory))
        {
            _fs.CreateDirectory(_directory);
        }

        state.SchemaVersion = PluginIdentity.CurrentSchemaVersion;
        var json = JsonSerializer.Serialize(state, JsonOptions);
        _writer.WriteAtomic(GetPath(campaignKey), json);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string campaignKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(campaignKey);
        if (_fs.FileExists(path))
        {
            _fs.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string campaignKey)
    {
        var safe = string.Join("_", campaignKey.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_directory, $"{safe}.campaign.json");
    }
}
