using Windows.UI.Popups;
using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Background.BackgroundTask;
using System.Threading.Tasks;

namespace kicquwp
{
    public sealed partial class SettingsPage : Page
    {
        // [ИСПРАВЛЕНИЕ 1]: Добавляем переменную для хранения активного соединения
        private OscarProtocol _oscar;

        private void PickBackground_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            Windows.Storage.ApplicationData.Current.LocalSettings
                .Values["PickerTarget"] = "background";
            picker.PickSingleFileAsync();
        }

        private void PickChatBackground_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            Windows.Storage.ApplicationData.Current.LocalSettings
                .Values["PickerTarget"] = "chat_background";
            picker.PickSingleFileAsync();
        }

        public SettingsPage()
        {
            this.InitializeComponent();
            //HardwareButtons.BackPressed += HardwareButtons_BackPressed;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // [ИСПРАВЛЕНИЕ 2]: Подхватываем переданный рабочий протокол
            if (e.Parameter is OscarProtocol activeProtocol)
            {
                _oscar = activeProtocol;
                Debug.WriteLine("[Settings] Активный протокол успешно получен из параметров.");
            }
            else
            {
                // Запасной вариант (fallback), если параметр забыли передать
                _oscar = OscarProtocol.Instance;
                Debug.WriteLine("[Settings] Внимание: протокол не передан. Используем Instance.");
            }

            LoadSettings();

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string bgPath = settings.Values["BackgroundPath"] as string;
            if (!string.IsNullOrEmpty(bgPath))
                UpdatePreview(bgPath);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            //HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            base.OnNavigatedFrom(e);
        }

        private void LoadSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            // Загружаем показ групп
            object showGroups = settings.Values["ShowGroups"];
            ShowGroupsToggle.IsOn = showGroups != null && (bool)showGroups;

            object hideOffline = settings.Values["HideOffline"];
            HideOfflineToggle.IsOn = hideOffline != null && (bool)hideOffline;

            object typingEnabled = settings.Values["TypingNotifications"];
            // По умолчанию включено
            TypingNotificationsToggle.IsOn = typingEnabled == null || (bool)typingEnabled;

            var isW10 = IsW10M();

            if (!isW10)
                BackgroundModeBorder.Visibility = Visibility.Collapsed;

            // работа в фоне
            object bgMode = settings.Values["BackgroundMode"];
            BackgroundModeToggle.IsOn = bgMode == null || (bool)bgMode;

            // Загружаем прозрачность
            object opacity = settings.Values["BackgroundOpacity"];
            double opacityVal = opacity != null ? (double)opacity : 100.0;
            BackgroundOpacitySlider.Value = opacityVal;
            OpacityValueText.Text = ((int)opacityVal) + "%";

            // Показываем превью фона
            string bgPath = settings.Values["BackgroundPath"] as string;
            UpdatePreview(bgPath);
            object contactOpacity = settings.Values["ContactOpacity"];

            double contactOpacityVal = contactOpacity != null ? (double)contactOpacity : 100.0;
            ContactOpacitySlider.Value = contactOpacityVal;
            ContactOpacityText.Text = ((int)contactOpacityVal) + "%";
        }

        public static bool IsW10M()
        {
            try
            {
                var type = Type.GetType("Windows.System.Profile.AnalyticsInfo, Windows, ContentType=WindowsRuntime");
                return type != null;
            }
            catch
            {
                return false;
            }
        }

        private async void UpdatePreview(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                BackgroundPreview.Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 13, 17, 23));
                BackgroundPreviewText.Visibility = Visibility.Visible;
                BackgroundPreviewText.Text = "Фон не выбран";
                return;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using (var stream = await file.OpenReadAsync())
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);
                    BackgroundPreview.Background = new ImageBrush
                    {
                        ImageSource = bitmap,
                        Stretch = Stretch.UniformToFill
                    };
                    BackgroundPreviewText.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                BackgroundPreview.Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 13, 17, 23));
                BackgroundPreviewText.Visibility = Visibility.Visible;
                BackgroundPreviewText.Text = "Фото недоступно";
            }
        }

        private void ContactOpacitySlider_ValueChanged(object sender,
    Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (ContactOpacityText == null) return;
            int val = (int)e.NewValue;
            ContactOpacityText.Text = val + "%";
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["ContactOpacity"] = (double)val;
        }

        private async void ClearChatBackground_Click(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["ChatBackgroundPath"] = null;
            try
            {
                var file = await ApplicationData.Current.LocalFolder
                                     .GetFileAsync("chat_background.jpg");
                await file.DeleteAsync();
            }
            catch { }
        }

        private void TypingNotificationsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["TypingNotifications"] = TypingNotificationsToggle.IsOn;
            Debug.WriteLine("[Settings] TypingNotifications=" + TypingNotificationsToggle.IsOn);
        }

        // ── Показ групп ─────────────────────────────────────────────
        private void ShowGroupsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["ShowGroups"] = ShowGroupsToggle.IsOn;
            Debug.WriteLine("[Settings] ShowGroups=" + ShowGroupsToggle.IsOn);
        }

        private void BackgroundModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["BackgroundMode"] = BackgroundModeToggle.IsOn;

            if (!BackgroundModeToggle.IsOn)
                ControlChannelService.Instance.Cleanup();

            Debug.WriteLine("[Settings] BackgroundMode=" + BackgroundModeToggle.IsOn);
        }

        // ── Очистка фона ────────────────────────────────────────────
        private async void ClearBackground_Click(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["BackgroundPath"] = null;
            UpdatePreview(null);

            // Удаляем файл
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                var file = await localFolder.GetFileAsync("background.jpg");
                await file.DeleteAsync();
            }
            catch { }

            Debug.WriteLine("[Settings] Background cleared");
        }

        // ── Прозрачность ────────────────────────────────────────────
        private void OpacitySlider_ValueChanged(object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (OpacityValueText == null) return;

            int val = (int)e.NewValue;
            OpacityValueText.Text = val + "%";

            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["BackgroundOpacity"] = (double)val;
        }

        private bool _isDeleteInProgress = false;

        private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_isDeleteInProgress) return;

            // [ИСПРАВЛЕНИЕ 3]: Используем рабочую переменную _oscar, а не статичный Instance
            if (_oscar == null || !_oscar.IsConnected)
            {
                await new ContentDialog { Title = "Ошибка", Content = "Нет подключения к серверу. Переподключитесь.", CloseButtonText = "ОК" }.ShowAsync();
                return;
            }

            var confirm = new ContentDialog
            {
                Title = "Удаление аккаунта",
                Content = "Вы уверены? UIN будет полностью удален с сервера. Действие необратимо.",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            var pwdBox = new PasswordBox { PlaceholderText = "Пароль от UIN" };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Введите пароль:", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            panel.Children.Add(pwdBox);
            panel.Children.Add(new TextBlock { Name = "ErrorText", Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red), Margin = new Thickness(0, 8, 0, 0), Visibility = Windows.UI.Xaml.Visibility.Collapsed });

            string capturedPwd = null;

            var pwdDlg = new ContentDialog
            {
                Title = "Подтверждение",
                Content = panel,
                PrimaryButtonText = "Удалить аккаунт",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close
            };

            pwdDlg.PrimaryButtonClick += (d, args) =>
            {
                try
                {
                    capturedPwd = pwdBox.Password;
                    if (string.IsNullOrWhiteSpace(capturedPwd))
                    {
                        args.Cancel = true;
                        var err = (panel.Children[2] as TextBlock);
                        if (err != null) { err.Text = "Пароль не может быть пустым."; err.Visibility = Windows.UI.Xaml.Visibility.Visible; }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Password read failed: " + ex.Message);
                    capturedPwd = null;
                    args.Cancel = true;
                }
            };

            var pwdResult = await pwdDlg.ShowAsync();
            if (pwdResult != ContentDialogResult.Primary) return;

            string pwd = capturedPwd;
            if (string.IsNullOrWhiteSpace(pwd))
            {
                await new ContentDialog { Title = "Ошибка", Content = "Пароль пустой", CloseButtonText = "ОК" }.ShowAsync();
                return;
            }

            _isDeleteInProgress = true;
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                var local = Windows.Storage.ApplicationData.Current.LocalSettings;
                local.Values["AutoLogin"] = false;
                local.Values["IsDeletingAccount"] = true;
            }
            catch { }

            Debug.WriteLine("[UI] Calling DeleteAccountAsync...");
            try
            {
                // [ИСПРАВЛЕНИЕ 4]: Вызываем удаление у живого протокола
                var protocol = _oscar;
                if (protocol == null) throw new Exception("Протокол равен null. Перелогиньтесь.");

                bool success = await protocol.DeleteAccountAsync(pwd);

                await Task.Delay(250);

                if (success)
                {
                    await new ContentDialog { Title = "Удалено", Content = "Аккаунт успешно удален с сервера.", CloseButtonText = "ОК" }.ShowAsync();

                    try { await protocol.DisconnectAfterDeleteAsync(); }
                    catch { try { await protocol.DisconnectAsync(); } catch { } }

                    try
                    {
                        var s = Windows.Storage.ApplicationData.Current.LocalSettings;
                        s.Values.Remove("SavedUin");
                        s.Values.Remove("SavedPassword");
                        s.Values.Remove("AutoLogin");
                        s.Values.Remove("IsDeletingAccount");
                    }
                    catch { }

                    // Сбрасываем статику тоже
                    OscarProtocol.Instance = null;

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        try { Frame.Navigate(typeof(LoginPage)); }
                        catch (Exception ex) { Debug.WriteLine("Navigate failed: " + ex.Message); }
                    });
                }
                else
                {
                    await new ContentDialog { Title = "Не удалось", Content = "Сервер вернул ошибку. Проверьте пароль.", CloseButtonText = "ОК" }.ShowAsync();
                    try { Windows.Storage.ApplicationData.Current.LocalSettings.Values["IsDeletingAccount"] = false; } catch { }
                }
            }
            catch (Exception ex)
            {
                await Task.Delay(250);
                Debug.WriteLine("[UI Delete ERROR] " + ex);
                try { await new ContentDialog { Title = "Ошибка", Content = "Ошибка удаления: " + ex.Message, CloseButtonText = "ОК" }.ShowAsync(); } catch { }
            }
            finally
            {
                _isDeleteInProgress = false;
                if (btn != null) try { btn.IsEnabled = true; } catch { }
                try { Windows.Storage.ApplicationData.Current.LocalSettings.Values["IsDeletingAccount"] = false; } catch { }
            }
        }

        private void HideOfflineToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["HideOffline"] = HideOfflineToggle.IsOn;

            Debug.WriteLine("[Settings] HideOffline=" + HideOfflineToggle.IsOn);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}