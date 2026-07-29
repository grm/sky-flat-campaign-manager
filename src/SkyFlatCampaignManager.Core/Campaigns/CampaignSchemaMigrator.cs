namespace SkyFlatCampaignManager.Core.Campaigns;

public static class CampaignSchemaMigrator
{
    public static CampaignState Migrate(CampaignState state)
    {
        if (state.SchemaVersion <= 0)
        {
            state.SchemaVersion = 1;
        }

        if (state.SchemaVersion > PluginIdentity.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Campaign schema {state.SchemaVersion} is newer than supported {PluginIdentity.CurrentSchemaVersion}.");
        }

        // v1 is current; future migrations append here without data loss.
        state.SchemaVersion = PluginIdentity.CurrentSchemaVersion;
        state.Filters ??= new Dictionary<string, FilterProgress>(StringComparer.OrdinalIgnoreCase);
        return state;
    }
}
