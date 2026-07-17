using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace kicquwp.Background
{
    public sealed class BackgroundTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnCanceled;

            Debug.WriteLine("[BGTask] Started at " + DateTime.Now);

            try
            {
                var settings = ApplicationData.Current.LocalSettings;

                object unreadObj = settings.Values["UnreadCount"];
                int unread = unreadObj != null ? (int)unreadObj : 0;

                if (unread > 0)
                {
                    string sender = settings.Values["LastMessageSender"] as string ?? "kicq";
                    string text = settings.Values["LastMessageText"] as string ?? "Новое сообщение";
                    ShowToast(sender, text, unread);
                    Debug.WriteLine("[BGTask] Showed toast, unread=" + unread);
                }
                else
                {
                    Debug.WriteLine("[BGTask] No unread messages");
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
                string xml = string.Format(
                    "<toast launch='openChat:{0}'>" +
                    "<visual><binding template='ToastGeneric'>" +
                    "<text>{1}{2}</text>" +
                    "<text>{3}</text>" +
                    "</binding></visual>" +
                    "<actions>" +
                    "<action content='Открыть' arguments='openApp' activationType='foreground'/>" +
                    "</actions>" +
                    "</toast>",
                    EscapeXml(sender),
                    EscapeXml(sender), badge,
                    EscapeXml(text.Length > 100 ? text.Substring(0, 100) + "..." : text));

                var doc = new XmlDocument();
                doc.LoadXml(xml);
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(doc));
                Debug.WriteLine("[BGTask] Toast shown for " + sender);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[BGTask] Toast error: " + ex.Message);
            }
        }



        private void OnCanceled(IBackgroundTaskInstance sender,
            BackgroundTaskCancellationReason reason)
        {
            Debug.WriteLine("[BGTask] Cancelled: " + reason);
            _deferral?.Complete();
        }

        private void ShowToast(string title, string text)
        {
            try
            {
                string xml = string.Format(
                    "<toast><visual><binding template='ToastGeneric'>" +
                    "<text>{0}</text><text>{1}</text>" +
                    "</binding></visual></toast>",
                    EscapeXml(title), EscapeXml(text));

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
            return s.Replace("&", "&amp;").Replace("<", "&lt;")
                    .Replace(">", "&gt;");
        }
    }
}