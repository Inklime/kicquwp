using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace kicquwp
{
    /// <summary>
    /// Фоновая задача для ControlChannelTrigger.
    ///
    /// ВАЖНО: эта задача НЕ разбирает сообщения и НЕ показывает тост сама.
    /// Сообщения приходят по висящему read из сырого движка приёма
    /// (OscarProtocol.StartRawReceiveLoop): на нём всегда «висит» LoadAsync,
    /// и когда система будит свёрнутое приложение по приходу данных на сокет,
    /// именно этот LoadAsync завершается → HandleIncomingIcbm →
    /// NotificationService.OnMessageReceived → тост + звук.
    ///
    /// Здесь задача выполняет только две роли:
    ///   1) подтвердить системе приём триггера (FlushTransport);
    ///   2) удерживать deferral, чтобы процесс не ушёл обратно в suspend
    ///      раньше, чем цикл приёма дочитает буферизованные данные.
    ///
    /// Раньше здесь был показ тоста из LocalSettings — это давало
    /// дублирующее/устаревшее уведомление (там лежало предыдущее сообщение),
    /// поэтому убрано.
    /// </summary>
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

                // Подтверждаем системе приём триггера.
                // (Внутри null-safe: если триггер уже dispose'нут — noop.)
                ControlChannelService.Instance.NotifyDataReceived();

                // Даём сыром циклу приёма время дочитать буферизованные
                // данные и отрисовать уведомление. Без этой паузы процесс
                // может заснуть обратно до завершения HandleIncomingIcbm.
                await Task.Delay(3000);
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

        private void OnCanceled(IBackgroundTaskInstance sender,
            BackgroundTaskCancellationReason reason)
        {
            Debug.WriteLine("[BGTask] Cancelled: " + reason);
            _deferral?.Complete();
        }
    }
}
