using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

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

        // In-process активация фоновой задачи (in-proc модель).
        // ControlChannelService регистрирует триггер БЕЗ TaskEntryPoint,
        // поэтому Windows вызывает вот этот override, а не отдельный
        // IBackgroundTask-класс. App всегда COM-активируем как точка входа
        // приложения — поэтому REGDB_E_CLASSNOTREG больше не возникает.
        //
        // Сами сообщения приходят по висящему read в
        // OscarProtocol.StartRawReceiveLoop; здесь только подтверждаем
        // приём и держим deferral, чтобы процесс не уснул раньше,
        // чем цикл приёма дочитает буфер.
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
            Debug.WriteLine("[App] Resuming");
            await Task.Delay(2000);
            var reconnect = ReconnectService;
            var oscar = Oscar;
            if (oscar != null && oscar.IsConnected)
            {
                Debug.WriteLine("[App] Already connected after resume");
                return;
            }
            if (reconnect != null)
                reconnect.ForceReconnect();
        }
    }
}
