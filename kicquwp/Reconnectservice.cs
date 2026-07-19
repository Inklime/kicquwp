using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace kicquwp
{
    /// <summary>
    /// Следит за соединением и переподключается при обрыве
    /// </summary>
    public class ReconnectService
    {
        private OscarProtocol _oscar;
        private string _uin;
        private string _password;
        private uint _statusCode;
        private CoreDispatcher _dispatcher;
        private CancellationTokenSource _cts;
        private bool _running = false;
        public event Action OnDisconnected;
        public event Action OnReconnecting;
        private volatile bool _kicked = false;
        public event Action<string> KickedOut;
        private static ReconnectService _instance;
        private Windows.Networking.Connectivity.NetworkStatusChangedEventHandler _networkHandler;

        // Событие — подписываемся в App чтобы обновить UI после реконнекта
        public event Action<OscarProtocol> Reconnected;
        public event Action Disconnected;

        public static ReconnectService Instance
        {
            get { return _instance; }
            set { _instance = value; }
        }

        public ReconnectService(string uin, string password, uint statusCode, CoreDispatcher dispatcher)
        {
            _uin = uin;
            _password = password;
            _statusCode = statusCode;
            _dispatcher = dispatcher;
        }

        public void Start(OscarProtocol oscar)
        {
            _oscar = oscar;
            _running = true;
            _cts = new CancellationTokenSource();

            // Подписываемся на событие обрыва
            _oscar.ConnectionLost += OnConnectionLostEvent;

            // Сетевые изменения
            _networkHandler = new Windows.Networking.Connectivity
                .NetworkStatusChangedEventHandler(OnNetworkStatusChanged);
            Windows.Networking.Connectivity.NetworkInformation
                .NetworkStatusChanged += _networkHandler;

            Task.Run(() => MonitorLoopAsync(_cts.Token));
        }

        private void OnConnectionLostEvent()
        {
            Debug.WriteLine("[Reconnect] ConnectionLost event received");
            // MonitorLoopAsync сам обнаружит обрыв через исключение в ReceiveServerSnacsAsync
            // Ничего не делаем — цикл уже разбужен через отмену токена
        }



        public void Stop()
        {
            _running = false;
            if (_networkHandler != null)
            {
                Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged -= _networkHandler;
                _networkHandler = null;
            }
            if (_cts != null)
            {
                _cts.Cancel();
                _cts = null;
            }
        }

        // Добавьте поле в класс ReconnectService
        private bool _isReconnectingNow = false;

        private async void OnNetworkStatusChanged(object sender)
        {
            var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
            if (profile == null) return;

            var level = profile.GetNetworkConnectivityLevel();
            if (level == Windows.Networking.Connectivity.NetworkConnectivityLevel.InternetAccess)
            {
                Debug.WriteLine("[Reconnect] Network restored — forcing reconnect");

                // ЗАЩИТА ОТ ДВОЙНОГО ЗАПУСКА
                if (_isReconnectingNow)
                {
                    Debug.WriteLine("[Reconnect] Reconnect already in progress, skipping...");
                    return;
                }
                _isReconnectingNow = true;

                try
                {
                    if (_cts != null)
                    {
                        _cts.Cancel();
                        _cts = new CancellationTokenSource();
                    }

                    if (_oscar != null)
                    {
                        try { await _oscar.DisconnectAsync(); } catch { }
                    }

                    // Даем время на корректное завершение всех старых тасок и CCT
                    await Task.Delay(3000);

                    Task.Run(() => MonitorLoopAsync(_cts.Token));
                }
                finally
                {
                    _isReconnectingNow = false;
                }
            }
        }

        private void OnKickedByServer(string reason)
        {
            _kicked = true;
            KickedOut?.Invoke(reason);
        }

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            Debug.WriteLine("[Reconnect] Monitor started");

            while (_running && !token.IsCancellationRequested)
            {
                try
                {
                    await _oscar.ReceiveServerSnacsAsync();
                    break; // нормальный выход
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Reconnect] Disconnected: " + ex.Message);
                }

                if (!_running || token.IsCancellationRequested) break;

                // Проверяем есть ли интернет перед реконнектом
                var profile = Windows.Networking.Connectivity.NetworkInformation
                    .GetInternetConnectionProfile();
                if (profile == null)
                {
                    Debug.WriteLine("[Reconnect] No internet, waiting...");
                    await Task.Delay(5000, token);
                    continue;
                }

                if (_dispatcher != null)
                    await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (OnDisconnected != null) OnDisconnected();
                    });

                int attempt = 0;
                while (_running && !token.IsCancellationRequested)
                {
                    attempt++;
                    int delay = Math.Min(30000, attempt * 5000);
                    Debug.WriteLine("[Reconnect] Retry in " + delay + "ms");

                    await Task.Delay(delay, token);
                    if (token.IsCancellationRequested) break;

                    bool ok = await TryReconnectAsync(token);
                    if (ok) break;
                }
            }
            Debug.WriteLine("[Reconnect] Monitor stopped");
        }

        public async Task ForceReconnectAsync()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    bool ok = await _oscar.AuthenticateAndInitializeAsync(_uin, _statusCode);
                    if (ok) return;
                }
                catch { }
                await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)));
            }
            // Только тут — на LoginPage
        }

        private async Task<bool> TryReconnectAsync(CancellationToken token)
        {
            try
            {
                // На всякий случай убеждаемся, что старые сокеты точно закрыты
                if (_oscar != null)
                {
                    try { await _oscar.DisconnectAsync(); } catch { }
                }

                // Переиспользуем СУЩЕСТВУЮЩИЙ экземпляр _oscar вместо new OscarProtocol(...)
                bool auth = await _oscar.AuthenticateAsync(_statusCode);
                if (!auth) return false;

                await _oscar.InitializeOscarSessionAsync(_statusCode);


                // Уведомляем UI о том, что связь восстановлена
                if (_dispatcher != null)
                {
                    await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Reconnected?.Invoke(_oscar);
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Reconnect] Failed: " + ex.Message);
                return false;
            }
        }

        private void OnOscarConnectionLost()
        {
            Debug.WriteLine("[Reconnect] Oscar reported connection lost");
            // MonitorLoopAsync сам запустит реконнект через исключение в ReceiveServerSnacsAsync
        }

        public void ForceReconnect()
        {
            Debug.WriteLine("[Reconnect] ForceReconnect called");

            // Проверяем соединение прежде чем дисконнектиться
            if (_oscar != null && _oscar.IsConnected)
            {
                Debug.WriteLine("[Reconnect] Already connected, skipping");
                return;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts = new CancellationTokenSource();
            }

            var oscar = _oscar;
            Task.Run(async () =>
            {
                if (oscar != null)
                {
                    try { await oscar.DisconnectAsync(); } catch { }
                }
                await Task.Delay(1000);
                await MonitorLoopAsync(_cts.Token);
            });
        }

        private async void OnIncomingMessage(string senderUin, string text)
        {
            //await NotificationService.Instance.OnMessageReceived(
                //senderUin, senderUin, text, _dispatcher);
        }
    }
}