namespace SkyFlatCampaignManager.Core.Notifications;

public interface INotificationService
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Success(string message);
}

public sealed class NullNotificationService : INotificationService
{
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message) { }
    public void Success(string message) { }
}

public sealed class LoggingNotificationService : INotificationService
{
    private readonly Action<string> _log;
    public LoggingNotificationService(Action<string> log) => _log = log;
    public void Info(string message) => _log($"INFO: {message}");
    public void Warning(string message) => _log($"WARN: {message}");
    public void Error(string message) => _log($"ERROR: {message}");
    public void Success(string message) => _log($"OK: {message}");
}
