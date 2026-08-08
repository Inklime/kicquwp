using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace kicquwp
{


    public class ChatMessage : INotifyPropertyChanged
    {
        private string _text;
        private bool _isIncoming;
        private bool _isOutgoing;
        

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(nameof(Text)); }
        }

        public string SenderName { get; set; }
        public string Time { get; set; }

        public bool IsIncoming
        {
            get => _isIncoming;
            set { _isIncoming = value; OnPropertyChanged(nameof(IsIncoming)); }
        }

        public bool IsOutgoing
        {
            get => _isOutgoing;
            set { _isOutgoing = value; OnPropertyChanged(nameof(IsOutgoing)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class ChatPage : Page
    {
        private OscarProtocol _oscar;
        private Contact _contact;
        private bool _emojiVisible = false;
        private bool _emojiLoaded = false;
        private ObservableCollection<ChatMessage> _messages
            = new ObservableCollection<ChatMessage>();
        private ReconnectService _reconnect;
        private DispatcherTimer _typingTimer;
        private bool _isTyping = false;
        private bool _typingNotificationsEnabled = true;
        private ChatMessage _replyTo;
        private HashSet<string> _loadedMessageKeys = new HashSet<string>();
        private static Windows.UI.Xaml.Media.ImageBrush _cachedChatBackground = null;
        private static string _lastChatBackgroundPath = null;

        // ─────────────────────────────────────────────────────────────────
        // КОНСТРУКТОР
        // ─────────────────────────────────────────────────────────────────
        public ChatPage()
        {
            this.InitializeComponent();
            MessagesList.ItemsSource = _messages;
            EmojiItemsControl.ItemsSource = _availableEmojis;
        }

        // ─────────────────────────────────────────────────────────────────
        // КЛЮЧ ДЕДУПЛИКАЦИИ
        // ─────────────────────────────────────────────────────────────────
        private string MakeMessageKey(ChatMessage msg)
        {
            string textPart = msg.Text != null && msg.Text.Length > 50
                ? msg.Text.Substring(0, 50) : msg.Text ?? "";
            return (msg.IsOutgoing ? "O" : "I") + "|" + msg.Time + "|" + textPart;
        }

        // ─────────────────────────────────────────────────────────────────
        // НАВИГАЦИЯ
        // ─────────────────────────────────────────────────────────────────
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            try
            {
                string forwardText = null;
                var paramWithForward = e.Parameter as Tuple<Contact, OscarProtocol, string>;
                if (paramWithForward != null)
                {
                    _contact = paramWithForward.Item1;
                    _oscar = paramWithForward.Item2;
                    forwardText = paramWithForward.Item3;
                }
                else
                {
                    var param = e.Parameter as Tuple<Contact, OscarProtocol>;
                    if (param == null) return;
                    _contact = param.Item1;
                    _oscar = param.Item2;
                }

                DebugLogService.Log("[ChatPage] Step 1: params OK");

                _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                _typingTimer.Tick += OnTypingTimerTick;
                _oscar.TypingNotificationReceived += OnTypingNotification;
                DebugLogService.Log("[ChatPage] Step 2: timer OK");

                var reconnect = ((App)Application.Current).ReconnectService;
                if (reconnect != null)
                {
                    reconnect.Reconnected += OnReconnectedInChat;
                    reconnect.OnDisconnected += OnChatConnectionLost;
                }
                DebugLogService.Log("[ChatPage] Step 3: reconnect OK");

                var settings = ApplicationData.Current.LocalSettings;
                object typingEnabled = settings.Values["TypingNotifications"];
                _typingNotificationsEnabled = typingEnabled == null || (bool)typingEnabled;

                ContactNameTextBlock.Text = _contact.Name;
                ContactUinTextBlock.Text = _contact.Uin;
                DebugLogService.Log("[ChatPage] Step 4: UI text OK");

                ((App)Application.Current).ConnectionStateChanged += OnGlobalConnectionStateChanged;
                ApplyConnectionState();
                DebugLogService.Log("[ChatPage] Step 5: connection state OK");

                UpdateAppBar();
                DebugLogService.Log("[ChatPage] Step 6: appbar OK");

                _reconnect = ((App)Application.Current).ReconnectService;
                if (_reconnect != null)
                {
                    _reconnect.OnDisconnected += OnConnectionLost;
                    _reconnect.Reconnected += OnReconnectedInChat;
                }

                try
                {
                    string iconPath = _contact.StatusIcon?.TrimStart('/') ?? "";
                    if (!string.IsNullOrEmpty(iconPath))
                        ContactStatusIcon.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(
                            new Uri("ms-appx:///" + iconPath));
                }
                catch { }
                DebugLogService.Log("[ChatPage] Step 7: icon OK");

                NotificationService.Instance.ActiveChatUin = _contact.Uin;
                NotificationService.Instance.ClearUnread(_contact.Uin);
                DebugLogService.Log("[ChatPage] Step 8: notifications OK");

                _messages.Clear();
                _loadedMessageKeys.Clear();
                await LoadHistoryAsync();
                DebugLogService.Log("[ChatPage] Step 9: history OK");

                await ApplyChatBackgroundAsync();
                DebugLogService.Log("[ChatPage] Step 10: background OK");

                var pending = _oscar.GetAndClearPending(_contact.Uin);
                foreach (var msgParts in pending)
                {
                    var chatMsg = new ChatMessage
                    {
                        Text = msgParts[0],
                        SenderName = _contact.Name,
                        Time = msgParts[1],
                        IsIncoming = true,
                        IsOutgoing = false
                    };
                    _messages.Add(chatMsg);
                    await SaveMessageAsync(chatMsg);
                }
                DebugLogService.Log("[ChatPage] Step 11: pending OK");

                ScrollToBottom();
                _oscar.IncomingMessage += OnIncomingMessage;

                if (!string.IsNullOrEmpty(forwardText))
                {
                    MessageTextBox.Text = forwardText;
                    MessageTextBox.SelectionStart = forwardText.Length;
                    MessageTextBox.Focus(FocusState.Programmatic);
                }

                Window.Current.CoreWindow.VisibilityChanged += OnWindowVisibilityChanged;
                DebugLogService.Log("[ChatPage] Step 12: DONE");
            }
            catch (Exception ex)
            {
                DebugLogService.Log("[ChatPage] CRASH: " + ex.GetType().Name + ": " + ex.Message);
                DebugLogService.Log("[ChatPage] StackTrace: " + ex.StackTrace);
                System.Diagnostics.Debug.WriteLine("[ChatPage] CRASH: " + ex);
            }
        }

        private async void OnChatConnectionLost()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ContactUinTextBlock.Text = "Соединение...";
            });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            Window.Current.CoreWindow.VisibilityChanged -= OnWindowVisibilityChanged;
            ((App)Application.Current).ConnectionStateChanged -= OnGlobalConnectionStateChanged;
            NotificationService.Instance.ActiveChatUin = null;

            if (_oscar != null)
            {
                _oscar.TypingNotificationReceived -= OnTypingNotification;
                _oscar.IncomingMessage -= OnIncomingMessage;
                _ = _oscar.SendTypingNotificationAsync(_contact.Uin, 0x0000);
                var reconnect = ((App)Application.Current).ReconnectService;
                if (reconnect != null)
                {
                    reconnect.Reconnected -= OnReconnectedInChat;
                    reconnect.OnDisconnected -= OnChatConnectionLost;
                }
                if (_oscar != null)
                    _oscar.ConnectionLost -= OnChatConnectionLost;
            }

            _typingTimer?.Stop();

            if (_reconnect != null)
            {
                _reconnect.OnDisconnected -= OnConnectionLost;
                _reconnect.Reconnected -= OnReconnectedInChat;
            }
        }

        private void OnGlobalConnectionStateChanged()
        {
            var ignored = Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                ApplyConnectionState());
        }

        private void ApplyConnectionState()
        {
            bool connected = ((App)Application.Current).IsConnected;
            ContactUinTextBlock.Text = connected
                ? (_contact?.Uin ?? "")
                : "Соединение...";
        }

        // ─────────────────────────────────────────────────────────────────
        // APPBAR
        // ─────────────────────────────────────────────────────────────────
        private void UpdateAppBar()
        {
            var bar = BottomAppBar as CommandBar;
            if (bar == null) return;

            bar.SecondaryCommands.Clear();

            if (_contact.IsTemporary)
            {
                AddSecondaryButton(bar, "Добавить в контакты",
                    Symbol.Add, AddToContacts_Click);
                AddSecondaryButton(bar, "Скопировать UIN",
                    Symbol.Copy, CopyUin_Click);
                AddSecondaryButton(bar, "Очистить чат",
                    Symbol.Clear, ClearChat_Click);
            }
            else
            {
                AddSecondaryButton(bar, "Скопировать UIN",
                    Symbol.Copy, CopyUin_Click);
                AddSecondaryButton(bar, "Информация",
                    Symbol.People, ContactInfo_Click);
                AddSecondaryButton(bar, "Переименовать",
                    Symbol.Edit, RenameContact_Click);
                AddSecondaryButton(bar, "Удалить контакт",
                    Symbol.Delete, DeleteContact_Click);
                AddSecondaryButton(bar, "Очистить чат",
                    Symbol.Clear, ClearChat_Click);
            }
        }

        private void AddSecondaryButton(CommandBar bar, string label,
            Symbol icon, RoutedEventHandler handler)
        {
            var btn = new AppBarButton
            {
                Label = label,
                Icon = new SymbolIcon(icon)
            };
            btn.Click += handler;
            bar.SecondaryCommands.Add(btn);
        }

        // ─────────────────────────────────────────────────────────────────
        // ОБРАБОТЧИКИ КНОПОК APPBAR
        // ─────────────────────────────────────────────────────────────────
        private async void AddToContacts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _oscar.AddContactAsync(_contact.Uin, _contact.Name);
                _contact.IsTemporary = false;
                UpdateAppBar();
                await ShowInfoDialogAsync(
                    _contact.Name + " добавлен в контакты", "Готово");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Ошибка: " + ex.Message);
            }
        }

        private void CopyUin_Click(object sender, RoutedEventArgs e)
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(_contact.Uin);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }

        private async void ContactInfo_Click(object sender, RoutedEventArgs e)
        {
            var info = _contact.Info;
            bool isOffline = _contact.StatusIcon?.Contains("offline") ?? true;

            string statusText = isOffline ? "Офлайн"
                : (info?.StatusText ?? "Неизвестно");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("UIN: " + _contact.Uin);
            sb.AppendLine("Имя: " + _contact.Name);
            sb.AppendLine("Группа: " + (_contact.Group ?? "—"));
            sb.AppendLine("Статус: " + statusText);

            if (info != null && !isOffline)
            {
                if (!string.IsNullOrEmpty(info.StatusMessage))
                    sb.AppendLine("Доп. статус: " + info.StatusMessage);
                if (!string.IsNullOrEmpty(info.Mood))
                    sb.AppendLine("Настроение: " + info.MoodText);
                if (info.OnlineTime > 0)
                    sb.AppendLine("Онлайн: " + info.OnlineTimeText);
                if (info.SignonTime > 0)
                    sb.AppendLine("Зашел: " + info.SignonTimeText);
                if (info.MemberSince > 0)
                    sb.AppendLine("Регистрация: " + info.MemberSinceText);
                if (info.ExternalIp > 0)
                    sb.AppendLine("IP: " + info.ExternalIpText);
            }

            // Запрашиваем полную анкету
            OscarProtocol.UserFullInfo fullInfo = null;
            try
            {
                ushort seq = (ushort)new Random().Next(1, 60000);
                fullInfo = await _oscar.RequestFullUserInfoDetailedAsync(
                    _contact.Uin, seq);
            }
            catch (Exception ex)
            {
                DebugLogService.Log("[ContactInfo] " + ex.Message);
            }

            if (fullInfo != null)
            {
                sb.AppendLine();
                sb.AppendLine("— Анкета —");
                if (!string.IsNullOrEmpty(fullInfo.FirstName))
                    sb.AppendLine("Имя: " + fullInfo.FirstName);
                if (!string.IsNullOrEmpty(fullInfo.LastName))
                    sb.AppendLine("Фамилия: " + fullInfo.LastName);
                if (!string.IsNullOrEmpty(fullInfo.Nickname))
                    sb.AppendLine("Ник: " + fullInfo.Nickname);
                if (!string.IsNullOrEmpty(fullInfo.Email))
                    sb.AppendLine("Email: " + fullInfo.Email);
                if (!string.IsNullOrEmpty(fullInfo.HomeCity))
                    sb.AppendLine("Город: " + fullInfo.HomeCity);
                if (!string.IsNullOrEmpty(fullInfo.HomeState))
                    sb.AppendLine("Регион: " + fullInfo.HomeState);
                if (!string.IsNullOrEmpty(fullInfo.HomePhone))
                    sb.AppendLine("Телефон: " + fullInfo.HomePhone);
                if (!string.IsNullOrEmpty(fullInfo.CellPhone))
                    sb.AppendLine("Мобильный: " + fullInfo.CellPhone);
                if (!string.IsNullOrEmpty(fullInfo.HomeAddress))
                    sb.AppendLine("Адрес: " + fullInfo.HomeAddress);
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("(Анкета недоступна)");
            }

            var dialog = new ContentDialog
            {
                Title = _contact.Name,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = sb.ToString(),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 420
                },
                CloseButtonText = "Закрыть"
            };
            await dialog.ShowAsync();
        }

        private async void RenameContact_Click(object sender, RoutedEventArgs e)
        {
            await ShowRenameDialogAsync(_contact, async newName =>
            {
                await _oscar.RenameContactAsync(_contact, newName);
                ContactNameTextBlock.Text = _contact.Name;
            });
        }

        private async void DeleteContact_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Удаление",
                Content = "Удалить " + _contact.Name + " из списка контактов?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await _oscar.RemoveContactAsync(_contact);
                    Frame.GoBack();
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync("Ошибка удаления: " + ex.Message);
                }
            }
        }

        private async void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Очистить чат",
                Content = "Удалить всю историю переписки?",
                PrimaryButtonText = "Очистить",
                CloseButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            _messages.Clear();
            _loadedMessageKeys.Clear();
            try
            {
                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(
                    HistoryFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, "");
            }
            catch (Exception ex)
            {
                DebugLogService.Log("[ClearChat] " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ПЕРЕИМЕНОВАНИЕ (ContentDialog вместо Popup)
        // ─────────────────────────────────────────────────────────────────
        private async Task ShowRenameDialogAsync(Contact contact, Func<string, Task> onSave)
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

            dialog.Opened += (s, args) => input.Focus(FocusState.Programmatic);

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string newName = (input.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(newName) && newName != contact.Name)
                {
                    try { await onSave(newName); }
                    catch (Exception ex)
                    {
                        await ShowErrorDialogAsync("Ошибка: " + ex.Message);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ВХОДЯЩИЕ СООБЩЕНИЯ
        // ─────────────────────────────────────────────────────────────────
        private async void OnIncomingMessage(string senderUin, string text)
        {
            if (senderUin != _contact.Uin) return;
            _oscar.GetAndClearPending(_contact.Uin);

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var msg = new ChatMessage
                {
                    Text = text,
                    SenderName = _contact.Name,
                    Time = DateTime.Now.ToString("HH:mm"),
                    IsIncoming = true,
                    IsOutgoing = false
                };

                _messages.Add(msg);
                await SaveMessageAsync(msg);
                ScrollToBottom();
            });
        }

        // ─────────────────────────────────────────────────────────────────
        // ОТПРАВКА
        // ─────────────────────────────────────────────────────────────────
        private async void SendMessage_Click(object sender, RoutedEventArgs e)
            => await SendCurrentMessageAsync();

        private async void MessageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await SendCurrentMessageAsync();
            }
        }

        private async Task SendCurrentMessageAsync()
        {
            _isTyping = false;
            _typingTimer.Stop();
            await _oscar.SendTypingNotificationAsync(_contact.Uin, 0x0000);

            string text = (MessageTextBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text) || _oscar == null) return;

            string finalText = text;
            if (_replyTo != null)
            {
                string quoted = string.Join("\n",
                    _replyTo.Text.Split('\n').Select(l => "> " + l));
                finalText = quoted + "\n\n" + text;
            }

            MessageTextBox.Text = "";
            _replyTo = null;
            if (ReplyPreviewPanel != null)
                ReplyPreviewPanel.Visibility = Visibility.Collapsed;

            try
            {
                await Task.Run(() => _oscar.SendIcbmAsync(_contact.Uin, finalText));

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    var msg = new ChatMessage
                    {
                        Text = finalText,
                        SenderName = "Вы",
                        Time = DateTime.Now.ToString("HH:mm"),
                        IsIncoming = false,
                        IsOutgoing = true
                    };

                    string key = MakeMessageKey(msg);
                    if (!_loadedMessageKeys.Contains(key))
                    {
                        _loadedMessageKeys.Add(key);
                        _messages.Add(msg);
                        await SaveMessageAsync(msg);
                        ScrollToBottom();
                    }
                });
            }
            catch (Exception ex)
            {
                DebugLogService.Log("[ChatPage] Send error: " + ex.Message);
                await ShowErrorDialogAsync("Ошибка отправки: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ПЕЧАТАНИЕ
        // ─────────────────────────────────────────────────────────────────
        private async void OnTypingNotification(string senderUin, ushort type)
        {
            if (senderUin != _contact.Uin) return;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ContactUinTextBlock.Text = type == 0x0000
                    ? _contact.Uin : "печатает...";
            });
        }

        private async void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_typingNotificationsEnabled) return;

            string text = MessageTextBox.Text;
            if (!string.IsNullOrEmpty(text))
            {
                if (!_isTyping)
                {
                    _isTyping = true;
                    await _oscar.SendTypingNotificationAsync(_contact.Uin, 0x0002);
                }
                _typingTimer.Stop();
                _typingTimer.Start();
            }
            else
            {
                if (_isTyping)
                {
                    _isTyping = false;
                    _typingTimer.Stop();
                    await _oscar.SendTypingNotificationAsync(_contact.Uin, 0x0000);
                }
            }
        }

        private async void OnTypingTimerTick(object sender, object e)
        {
            _typingTimer.Stop();
            if (_isTyping)
            {
                _isTyping = false;
                await _oscar.SendTypingNotificationAsync(_contact.Uin, 0x0001);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // КОНТЕКСТНОЕ МЕНЮ СООБЩЕНИЯ
        // ─────────────────────────────────────────────────────────────────
        private void MessageBorder_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState != HoldingState.Started) return;

            var element = sender as FrameworkElement;
            var msg = element?.DataContext as ChatMessage;
            if (msg == null) return;

            var flyout = new MenuFlyout();

            var reply = new MenuFlyoutItem { Text = "Ответить" };
            reply.Click += (s, a) => StartReply(msg);
            flyout.Items.Add(reply);

            var forward = new MenuFlyoutItem { Text = "Переслать" };
            forward.Click += (s, a) => _ = ForwardMessageAsync(msg);
            flyout.Items.Add(forward);

            var copy = new MenuFlyoutItem { Text = "Копировать" };
            copy.Click += (s, a) => CopyMessageText(msg);
            flyout.Items.Add(copy);

            flyout.ShowAt(element);
        }

        private void StartReply(ChatMessage msg)
        {
            _replyTo = msg;
            string preview = msg.Text?.Length > 60
                ? msg.Text.Substring(0, 60) + "…" : msg.Text ?? "";
            ReplyPreviewText.Text = "Ответ на: " + preview;
            ReplyPreviewPanel.Visibility = Visibility.Visible;
            MessageTextBox.Focus(FocusState.Programmatic);
        }

        private void CancelReply_Click(object sender, RoutedEventArgs e)
        {
            _replyTo = null;
            ReplyPreviewPanel.Visibility = Visibility.Collapsed;
        }

        private void CopyMessageText(ChatMessage msg)
        {
            if (string.IsNullOrEmpty(msg?.Text)) return;
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(msg.Text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }

        private async Task ForwardMessageAsync(ChatMessage msg)
        {
            await ShowForwardContactPickerAsync(msg.Text);
        }

        // ─────────────────────────────────────────────────────────────────
        // ПЕРЕСЫЛКА — ContentDialog вместо Popup
        // ─────────────────────────────────────────────────────────────────
        private async Task ShowForwardContactPickerAsync(string textToForward)
        {
            var contacts = await _oscar.GetContactsAsync(0);
            if (contacts == null || contacts.Count == 0)
            {
                await ShowInfoDialogAsync("Список контактов пуст.", "Переслать");
                return;
            }

            var listBox = new ListBox
            {
                ItemsSource = contacts,
                DisplayMemberPath = "Name",
                MaxHeight = 400
            };

            var dialog = new ContentDialog
            {
                Title = "Переслать кому?",
                Content = listBox,
                PrimaryButtonText = "Переслать",
                CloseButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var chosen = listBox.SelectedItem as Contact;
                if (chosen != null)
                {
                    Frame.Navigate(typeof(ChatPage),
                        Tuple.Create(chosen, _oscar, textToForward));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ЭМОДЗИ
        // ─────────────────────────────────────────────────────────────────
        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            _emojiVisible = !_emojiVisible;
            EmojiPanel.Visibility = _emojiVisible
                ? Visibility.Visible : Visibility.Collapsed;

            if (_emojiVisible && !_emojiLoaded)
                LoadEmojiAnimationsAsync();
        }

        private async void LoadEmojiAnimationsAsync()
        {
            await Task.Delay(100);
            _emojiLoaded = true;
        }

        private void OnEmojiPicked_GridView(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as EmojiItem;
            if (item == null) return;

            int pos = MessageTextBox.SelectionStart;
            MessageTextBox.Text = MessageTextBox.Text.Insert(pos, item.Code);
            MessageTextBox.SelectionStart = pos + item.Code.Length;

            EmojiPanel.Visibility = Visibility.Collapsed;
            _emojiVisible = false;
            MessageTextBox.Focus(FocusState.Programmatic);
        }

        private void InsertEmoji_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string tag = btn?.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;

            int pos = MessageTextBox.SelectionStart;
            MessageTextBox.Text = (MessageTextBox.Text ?? "").Insert(pos, tag);
            MessageTextBox.SelectionStart = pos + tag.Length;

            EmojiPanel.Visibility = Visibility.Collapsed;
            _emojiVisible = false;
            MessageTextBox.Focus(FocusState.Programmatic);
        }

        // ─────────────────────────────────────────────────────────────────
        // ПЕРЕПОДКЛЮЧЕНИЕ
        // ─────────────────────────────────────────────────────────────────
        private void OnConnectionLost()
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                ContactUinTextBlock.Text = "Соединение...");
        }

        private void OnReconnectedInChat(OscarProtocol newOscar)
        {
            // Отписываемся от старого
            if (_oscar != null)
                _oscar.ConnectionLost -= OnChatConnectionLost;

            // Подписываемся на новый
            _oscar = newOscar;
            _oscar.IncomingMessage += OnIncomingMessage;
            _oscar.TypingNotificationReceived += OnTypingNotification;
            _oscar.ConnectionLost += OnChatConnectionLost;

            var ignored = Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ContactUinTextBlock.Text = _contact.Uin;
                });
        }
        // ─────────────────────────────────────────────────────────────────
        // ПРОКРУТКА
        // ─────────────────────────────────────────────────────────────────
        private void ScrollToBottom()
        {
            if (_messages.Count == 0) return;
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                MessagesList.ScrollIntoView(_messages[_messages.Count - 1]));
        }

        // ─────────────────────────────────────────────────────────────────
        // ФОН ЧАТА
        // ─────────────────────────────────────────────────────────────────
        private async Task ApplyChatBackgroundAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            string path = settings.Values["ChatBackgroundPath"] as string;

            // Если фон удалили
            if (string.IsNullOrEmpty(path))
            {
                MessagesList.Background = null;
                _cachedChatBackground = null;
                _lastChatBackgroundPath = null;
                return;
            }

            // МОМЕНТАЛЬНО применяем кэш, если картинка та же самая
            if (path == _lastChatBackgroundPath && _cachedChatBackground != null)
            {
                MessagesList.Background = _cachedChatBackground;
                return;
            }

            // Загружаем с диска только при первом входе или смене картинки
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using (var stream = await file.OpenReadAsync())
                {
                    var bitmap = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                    await bitmap.SetSourceAsync(stream);

                    _cachedChatBackground = new Windows.UI.Xaml.Media.ImageBrush
                    {
                        ImageSource = bitmap,
                        Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill,
                        Opacity = 0.3
                    };

                    _lastChatBackgroundPath = path;
                    MessagesList.Background = _cachedChatBackground;
                }
            }
            catch (Exception ex)
            {
                DebugLogService.Log("[ChatPage] ApplyChatBackground error: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ИСТОРИЯ
        // ─────────────────────────────────────────────────────────────────
        private string HistoryFileName
            => "history_" + _oscar.UIN + "_" + _contact.Uin + ".txt";

        private async Task SaveMessageAsync(ChatMessage msg)
        {
            try
            {
                string text = (msg.Text ?? "")
                    .Replace("\x01", " ")
                    .Replace("\x02", " ");

                string line = (msg.IsOutgoing ? "OUT" : "IN")
                    + "\x01" + msg.Time
                    + "\x01" + msg.SenderName
                    + "\x01" + text
                    + "\x02";

                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(
                    HistoryFileName, CreationCollisionOption.OpenIfExists);
                await FileIO.AppendTextAsync(file, line);
            }
            catch (Exception ex)
            {
                DebugLogService.Log("[History] Save error: " + ex.Message);
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.GetFileAsync(HistoryFileName);
                string content = await FileIO.ReadTextAsync(file);

                bool isNewFormat = content.Contains("\x02");
                string[] lines = isNewFormat
                    ? content.Split('\x02')
                    : content.Split('\n');
                char sep = isNewFormat ? '\x01' : '|';

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] parts = line.Split(new[] { sep }, 4);
                    if (parts.Length < 4) continue;

                    var msg = new ChatMessage
                    {
                        IsOutgoing = parts[0] == "OUT",
                        IsIncoming = parts[0] != "OUT",
                        Time = parts[1],
                        SenderName = parts[2],
                        Text = parts[3]
                    };

                    string key = MakeMessageKey(msg);
                    if (!_loadedMessageKeys.Contains(key))
                    {
                        _loadedMessageKeys.Add(key);
                        _messages.Add(msg);
                    }
                }

                ScrollToBottom();

                // Миграция в новый формат
                if (!isNewFormat && _messages.Count > 0)
                {
                    try
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var m in _messages)
                        {
                            string t = (m.Text ?? "")
                                .Replace("\x01", " ").Replace("\x02", " ");
                            sb.Append((m.IsOutgoing ? "OUT" : "IN")
                                + "\x01" + m.Time
                                + "\x01" + m.SenderName
                                + "\x01" + t + "\x02");
                        }
                        StorageFile nf = await folder.CreateFileAsync(
                            HistoryFileName, CreationCollisionOption.ReplaceExisting);
                        await FileIO.WriteTextAsync(nf, sb.ToString());
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────────────────
        // ВСПОМОГАТЕЛЬНЫЕ ДИАЛОГИ
        // ─────────────────────────────────────────────────────────────────
        private async Task ShowErrorDialogAsync(string message)
        {
            var d = new ContentDialog
            {
                Title = "Ошибка",
                Content = message,
                CloseButtonText = "OK"
            };
            await d.ShowAsync();
        }

        private async Task ShowDialogAsync(string message)
        {
            var d = new ContentDialog
            {
                Title = "",
                Content = message,
                CloseButtonText = "OK"
            };
            await d.ShowAsync();
        }

        private async Task ShowInfoDialogAsync(string message, string title = "")
        {
            var d = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };
            await d.ShowAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // НАВИГАЦИЯ НАЗАД
        // ─────────────────────────────────────────────────────────────────
        private void BackButton_Click(object sender, RoutedEventArgs e)
            => Frame.GoBack();

        // ─────────────────────────────────────────────────────────────────
        // СПИСОК ЭМОДЗИ
        // ─────────────────────────────────────────────────────────────────
        private readonly List<EmojiItem> _availableEmojis = new List<EmojiItem>
        {
            new EmojiItem { Code = "O:-)",         ImagePath = "ms-appx:///Assets/emoji/aa.gif" },
            new EmojiItem { Code = ":-)",           ImagePath = "ms-appx:///Assets/emoji/ab.gif" },
            new EmojiItem { Code = ":-(",           ImagePath = "ms-appx:///Assets/emoji/ac.gif" },
            new EmojiItem { Code = ";-)",           ImagePath = "ms-appx:///Assets/emoji/ad.gif" },
            new EmojiItem { Code = ":-P",           ImagePath = "ms-appx:///Assets/emoji/ae.gif" },
            new EmojiItem { Code = "8)",            ImagePath = "ms-appx:///Assets/emoji/af.gif" },
            new EmojiItem { Code = ":-D",           ImagePath = "ms-appx:///Assets/emoji/ag.gif" },
            new EmojiItem { Code = ":-[",           ImagePath = "ms-appx:///Assets/emoji/ah.gif" },
            new EmojiItem { Code = "=-O",           ImagePath = "ms-appx:///Assets/emoji/ai.gif" },
            new EmojiItem { Code = ":-*",           ImagePath = "ms-appx:///Assets/emoji/aj.gif" },
            new EmojiItem { Code = ":'(",           ImagePath = "ms-appx:///Assets/emoji/ak.gif" },
            new EmojiItem { Code = ":-X",           ImagePath = "ms-appx:///Assets/emoji/al.gif" },
            new EmojiItem { Code = ">:o",           ImagePath = "ms-appx:///Assets/emoji/am.gif" },
            new EmojiItem { Code = ":-|",           ImagePath = "ms-appx:///Assets/emoji/an.gif" },
            new EmojiItem { Code = ":-\\",          ImagePath = "ms-appx:///Assets/emoji/ao.gif" },
            new EmojiItem { Code = "*JOKINGLY*",    ImagePath = "ms-appx:///Assets/emoji/ap.gif" },
            new EmojiItem { Code = "]:->",          ImagePath = "ms-appx:///Assets/emoji/aq.gif" },
            new EmojiItem { Code = "[:-}",          ImagePath = "ms-appx:///Assets/emoji/ar.gif" },
            new EmojiItem { Code = "*KISSED*",      ImagePath = "ms-appx:///Assets/emoji/as.gif" },
            new EmojiItem { Code = ":-!",           ImagePath = "ms-appx:///Assets/emoji/at.gif" },
            new EmojiItem { Code = "*TIRED*",       ImagePath = "ms-appx:///Assets/emoji/au.gif" },
            new EmojiItem { Code = "*STOP*",        ImagePath = "ms-appx:///Assets/emoji/av.gif" },
            new EmojiItem { Code = "*KISSING*",     ImagePath = "ms-appx:///Assets/emoji/aw.gif" },
            new EmojiItem { Code = "@}->--",        ImagePath = "ms-appx:///Assets/emoji/ax.gif" },
            new EmojiItem { Code = "*THUMBS UP*",   ImagePath = "ms-appx:///Assets/emoji/ay.gif" },
            new EmojiItem { Code = "*DRINK*",       ImagePath = "ms-appx:///Assets/emoji/az.gif" },
            new EmojiItem { Code = "*IN LOVE*",     ImagePath = "ms-appx:///Assets/emoji/ba.gif" },
            new EmojiItem { Code = "@=",            ImagePath = "ms-appx:///Assets/emoji/bb.gif" },
            new EmojiItem { Code = "*HELP*",        ImagePath = "ms-appx:///Assets/emoji/bc.gif" },
            new EmojiItem { Code = "\\m/",          ImagePath = "ms-appx:///Assets/emoji/bd.gif" },
            new EmojiItem { Code = "%)",            ImagePath = "ms-appx:///Assets/emoji/be.gif" },
            new EmojiItem { Code = "*OK*",          ImagePath = "ms-appx:///Assets/emoji/bf.gif" },
            new EmojiItem { Code = "*WASSUP*",      ImagePath = "ms-appx:///Assets/emoji/bg.gif" },
            new EmojiItem { Code = "*SORRY*",       ImagePath = "ms-appx:///Assets/emoji/bh.gif" },
            new EmojiItem { Code = "*BRAVO*",       ImagePath = "ms-appx:///Assets/emoji/bi.gif" },
            new EmojiItem { Code = "*ROFL*",        ImagePath = "ms-appx:///Assets/emoji/bj.gif" },
            new EmojiItem { Code = "*PARDON*",      ImagePath = "ms-appx:///Assets/emoji/bk.gif" },
            new EmojiItem { Code = "*NO*",          ImagePath = "ms-appx:///Assets/emoji/bl.gif" },
            new EmojiItem { Code = "*CRAZY*",       ImagePath = "ms-appx:///Assets/emoji/bm.gif" },
            new EmojiItem { Code = "*DONT_KNOW*",   ImagePath = "ms-appx:///Assets/emoji/bn.gif" },
            new EmojiItem { Code = "*DANCE*",       ImagePath = "ms-appx:///Assets/emoji/bo.gif" },
            new EmojiItem { Code = "*YAHOO*",       ImagePath = "ms-appx:///Assets/emoji/bp.gif" },
        };
        private void OnWindowVisibilityChanged(CoreWindow sender, VisibilityChangedEventArgs args)
        {
            if (!args.Visible)
            {
                // Приложение свернули или ушли из него (пользователь не видит чат) — 
                // сбрасываем ActiveChatUin, чтобы входящие сообщения присылали тосты
                NotificationService.Instance.ActiveChatUin = null;
            }
            else
            {
                // Пользователь вернулся в приложение и оно на экране — 
                // если мы всё еще в этом чате, снова блокируем тосты для него
                if (_contact != null)
                {
                    NotificationService.Instance.ActiveChatUin = _contact.Uin;
                }
            }
        }
    }
}