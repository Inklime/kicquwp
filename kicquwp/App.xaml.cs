using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Background.BackgroundTask;

namespace kicquwp
{
    sealed partial class App : Application
    {
        public OscarProtocol Oscar { get; set; }
        public ReconnectService ReconnectService { get; set; }
        public byte ContactAlpha { get; set; } = 255;

        // ExtendedExecution УБРАН ПОЛНОСТЬЮ.
        // Причина: new ExtendedExecutionSession() кидал 0x8007139F
        // ("The group or resource is not in the correct state") при
        // сворачивании, а сама механика устарела и конфликтует с
        // ControlChannelTrigger. Теперь фоновое удержание/пробуждение
        // обеспечивает только CCT — это правильный инструмент для сокета.

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

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
                SoundService.Init(Window.Current.Dispatcher);
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                    rootFrame.Navigate(typeof(LoginPage), e.Arguments);
                Window.Current.Activate();
            }
        }

        // In-process активация фоновой задачи.
        // ControlChannelService регистрирует триггер с TaskEntryPoint =
        // "kicquwp.BackgroundTask" (IBackgroundTask-класс). Когда система
        // будит приложение по CCT, BackgroundTask.Run вызывает
        // NotifyDataReceived → FlushTransport и ждёт 3 секунды, давая
        // сырому движку приёма дочитать буферизированные данные.
        //
        // Сообщения приходят по висящему read в
        // OscarProtocol.StartRawReceiveLoop; здесь ничего дополнительно
        // делать не нужно.
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

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                // Соединение в фоне держит ControlChannelTrigger.
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void OnResuming(object sender, object e)
        {
            System.Diagnostics.Debug.WriteLine("[App] Вернулись из фона. Проверяем сокет...");
            var oscar = Oscar;
            // Проверяем флаг подключения на твоем объекте (имя свойства IsConnected может отличаться)
            if (oscar == null || !oscar.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[App] Соединение потеряно. Перезапуск...");

                // 1. Полностью сносим старый поломанный триггер и задачу
                ControlChannelService.Instance.Cleanup();

                // 2. Вызываем твой стандартный метод подключения через твою переменную
                await ReconnectService.ForceReconnectAsync();
            }
        }

    }
}