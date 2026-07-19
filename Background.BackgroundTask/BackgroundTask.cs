using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

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

                // Уведомляем CCT что получили данные
                // Обрати внимание на "I" в начале имени интерфейса
                var details = taskInstance.TriggerDetails as Windows.Networking.Sockets.IControlChannelTriggerEventDetails;
                if (details != null)
                {
                    var channelTrigger = details.ControlChannelTrigger; // Получаем сам триггер
                    channelTrigger?.FlushTransport(); // Уведомляем систему
                    Debug.WriteLine("[BGTask] FlushTransport вызван через TriggerDetails");
                }

                // Даём время receive loop обработать входящие пакеты
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