using NINA.Core.Utility.Notification;
using SkyFlatCampaignManager.Core.Notifications;

namespace NINA.Plugin.SkyFlatCampaignManager.Adapters;

public sealed class NinaNotificationService : INotificationService
{
    public void Info(string message) => Notification.ShowInformation(message);
    public void Warning(string message) => Notification.ShowWarning(message);
    public void Error(string message) => Notification.ShowError(message);
    public void Success(string message) => Notification.ShowSuccess(message);
}
