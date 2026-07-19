using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Networking.Sockets;

namespace kicquwp
{
    /// <summary>
    /// Управляет ControlChannelTrigger для фоновой работы
    /// </summary>
    public class ControlChannelService
    {
        private ControlChannelTrigger _trigger;
        private BackgroundTaskRegistration _taskReg;
        private const string TaskName = "kicqPushTask";
        private const string TriggerId = "kicqChannel";
        // Точка входа фоновой задачи — класс, реализующий IBackgroundTask.
        // НЕ App (Application не реализует IBackgroundTask, и COM-активация
        // падает с 0x80040154 на Windows 10 Mobile).
        private const string TaskEntry = "Background.BackgroundTask.BackgroundTask";

        // Singleton
        private static ControlChannelService _instance;
        public static ControlChannelService Instance
        {
            get
            {
                if (_instance == null) _instance = new ControlChannelService();
                return _instance;
            }
        }

        private ControlChannelService() { }

        /// <summary>
        /// Инициализация CCT — вызывать ДО ConnectAsync сокета
        /// </summary>
        public async Task<ControlChannelTrigger> InitializeAsync()
        {
            BackgroundAccessStatus status;
            try
            {
                status = await BackgroundExecutionManager.RequestAccessAsync();
                Debug.WriteLine("[CCT] Step 1 OK, status=" + status);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CCT] STEP 1 (RequestAccessAsync) FAILED: " + ex.GetType().Name + " — " + ex.Message);
                return null;
            }

            if (status == BackgroundAccessStatus.Denied)
            {
                Debug.WriteLine("[CCT] Background access denied (status=Denied)");
                return null;
            }

            try
            {
                UnregisterTask();
                Debug.WriteLine("[CCT] Step 2 OK (UnregisterTask)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CCT] STEP 2 (UnregisterTask) FAILED: " + ex.GetType().Name + " — " + ex.Message);
            }

            try
            {
                _trigger = new ControlChannelTrigger(TriggerId, 15, ControlChannelTriggerResourceType.RequestHardwareSlot);
                Debug.WriteLine("[CCT] Step 3 OK (ControlChannelTrigger created)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CCT] STEP 3 (new ControlChannelTrigger) FAILED: " + ex.GetType().Name + " — " + ex.Message);
                return null;
            }

            try
            {
                var builder = new BackgroundTaskBuilder();
                builder.Name = TaskName;
                builder.TaskEntryPoint = TaskEntry;
                builder.SetTrigger(_trigger.PushNotificationTrigger);
                _taskReg = builder.Register();
                Debug.WriteLine("[CCT] Step 4 OK (Background task registered): " + TaskName);
                return _trigger;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CCT] STEP 4 (builder.Register) FAILED: " + ex.GetType().Name + " — " + ex.Message);
                return null;
            }
        }

        public bool WaitForPushEnabled()
        {
            if (_trigger == null) return false;
            try
            {
                var status = _trigger.WaitForPushEnabled();
                Debug.WriteLine("[CCT] WaitForPushEnabled completed with status: " + status);

                if (status != ControlChannelTriggerStatus.HardwareSlotAllocated &&
                    status != ControlChannelTriggerStatus.SoftwareSlotAllocated)
                {
                    Debug.WriteLine("[CCT] Ни аппаратный, ни программный слот не выделены — канал не активирован");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CCT] WaitForPushEnabled FAILED: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Привязываем StreamSocket к триггеру после создания но ДО подключения
        /// </summary>
        public bool AssignSocket(StreamSocket socket)
        {
            if (_trigger == null || socket == null) return false;
            try
            {
                _trigger.UsingTransport(socket);
                Debug.WriteLine("[CCT] Socket assigned to trigger");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CCT] AssignSocket error: " + ex.Message);
                return false;
            }
        }




        /// <summary>
        /// Уведомляем систему что получили данные (вызывать после чтения пакета)
        /// </summary>
        public void NotifyDataReceived()
        {
            try
            {
                _trigger?.FlushTransport();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CCT] NotifyDataReceived error: " + ex.Message);
            }
        }

        public void Cleanup()
        {
            UnregisterTask();
            try { _trigger?.Dispose(); } catch { }
            _trigger = null;
        }

        private void UnregisterTask()
        {
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == TaskName)
                {
                    task.Value.Unregister(true);
                    Debug.WriteLine("[CCT] Unregistered old task");
                }
            }
        }
    }
}