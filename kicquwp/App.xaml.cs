using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media;
using Windows.UI.Core;
using Windows.Storage;
using Background.BackgroundTask;

namespace kicquwp
{
    sealed partial class App : Application
    {
        public OscarProtocol Oscar { get; set; }
        public ReconnectService ReconnectService { get; set; }
        public byte ContactAlpha { get; set; } = 255;
        public static ImageBrush GlobalChatBackground { get; set; }

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += OnResuming;
            this.UnhandledException += (s, e) =>
            {
                Debug.WriteLine("[CRASH] " + e.Message);
                Debug.WriteLine("[CRASH] " + e.Exception?.GetType().FullName);
                Debug.WriteLine("[CRASH] " + e.Exception?.StackTrace);
                if (e.Exception?.InnerException != null)
                    Debug.WriteLine("[CRASH INNER] " + e.Exception.InnerException.Message);
                e.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Debug.WriteLine("[TASK CRASH] " + e.Exception?.Message);
                Debug.WriteLine("[TASK CRASH] " + e.Exception?.StackTrace);
                e.SetObserved();
            };
        }


        private DispatcherTimer _connectingTimer;
        private int _connectingFrame = 0;
        private static readonly string[] ConnectingFrames = new string[]
        {
    "/Assets/statuses/connecting_1.png",
    "/Assets/statuses/connecting_2.png",
    "/Assets/statuses/connecting_3.png",
    "/Assets/statuses/connecting_4.png",
    "/Assets/statuses/connecting_5.png",
    "/Assets/statuses/connecting_6.png",
    "/Assets/statuses/connecting_7.png",
    "/Assets/statuses/connecting_8.png"
        };

        public void StartConnectingAnimation()
        {
            if (_connectingTimer != null) return;
            _connectingTimer = new DispatcherTimer();
            _connectingTimer.Interval = TimeSpan.FromMilliseconds(150);
            _connectingTimer.Tick += (s, e) =>
            {
                _connectingFrame = (_connectingFrame + 1) % ConnectingFrames.Length;
                if (ConnectingAnimationFrame != null)
                    ConnectingAnimationFrame(ConnectingFrames[_connectingFrame]);
            };
            _connectingTimer.Start();
        }

        public void StopConnectingAnimation()
        {
            if (_connectingTimer != null)
            {
                _connectingTimer.Stop();
                _connectingTimer = null;
                _connectingFrame = 0;
            }
        }

        // Событие — MainPage подписывается
        public event Action<string> ConnectingAnimationFrame;

        public bool IsConnected { get; set; } = false;
        public event Action ConnectionStateChanged;

        public void NotifyConnectionLost()
        {
            IsConnected = false;
            if (ConnectionStateChanged != null) ConnectionStateChanged();
        }

        public void NotifyConnected()
        {
            IsConnected = true;
            if (ConnectionStateChanged != null) ConnectionStateChanged();
        }

        // ===== ПРОВЕРКА ОБНОВЛЕНИЙ ПРИ ЗАПУСКЕ =====
        private void CheckUpdateOnStartup()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                var autoCheck = settings.Values["AutoCheckUpdate"] as bool? ?? true;
                if (!autoCheck) return;

                // Не чаще раза в 12 часов
                var lastCheckStr = settings.Values["LastUpdateCheck"] as string;
                if (!string.IsNullOrEmpty(lastCheckStr))
                {
                    if (DateTimeOffset.TryParse(lastCheckStr, out var last))
                    {
                        if ((DateTimeOffset.Now - last).TotalHours < 12)
                        {
                            Debug.WriteLine("[Update] Пропускаем, проверяли недавно");
                            return;
                        }
                    }
                }

                // Запускаем в фоне
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(4000); // даем приложению загрузиться

                        var service = new GitHubUpdateService();
                        var result = await service.CheckForUpdatesAsync(includePrerelease: false);

                        if (!string.IsNullOrEmpty(result.Error))
                        {
                            Debug.WriteLine("[Update Startup] " + result.Error);
                            return;
                        }

                        if (result.IsUpdateAvailable)
                        {
                            Debug.WriteLine($"[Update Startup] Найдено {result.LatestTag} > {result.CurrentVersion}");
                            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher
                                .RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                    try
                                    {
                                        var dlg = new ContentDialog
                                        {
                                            Title = "Доступно обновление",
                                            Content = $"Новая версия {result.LatestTag} ({result.LatestVersion}) доступна!\n" +
                                                      $"У вас: {result.CurrentVersion}\n\n" +
                                                      $"{result.ReleaseName}\n\nОткрыть GitHub?",
                                            PrimaryButtonText = "Открыть",
                                            CloseButtonText = "Позже"
                                        };
                                        var r = await dlg.ShowAsync();
                                        if (r == ContentDialogResult.Primary)
                                        {
                                            await service.OpenReleasePageAsync(result.ReleaseUrl);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine("[Update Dialog] " + ex.Message);
                                    }
                                });
                        }
                        else
                        {
                            Debug.WriteLine("[Update Startup] Обновлений нет");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Update Startup] Exception: " + ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Update Startup Init] " + ex.Message);
            }
        }

        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);
            var taskInstance = args.TaskInstance;
            var deferral = taskInstance.GetDeferral();
            Debug.WriteLine("[BGTask] OnBackgroundActivated, trigger=" + taskInstance.Task?.Name);
            try
            {
                ControlChannelService.Instance.NotifyDataReceived();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[BGTask] NotifyDataReceived error: " + ex.Message);
            }
            Task.Delay(3000).ContinueWith(_ => deferral.Complete());
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        protected override void OnActivated(IActivatedEventArgs e)
        {
            base.OnActivated(e);

            string args = null;
            if (e is ToastNotificationActivatedEventArgs toastArgs)
            {
                args = toastArgs.Argument;
            }

            if (!string.IsNullOrEmpty(args) && args.StartsWith("uin="))
            {
                HandleToastNavigation(args.Substring(4));
            }
            else
            {
                Window.Current.Activate();
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
                // Убедитесь, что SoundService существует, или закомментируйте эту строку
                // SoundService.Init(Window.Current.Dispatcher); 
            }

            // Если W10M решил передать тост через стандартный Launch
            if (!string.IsNullOrEmpty(e.Arguments) && e.Arguments.StartsWith("uin="))
            {
                HandleToastNavigation(e.Arguments.Substring(4));
                return;
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                    rootFrame.Navigate(typeof(LoginPage), e.Arguments);
                Window.Current.Activate();
            }
        }

        private void HandleToastNavigation(string uin)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                Window.Current.Content = rootFrame;
            }

            if (Oscar != null)
            {
                // ГОЯРЧИЙ СТАРТ: Протокол жив. Сразу переходим в чат.
                var contacts = Oscar.GetCachedContacts();
                Contact targetContact = contacts?.FirstOrDefault(c => c.Uin == uin)
                                      ?? new Contact { Uin = uin, Name = uin, IsTemporary = true };

                rootFrame.Navigate(typeof(ChatPage), new Tuple<Contact, OscarProtocol>(targetContact, Oscar));
            }
            else
            {
                // ХОЛОДНЫЙ СТАРТ: Приложение было закрыто. Запоминаем UIN для перехода ПОСЛЕ логина.
                Windows.Storage.ApplicationData.Current.LocalSettings.Values["PendingToastUin"] = uin;

                if (rootFrame.Content == null)
                    rootFrame.Navigate(typeof(LoginPage));
            }

            Window.Current.Activate();
        }

        private async Task OpenChatPageFromToastAsync(string uin)
        {
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                Window.Current.Content = rootFrame;
            }

            int retries = 10;
            while (Oscar == null && retries > 0)
            {
                await Task.Delay(200);
                retries--;
            }

            if (Oscar == null)
            {
                Window.Current.Activate();
                return;
            }

            var contacts = Oscar.GetCachedContacts();
            Contact targetContact = contacts?.FirstOrDefault(c => c.Uin == uin)
                                  ?? new Contact { Uin = uin, Name = uin, IsTemporary = true };

            rootFrame.Navigate(typeof(ChatPage), new Tuple<Contact, OscarProtocol>(targetContact, Oscar));
            Window.Current.Activate();
        }

        private string ExtractUinFromArgs(string args)
        {
            var parts = args.Split('&');
            foreach (var part in parts)
            {
                if (part.StartsWith("uin="))
                    return part.Substring(4);
            }
            return null;
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try { }
            finally { deferral.Complete(); }
        }

        private async void OnResuming(object sender, object e)
        {
            Debug.WriteLine("[App] Вернулись из фона. Проверяем сокет...");
            var oscar = Oscar;
            if (oscar == null || !oscar.IsConnected)
            {
                Debug.WriteLine("[App] Соединение потеряно. Перезапуск...");
                ControlChannelService.Instance.Cleanup();
                await ReconnectService.ForceReconnectAsync();
            }
        }
    }
}
