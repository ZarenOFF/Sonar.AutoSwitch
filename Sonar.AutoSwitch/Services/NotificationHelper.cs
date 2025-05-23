using Microsoft.Toolkit.Uwp.Notifications;
using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.ViewModels;
using System;

public static class NotificationHelper
{
    /// <summary>
    /// Sending system toast-notification
    /// </summary>
    public static void Send(string title, string message)
    {
        try
        {
            var settings = StateManager.Instance.GetOrLoadState<SettingsViewModel>();
            if (!settings.EnableNotifications)
            {
                LoggingService.LogDebug($"[NotificationHelper] Skipping notification");
                return;
            }

            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
            LoggingService.LogDebug($"[NotificationHelper] Sended notification \"{title}\" \"{message}\"");
        }
        catch (Exception ex)
        {
            //DebugLog.Log($"Error sending notification: {ex.Message}");
            LoggingService.LogError("[NotificationHelper] Error sending notification", ex);
        }
    }
}