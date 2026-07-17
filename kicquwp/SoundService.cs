using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Core;

namespace kicquwp
{
    public static class SoundService
    {
        private static CoreDispatcher _dispatcher;
        private static MediaPlayer _player;

        public static void Init(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            try
            {
                _player = new MediaPlayer();
                _player.AutoPlay = false;
                _player.Volume = 1.0;
                Debug.WriteLine("[Sound] MediaPlayer initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Sound] Init error: " + ex.Message);
            }
        }

        public static void SetPlayer(Windows.UI.Xaml.Controls.MediaElement player,
            CoreDispatcher dispatcher)
        {
            if (dispatcher != null) _dispatcher = dispatcher;
        }

        public static async void PlayMessage() => await Play("ms-appx:///Assets/Sounds/message.wav");
        public static async void PlayOnline() => await Play("ms-appx:///Assets/Sounds/online.wav");
        public static async void PlayOffline() => await Play("ms-appx:///Assets/Sounds/offline.wav");
        public static async void PlayError() => await Play("ms-appx:///Assets/Sounds/error.wav");
        public static async void PlayNotification() => await Play("ms-appx:///Assets/Sounds/notification.wav");

        private static async Task Play(string uri)
        {
            try
            {
                if (_player == null)
                {
                    Debug.WriteLine("[Sound] Player not initialized");
                    if (_dispatcher != null)
                        await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            Init(_dispatcher));
                    if (_player == null) return;
                }

                // MediaPlayer в UWP не требует UI потока
                _player.Source = MediaSource.CreateFromUri(new Uri(uri));
                _player.Play();
                Debug.WriteLine("[Sound] Playing: " + uri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Sound] Error: " + ex.Message);
            }
        }
    }
}