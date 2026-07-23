using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.Networking.Sockets;

namespace Background.BackgroundTask
{
    public sealed class BackgroundTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnCanceled;

            try
            {
                Debug.WriteLine("[BGTask] Woke up at " + DateTime.Now);

                // CCT разбудил нас — значит пришли данные на сокет
                // Foreground receive loop их обработает если приложение запущено
                // Если нет — читаем LocalSettings которые foreground успел записать
                var details = taskInstance.TriggerDetails
                    as IControlChannelTriggerEventDetails;

                if (details != null)
                {
                    details.ControlChannelTrigger?.FlushTransport();
                    Debug.WriteLine("[BGTask] FlushTransport called");
                }

                // Даём foreground время обработать пакет если он запущен
                await Task.Delay(2000);

                // Проверяем есть ли непрочитанные сообщения
                var settings = ApplicationData.Current.LocalSettings;
                object unreadObj = settings.Values["UnreadCount"];
                int unread = unreadObj != null ? (int)unreadObj : 0;

                if (unread > 0)
                {
                    string sender = settings.Values["LastMessageSender"] as string ?? "kicq";
                    string text = settings.Values["LastMessageText"] as string ?? "Новое сообщение";

                    // Показываем только если foreground не показал сам
                    // (foreground сбрасывает флаг ShowPendingToast после показа)
                    object pendingToast = settings.Values["ShowPendingToast"];
                    if (pendingToast != null && (bool)pendingToast)
                    {
                        ShowToast(sender, text, unread);
                        settings.Values["ShowPendingToast"] = false;
                        Debug.WriteLine("[BGTask] Toast shown for: " + sender);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[BGTask] Error: " + ex.Message);
            }
            finally
            {
                _deferral.Complete();
            }
        }

        private void ShowToast(string sender, string text, int count)
        {
            try
            {
                string badge = count > 1 ? " (" + count + ")" : "";
                string title = EscapeXml(sender + badge);
                string body = EscapeXml(
                    text.Length > 100 ? text.Substring(0, 100) + "..." : text);

                string xml =
                    "<toast launch='openApp'>" +
                    "<visual><binding template='ToastGeneric'>" +
                    "<text>" + title + "</text>" +
                    "<text>" + body + "</text>" +
                    "</binding></visual>" +
                    "</toast>";

                var doc = new XmlDocument();
                doc.LoadXml(xml);
                ToastNotificationManager.CreateToastNotifier()
                    .Show(new ToastNotification(doc));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[BGTask] Toast error: " + ex.Message);
            }
        }

        private string EscapeXml(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;");
        }

        private void OnCanceled(IBackgroundTaskInstance sender,
            BackgroundTaskCancellationReason reason)
        {
            Debug.WriteLine("[BGTask] Cancelled: " + reason);
            _deferral?.Complete();
        }
    }
}