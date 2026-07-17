using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace kicquwp
{
    public sealed partial class MainPage : Page
    {
        private OscarProtocol _oscarProtocol;
        private bool _showGroups = false;
        private bool _hideOffline = false;
        private Contact _holdContact;
        private uint _currentStatus = 0x10010000;
        private bool _initialized = false;
        private bool _statusPanelVisible = false;

        public ObservableCollection<Contact> Contacts { get; set; }

        // ─────────────────────────────────────────────────────────────────────
        // КОНСТРУКТОР
        // ─────────────────────────────────────────────────────────────────────
        public MainPage()
        {
            this.InitializeComponent();
            this.DataContext = this;

            Contacts = new ObservableCollection<Contact>();
            ContactsListView.ItemsSource = Contacts;

            // Загружаем сохранённые контакты асинхронно
            _ = InitAsync();
        }

        private async Task InitAsync()
        {
            try
            {
                string uin = await LoadLastUsedUinAsync();
                if (string.IsNullOrEmpty(uin)) return;

                var saved = await ContactStorage.LoadContactsFromFileAsync(uin);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    foreach (var contact in saved)
                        Contacts.Add(contact);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[InitAsync ERROR] " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ХРАНЕНИЕ UIN
        // ─────────────────────────────────────────────────────────────────────
        private void SaveLastUin(string uin)
        {
            ApplicationData.Current.LocalSettings.Values["LastUin"] = uin;
        }

        public static async Task<string> LoadLastUsedUinAsync()
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder
                    .GetFileAsync("last_uin.txt");
                return await FileIO.ReadTextAsync(file);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async Task SaveLastUsedUinAsync(string uin)
        {
            StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                "last_uin.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, uin);
        }

        // ─────────────────────────────────────────────────────────────────────
        // НАВИГАЦИЯ
        // ─────────────────────────────────────────────────────────────────────
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Первичная инициализация — только один раз
            if (!_initialized)
            {
                var oscarProtocol = e.Parameter as OscarProtocol;
                if (oscarProtocol == null) return;

                _oscarProtocol = oscarProtocol;
                _initialized = true;

                SaveLastUin(_oscarProtocol.UIN);
                _ = SaveLastUsedUinAsync(_oscarProtocol.UIN);
                UinTextBlock.Text = _oscarProtocol.UIN;
                LoadContacts(0x00000000);
            }

            // Подписки на события (каждый раз при входе на страницу)
            SubscribeToEvents();

            // Восстанавливаем сохранённый статус
            var settings = ApplicationData.Current.LocalSettings;
            object savedStatus = settings.Values["LastStatus"];
            if (savedStatus != null)
                _currentStatus = (uint)(long)savedStatus;

            UpdateOwnStatusIcon(_currentStatus);

            // Действия при возврате (например, из чата)
            ApplySettings();
            OnUnreadChanged();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            UnsubscribeFromEvents(); // защита от двойных подписок

            var reconnect = ((App)Application.Current).ReconnectService;
            if (reconnect != null)
            {
                reconnect.OnDisconnected += OnConnectionLost;
                reconnect.Reconnected += OnReconnected;
                reconnect.KickedOut += OnKickedOut;
            }

            if (_oscarProtocol != null)
            {
                _oscarProtocol.ContactStatusChanged += OnContactStatusChanged;
                _oscarProtocol.ContactRenamed += OnContactRenamed;
                _oscarProtocol.ContactRemoved += OnContactRemoved;
                _oscarProtocol.TemporaryContactAdded += OnTemporaryContactAdded;
                _oscarProtocol.DisconnectedByServer += OnKickedOut;
                
            }

            NotificationService.Instance.UnreadChanged += OnUnreadChanged;
        }

        private void UnsubscribeFromEvents()
        {
            var reconnect = ((App)Application.Current).ReconnectService;
            if (reconnect != null)
            {
                reconnect.OnDisconnected -= OnConnectionLost;
                reconnect.Reconnected -= OnReconnected;
                reconnect.KickedOut -= OnKickedOut;
            }

            if (_oscarProtocol != null)
            {
                try { _oscarProtocol.ContactStatusChanged -= OnContactStatusChanged; } catch { }
                try { _oscarProtocol.ContactRenamed -= OnContactRenamed; } catch { }
                try { _oscarProtocol.ContactRemoved -= OnContactRemoved; } catch { }
                try { _oscarProtocol.TemporaryContactAdded -= OnTemporaryContactAdded; } catch { }
            }

            try { NotificationService.Instance.UnreadChanged -= OnUnreadChanged; } catch { }
        }

        // ─────────────────────────────────────────────────────────────────────
        // КОНТЕКСТНОЕ МЕНЮ (удержание)
        // ─────────────────────────────────────────────────────────────────────
        private async void ContactItem_Holding(object sender,
            Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState != Windows.UI.Input.HoldingState.Started) return;

            var grid = sender as FrameworkElement;
            if (grid == null) return;

            _holdContact = grid.DataContext as Contact;
            if (_holdContact == null) return;

            await ShowContactContextMenuAsync(_holdContact);
        }

        private async Task ShowContactContextMenuAsync(Contact contact)
        {
            if (contact == null) return;

            if (contact.IsTemporary)
            {
                await ShowTemporaryContactMenuAsync(contact);
                return;
            }

            await ShowRegularContactMenuAsync(contact);
        }

        // Меню для временного контакта
        private async Task ShowTemporaryContactMenuAsync(Contact contact)
        {
            var dialog = new ContentDialog
            {
                Title = contact.Name + " (" + contact.Uin + ")",
                PrimaryButtonText = "Добавить в контакты",
                CloseButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await _oscarProtocol.AddContactAsync(contact.Uin, contact.Name);
                    contact.IsTemporary = false;
                    SortContacts();
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync("Ошибка: " + ex.Message);
                }
            }
        }

        // Меню для обычного контакта
        private async Task ShowRegularContactMenuAsync(Contact contact)
        {
            // Первый уровень: выбор действия
            var dialog = new ContentDialog
            {
                Title = contact.Name + " (" + contact.Uin + ")",
                PrimaryButtonText = "Переим./Удалить",
                SecondaryButtonText = "Информация",
                CloseButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    await ShowEditDeleteMenuAsync(contact);
                    break;
                case ContentDialogResult.Secondary:
                    await ShowContactInfoAsync(contact);
                    break;
            }
        }

        // Меню «Переименовать / Удалить»
        private async Task ShowEditDeleteMenuAsync(Contact contact)
        {
            var dialog = new ContentDialog
            {
                Title = contact.Name,
                PrimaryButtonText = "Переименовать",
                SecondaryButtonText = "Удалить",
                CloseButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    await ShowRenameDialogAsync(contact);
                    break;
                case ContentDialogResult.Secondary:
                    await ConfirmAndDeleteContactAsync(contact);
                    break;
            }
        }

        // Диалог переименования
        private async Task ShowRenameDialogAsync(Contact contact)
        {
            var input = new TextBox
            {
                Text = contact.Name,
                PlaceholderText = "Введите новое имя",
                Margin = new Thickness(0, 8, 0, 0)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Новое имя:" });
            panel.Children.Add(input);

            var dialog = new ContentDialog
            {
                Title = "Переименовать",
                Content = panel,
                PrimaryButtonText = "Сохранить",
                CloseButtonText = "Отмена"
            };

            // Фокус на поле ввода после открытия
            dialog.Opened += (s, args) =>
                input.Focus(FocusState.Programmatic);

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string newName = (input.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(newName) && newName != contact.Name)
                {
                    try
                    {
                        await _oscarProtocol.RenameContactAsync(contact, newName);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Rename ERROR] " + ex.Message);
                        await ShowErrorDialogAsync("Ошибка переименования: " + ex.Message);
                    }
                }
            }
        }

        // Подтверждение удаления
        private async Task ConfirmAndDeleteContactAsync(Contact contact)
        {
            var dialog = new ContentDialog
            {
                Title = "Подтверждение",
                Content = "Удалить " + contact.Name + "?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await _oscarProtocol.RemoveContactAsync(contact);
                    Contacts.Remove(contact);
                    SortContacts();
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync("Ошибка: " + ex.Message);
                }
            }
        }

        // Информация о контакте
        private async Task ShowContactInfoAsync(Contact contact)
        {
            var info = contact.Info;
            bool isOffline = contact.StatusIcon?.Contains("offline") ?? true;
            string statusText = isOffline ? "Офлайн" : (info?.StatusText ?? "Неизвестно");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("UIN: " + contact.Uin);
            sb.AppendLine("Имя: " + contact.Name);
            sb.AppendLine("Группа: " + (contact.Group ?? "—"));
            sb.AppendLine("Статус: " + statusText);

            if (info != null && !isOffline)
            {
                if (info.OnlineTime > 0)
                    sb.AppendLine("Онлайн: " + info.OnlineTimeText);
                if (info.SignonTime > 0)
                    sb.AppendLine("Зашел: " + info.SignonTimeText);
                if (info.MemberSince > 0)
                    sb.AppendLine("Регистрация: " + info.MemberSinceText);
                if (!string.IsNullOrEmpty(info.StatusMessage))
                    sb.AppendLine("Сообщение: " + info.StatusMessage);
            }

            var dialog = new ContentDialog
            {
                Title = contact.Name,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = sb.ToString(),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 400
                },
                CloseButtonText = "Закрыть"
            };

            await dialog.ShowAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // ПЕРЕМЕЩЕНИЕ В ГРУППУ
        // ─────────────────────────────────────────────────────────────────────
        private async Task ShowMoveToGroupDialogAsync(Contact contact)
        {
            var groups = _oscarProtocol.GetGroups()
                .Where(g => g.GroupId != 0x0000 && g.GroupId != contact.GroupId)
                .ToList();

            if (groups.Count == 0)
            {
                await ShowInfoDialogAsync("Других групп нет", "Переместить");
                return;
            }

            var listBox = new ListBox
            {
                ItemsSource = groups.Select(g => g.Name).ToList(),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var dialog = new ContentDialog
            {
                Title = "Переместить " + contact.Name,
                Content = listBox,
                PrimaryButtonText = "Переместить",
                CloseButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && listBox.SelectedIndex >= 0)
            {
                var selectedGroup = groups[listBox.SelectedIndex];
                try
                {
                    await _oscarProtocol.MoveContactAsync(contact, selectedGroup.GroupId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Move ERROR] " + ex.Message);
                    await ShowErrorDialogAsync("Ошибка перемещения: " + ex.Message);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ОБРАБОТЧИКИ СОБЫТИЙ ПРОТОКОЛА
        // ─────────────────────────────────────────────────────────────────────
        private void OnTemporaryContactAdded(Contact contact)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!Contacts.Any(c => c.Uin == contact.Uin))
                {
                    Contacts.Add(contact);
                    SortContacts();
                }
            });
        }

        private void OnContactRenamed(string uin, string newName)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, SortContacts);
        }

        private void OnContactRemoved(string uin)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var toRemove = Contacts.Where(c => c.Uin == uin).ToList();
                foreach (var c in toRemove)
                    Contacts.Remove(c);
                SortContacts();
            });
        }

        private void OnContactStatusChanged()
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SortContacts();
                RefreshView();
            });
        }

        private void OnConnectionLost()
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UinTextBlock.Text = "Соединение...";
                foreach (var contact in Contacts)
                {
                    contact.StatusIcon = "/Assets/statuses/offline.png";
                    contact.IsNewOnline = false;
                }
            });
        }

        private async void OnReconnected(OscarProtocol newOscar)
        {
            if (_oscarProtocol != null)
                _oscarProtocol.ContactStatusChanged -= OnContactStatusChanged;

            _oscarProtocol = newOscar;
            _oscarProtocol.ContactStatusChanged += OnContactStatusChanged;

            var fresh = await _oscarProtocol.GetContactsAsync(0);

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UinTextBlock.Text = _oscarProtocol.UIN;
                Contacts.Clear();
                foreach (var c in fresh)
                    Contacts.Add(c);
                SortContacts();
                RefreshView();
            });
        }

        private async void OnKickedOut(string reason)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await ShowInfoDialogAsync(reason, "Отключен");
                ((App)Application.Current).ReconnectService = null;
                ((App)Application.Current).Oscar = null;
                Frame.Navigate(typeof(LoginPage));
            });
        }

        private void OnUnreadChanged()
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (var contact in Contacts)
                    contact.UnreadCount = NotificationService.Instance.GetUnread(contact.Uin);
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // СТАТУС
        // ─────────────────────────────────────────────────────────────────────
        private void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            _statusPanelVisible = !_statusPanelVisible;
            StatusPanel.Visibility = _statusPanelVisible
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void StatusPanelClose_Click(object sender, RoutedEventArgs e)
        {
            StatusPanel.Visibility = Visibility.Collapsed;
            _statusPanelVisible = false;
        }

        private async void SetStatus_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || _oscarProtocol == null) return;

            string tagStr = btn.Tag as string;
            if (string.IsNullOrEmpty(tagStr)) return;

            try
            {
                uint statusCode = Convert.ToUInt32(tagStr, 16);
                await _oscarProtocol.SendSetStatusAsync(statusCode);
                _currentStatus = statusCode;
                ApplicationData.Current.LocalSettings.Values["LastStatus"] = (long)statusCode;
                UpdateOwnStatusIcon(statusCode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Status ERROR] " + ex.Message);
            }

            StatusPanel.Visibility = Visibility.Collapsed;
            _statusPanelVisible = false;
        }

        private void UpdateOwnStatusIcon(uint statusCode)
        {
            string icon;
            switch (statusCode & 0xFFFF)
            {
                case 0x0001: icon = "away"; break;
                case 0x0002: icon = "dnd"; break;
                case 0x0004: icon = "na"; break;
                case 0x0010: icon = "busy"; break;
                case 0x0020: icon = "f4c"; break;
                case 0x0100: icon = "inv"; break;
                case 0x3000: icon = "evil"; break;
                case 0x4000: icon = "depressed"; break;
                case 0x5000: icon = "home"; break;
                case 0x6000: icon = "work"; break;
                case 0x2001: icon = "eating"; break;
                default: icon = "online"; break;
            }

            try
            {
                OwnStatusIcon.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(
                    new Uri("ms-appx:///Assets/statuses/" + icon + ".png"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[UpdateOwnStatusIcon ERROR] " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // СОРТИРОВКА И ЗАГРУЗКА
        // ─────────────────────────────────────────────────────────────────────
        private void SortContacts()
        {
            var sorted = Contacts
                .OrderBy(c =>
                {
                    if (c.StatusIcon == null) return 7;
                    if (c.StatusIcon.Contains("online")) return 0;
                    if (c.StatusIcon.Contains("f4c")) return 1;
                    if (c.StatusIcon.Contains("away")) return 2;
                    if (c.StatusIcon.Contains("busy")) return 3;
                    if (c.StatusIcon.Contains("dnd")) return 4;
                    if (c.StatusIcon.Contains("na")) return 5;
                    if (c.StatusIcon.Contains("inv")) return 6;
                    return 7; // offline
                })
                .ThenBy(c => c.Name)
                .ToList();

            Contacts.Clear();
            foreach (var c in sorted)
                Contacts.Add(c);
        }

        private async void LoadContacts(uint statusCode)
        {
            try
            {
                Contacts.Clear();
                var parsedContacts = await _oscarProtocol.GetContactsAsync(statusCode);
                foreach (var contact in parsedContacts)
                    Contacts.Add(contact);
                SortContacts();
                ApplySettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MainPage] Error loading contacts: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // НАСТРОЙКИ И ВИД
        // ─────────────────────────────────────────────────────────────────────
        public async void ApplySettings()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var settings = ApplicationData.Current.LocalSettings;

                object showGroups = settings.Values["ShowGroups"];
                _showGroups = showGroups is bool b1 && b1;

                object hideOffline = settings.Values["HideOffline"];
                _hideOffline = hideOffline is bool b2 && b2;

                string bgPath = settings.Values["BackgroundPath"] as string;
                object opacityObj = settings.Values["BackgroundOpacity"];
                double opacity = opacityObj is double d ? d : 100.0;

                await ApplyBackgroundAsync(bgPath, opacity);

                object contactOpacityObj = settings.Values["ContactOpacity"];
                double contactOpacity = contactOpacityObj is double cd ? cd : 100.0;
                byte alpha = (byte)(contactOpacity / 100.0 * 255);
                ((App)Application.Current).ContactAlpha = alpha;

                foreach (var c in Contacts)
                    c.NotifyBackgroundChanged();

                RefreshView();
            });
        }

        private async Task ApplyBackgroundAsync(string path, double opacityPercent)
        {
            if (string.IsNullOrEmpty(path))
            {
                ContactsListView.Background =
                    new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Transparent);
                return;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using (var stream = await file.OpenReadAsync())
                {
                    var bitmap = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                    await bitmap.SetSourceAsync(stream);

                    ContactsListView.Background = new Windows.UI.Xaml.Media.ImageBrush
                    {
                        ImageSource = bitmap,
                        Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill,
                        Opacity = opacityPercent / 100.0
                    };
                }
                Debug.WriteLine("[MainPage] Background applied");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MainPage] ApplyBackground error: " + ex.Message);
            }
        }

        private void RefreshView()
        {
            IEnumerable<Contact> filtered = _hideOffline
                ? Contacts.Where(c => c.StatusIcon != null && !c.StatusIcon.Contains("offline"))
                : (IEnumerable<Contact>)Contacts;

            if (_showGroups)
            {
                var groupDict = new Dictionary<string, ObservableCollection<Contact>>();

                foreach (var contact in filtered)
                {
                    string groupName = !string.IsNullOrEmpty(contact.Group)
                        ? contact.Group : "Без группы";

                    if (!groupDict.ContainsKey(groupName))
                        groupDict[groupName] = new ObservableCollection<Contact>();

                    groupDict[groupName].Add(contact);
                }

                var groupList = new ObservableCollection<ContactGroup>();
                foreach (var kvp in groupDict)
                    groupList.Add(new ContactGroup(kvp.Key, kvp.Value));

                var cvs = new Windows.UI.Xaml.Data.CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = groupList
                };

                ContactsListView.ItemsSource = cvs.View;
            }
            else
            {
                ContactsListView.ItemsSource =
                    new ObservableCollection<Contact>(filtered);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ВЫХОД
        // ─────────────────────────────────────────────────────────────────────
        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reconnect = ((App)Application.Current).ReconnectService;
                if (reconnect != null)
                {
                    reconnect.Stop();
                    ((App)Application.Current).ReconnectService = null;
                }

                UnsubscribeFromEvents();

                if (_oscarProtocol != null)
                {
                    try
                    {
                        await _oscarProtocol.SendSetStatusAsync(0xFFFFFFFF);
                        await Task.Delay(200);
                    }
                    catch { }

                    try { await _oscarProtocol.DisconnectAsync(); } catch { }

                    ((App)Application.Current).Oscar = null;
                    _oscarProtocol = null;
                }

                Contacts.Clear();
                _initialized = false;
                Frame.Navigate(typeof(LoginPage));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Logout ERROR] " + ex.Message);
                Frame.Navigate(typeof(LoginPage));
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        // ─────────────────────────────────────────────────────────────────────
        // НАВИГАЦИЯ К ДРУГИМ СТРАНИЦАМ
        // ─────────────────────────────────────────────────────────────────────
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void AcInfButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AccountInfoPage));
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SearchPage), _oscarProtocol);
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(InfoPage));
        }

        private void ContactsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedContact = e.ClickedItem as Contact;
            if (clickedContact != null && _oscarProtocol != null)
            {
                Frame.Navigate(typeof(ChatPage),
                    new Tuple<Contact, OscarProtocol>(clickedContact, _oscarProtocol));
            }
        }

        private void ContactButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var contact = button.DataContext as Contact;
            if (contact != null && _oscarProtocol != null)
            {
                Frame.Navigate(typeof(ChatPage),
                    new Tuple<Contact, OscarProtocol>(contact, _oscarProtocol));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ВСПОМОГАТЕЛЬНЫЕ ДИАЛОГИ
        // ─────────────────────────────────────────────────────────────────────
        private async Task ShowErrorDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Ошибка",
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        private async Task ShowInfoDialogAsync(string message, string title = "Информация")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // ПРОЧЕЕ
        // ─────────────────────────────────────────────────────────────────────
        private void CommandBar_Opened(object sender, object e)
        {
            Debug.WriteLine("AppBar открыт.");
        }
    }
}