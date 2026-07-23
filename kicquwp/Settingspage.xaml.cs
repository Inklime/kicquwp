using Windows.UI.Popups;
using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;

namespace kicquwp
{
    public sealed partial class SettingsPage : Page
    {
        private OscarProtocol _oscar;
        private bool _isLoaded = false;

        // ===== ФОН СПИСКА =====
        private async void PickBackground_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                    StorageFile copiedFile = await file.CopyAsync(localFolder, "background.jpg", NameCollisionOption.ReplaceExisting);

                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values["BackgroundPath"] = copiedFile.Path;
                    UpdatePreview(copiedFile.Path);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine("[PickBackground] Access denied: " + ex.Message);
                await new ContentDialog { Title = "Ошибка доступа", Content = "Нет доступа к файлу: " + ex.Message, CloseButtonText = "ОК" }.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PickBackground] " + ex);
            }
        }

        private async void PickChatBackground_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                    StorageFile copiedFile = await file.CopyAsync(localFolder, "chat_background.jpg", NameCollisionOption.ReplaceExisting);
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values["ChatBackgroundPath"] = copiedFile.Path;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine("[PickChatBackground] Access denied: " + ex.Message);
                await new ContentDialog { Title = "Ошибка доступа", Content = "Нет доступа к файлу: " + ex.Message, CloseButtonText = "ОК" }.ShowAsync();
            }
            catch (Exception ex) { Debug.WriteLine("[PickChatBackground] " + ex); }
        }

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isLoaded = false;
            base.OnNavigatedTo(e);

            if (e.Parameter is OscarProtocol activeProtocol)
            {
                _oscar = activeProtocol;
                Debug.WriteLine("[Settings] Активный протокол успешно получен из параметров.");
            }
            else
            {
                _oscar = OscarProtocol.Instance;
                Debug.WriteLine("[Settings] Внимание: протокол не передан. Используем Instance.");
            }

            LoadSettings();

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string bgPath = settings.Values["BackgroundPath"] as string;
            if (!string.IsNullOrEmpty(bgPath))
                UpdatePreview(bgPath);

            // Обновления — показываем текущую версию
            UpdateCurrentVersionDisplay();

            _isLoaded = true;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        private void LoadSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            object showGroups = settings.Values["ShowGroups"];
            ShowGroupsToggle.IsOn = showGroups != null && (bool)showGroups;

            object hideOffline = settings.Values["HideOffline"];
            HideOfflineToggle.IsOn = hideOffline != null && (bool)hideOffline;

            object typingEnabled = settings.Values["TypingNotifications"];
            TypingNotificationsToggle.IsOn = typingEnabled == null || (bool)typingEnabled;

            var isW10 = IsW10M();
            if (!isW10)
                BackgroundModeBorder.Visibility = Visibility.Collapsed;

            object bgMode = settings.Values["BackgroundMode"];
            BackgroundModeToggle.IsOn = bgMode == null || (bool)bgMode;

            object opacity = settings.Values["BackgroundOpacity"];
            double opacityVal = opacity != null ? (double)opacity : 100.0;
            BackgroundOpacitySlider.Value = opacityVal;
            OpacityValueText.Text = ((int)opacityVal) + "%";

            string bgPath = settings.Values["BackgroundPath"] as string;
            UpdatePreview(bgPath);

            object contactOpacity = settings.Values["ContactOpacity"];
            double contactOpacityVal = contactOpacity != null ? (double)contactOpacity : 100.0;
            ContactOpacitySlider.Value = contactOpacityVal;
            ContactOpacityText.Text = ((int)contactOpacityVal) + "%";

            // Обновления
            object autoCheck = settings.Values["AutoCheckUpdate"];
            AutoCheckUpdateToggle.IsOn = autoCheck == null || (bool)autoCheck;

            object lastCheck = settings.Values["LastUpdateCheck"];
            if (lastCheck != null)
            {
                try
                {
                    var dt = DateTimeOffset.Parse(lastCheck.ToString());
                    LastCheckText.Text = "Проверено: " + dt.ToString("dd.MM.yyyy HH:mm");
                }
                catch { LastCheckText.Text = "Проверено: " + lastCheck.ToString(); }
            }
            else
            {
                LastCheckText.Text = "Еще не проверялось";
            }
        }

        public static bool IsW10M()
        {
            try
            {
                var type = Type.GetType("Windows.System.Profile.AnalyticsInfo, Windows, ContentType=WindowsRuntime");
                return type != null;
            }
            catch { return false; }
        }

        private void UpdatePreview(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                BackgroundPreview.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 13, 17, 23));
                BackgroundPreviewText.Visibility = Visibility.Visible;
                BackgroundPreviewText.Text = "Фон не выбран";
                return;
            }
            try
            {
                var bitmap = new BitmapImage();
                BackgroundPreview.Opacity = 0;
                bitmap.ImageOpened += (s, e) => { BackgroundPreview.Opacity = 1; };
                bitmap.UriSource = new Uri("ms-appdata:///local/background.jpg");
                BackgroundPreview.Background = new ImageBrush { ImageSource = bitmap, Stretch = Stretch.UniformToFill };
                BackgroundPreviewText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                BackgroundPreview.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 13, 17, 23));
                BackgroundPreviewText.Visibility = Visibility.Visible;
                BackgroundPreviewText.Text = "Фото недоступно";
            }
        }

        private void ContactOpacitySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isLoaded || ContactOpacityText == null) return;
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
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync("chat_background.jpg");
                await file.DeleteAsync();
            }
            catch { }
        }

        private void TypingNotificationsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["TypingNotifications"] = TypingNotificationsToggle.IsOn;
        }

        private void ShowGroupsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["ShowGroups"] = ShowGroupsToggle.IsOn;
        }

        private void BackgroundModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["BackgroundMode"] = BackgroundModeToggle.IsOn;
            if (!BackgroundModeToggle.IsOn)
                ControlChannelService.Instance.Cleanup();
        }

        private async void ClearBackground_Click(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["BackgroundPath"] = null;
            UpdatePreview(null);
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                var file = await localFolder.GetFileAsync("background.jpg");
                await file.DeleteAsync();
            }
            catch { }
        }

        private void OpacitySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isLoaded || OpacityValueText == null) return;
            int val = (int)e.NewValue;
            OpacityValueText.Text = val + "%";
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["BackgroundOpacity"] = (double)val;
        }

        // ============================================================
        // ОБНОВЛЕНИЯ
        // ============================================================

        private void UpdateCurrentVersionDisplay()
        {
            try
            {
                CurrentVersionText.Text = GitHubUpdateService.GetCurrentVersionString();
            }
            catch { CurrentVersionText.Text = "?.?.?.?"; }
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync(silent: false);
        }

        private async void OpenReleases_Click(object sender, RoutedEventArgs e)
        {
            var service = new GitHubUpdateService();
            await service.OpenReleasePageAsync();
        }

        private void AutoCheckUpdateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["AutoCheckUpdate"] = AutoCheckUpdateToggle.IsOn;
        }

        public async Task CheckForUpdatesAsync(bool silent = false)
        {
            if (CheckUpdateButton == null) return;

            try
            {
                CheckUpdateButton.IsEnabled = false;
                UpdateProgressRing.IsActive = true;
                UpdateStatusText.Text = "Проверка...";
                ReleaseNotesBorder.Visibility = Visibility.Collapsed;
                OpenReleasesButton.Visibility = Visibility.Collapsed;

                var service = new GitHubUpdateService();
                var result = await service.CheckForUpdatesAsync(includePrerelease: false);

                if (!string.IsNullOrEmpty(result.Error))
                {
                    UpdateStatusText.Text = "Ошибка";
                    LastCheckText.Text = result.Error;
                    if (!silent)
                    {
                        await new ContentDialog
                        {
                            Title = "Ошибка проверки",
                            Content = result.Error + "\n\nПопробуйте позже. Репозиторий: github.com/Inklime/kicquwp",
                            CloseButtonText = "ОК"
                        }.ShowAsync();
                    }
                    return;
                }

                LastCheckText.Text = $"Проверено: {DateTime.Now:dd.MM.yyyy HH:mm} • Последняя: {result.LatestTag}";

                if (result.IsUpdateAvailable)
                {
                    UpdateStatusText.Text = "Есть обновление!";
                    LatestVersionText.Text = $"{result.ReleaseName} ({result.LatestTag}) доступно!";
                    ReleaseNotesText.Text = string.IsNullOrWhiteSpace(result.ReleaseNotes) ? "Без описания" : result.ReleaseNotes.Length > 500 ? result.ReleaseNotes.Substring(0, 500) + "..." : result.ReleaseNotes;
                    ReleaseNotesBorder.Visibility = Visibility.Visible;
                    OpenReleasesButton.Visibility = Visibility.Visible;

                    if (!silent)
                    {
                        var dlg = new ContentDialog
                        {
                            Title = "Доступно обновление",
                            Content = $"Текущая: {result.CurrentVersion}\nНовая: {result.LatestVersion} ({result.LatestTag})\n\n{result.ReleaseName}\n\nОткрыть страницу релиза?",
                            PrimaryButtonText = "Открыть GitHub",
                            CloseButtonText = "Позже"
                        };
                        var r = await dlg.ShowAsync();
                        if (r == ContentDialogResult.Primary)
                        {
                            await service.OpenReleasePageAsync(result.ReleaseUrl);
                        }
                    }
                }
                else
                {
                    UpdateStatusText.Text = "У вас последняя версия";
                    if (!silent)
                    {
                        await new ContentDialog
                        {
                            Title = "Обновлений нет",
                            Content = $"У вас последняя версия: {result.CurrentVersion}\nПоследний релиз на GitHub: {result.LatestTag}",
                            CloseButtonText = "ОК"
                        }.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Update UI] " + ex);
                UpdateStatusText.Text = "Ошибка";
                if (!silent)
                    await new ContentDialog { Title = "Ошибка", Content = ex.Message, CloseButtonText = "ОК" }.ShowAsync();
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
                UpdateProgressRing.IsActive = false;
            }
        }

        // ============================================================
        // УДАЛЕНИЕ АККАУНТА — V3 FINAL без AV на PasswordBox
        // ============================================================

        private bool _isDeleteInProgress = false;

        private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_isDeleteInProgress) return;

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
            var errText = new TextBlock { Foreground = new SolidColorBrush(Windows.UI.Colors.Red), Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
            panel.Children.Add(errText);

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
                        errText.Text = "Пароль не может быть пустым.";
                        errText.Visibility = Visibility.Visible;
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
            if (!_isLoaded) return;
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["HideOffline"] = HideOfflineToggle.IsOn;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}
