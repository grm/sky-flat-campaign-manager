namespace SkyFlatCampaignManager.Core.Utilities;

public interface IClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => DateTime.Now;
}
