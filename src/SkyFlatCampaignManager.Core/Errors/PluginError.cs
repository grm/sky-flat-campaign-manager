namespace SkyFlatCampaignManager.Core.Errors;

public enum ErrorCategory
{
    Recoverable,
    NonRecoverable,
    Filter,
    Session,
    Storage,
    Safety,
    Cancellation
}

public sealed class PluginError : Exception
{
    public ErrorCategory Category { get; }

    public PluginError(string message, ErrorCategory category, Exception? inner = null)
        : base(message, inner)
    {
        Category = category;
    }
}
