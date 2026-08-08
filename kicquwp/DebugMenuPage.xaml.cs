using System;
using System.Collections.ObjectModel;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace kicquwp
{
    public sealed partial class DebugMenuPage : Page
    {
        public DebugMenuPage()
        {
            this.InitializeComponent();
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            string target = btn.Tag as string;

            switch (target)
            {
                case "LoginPage":
                    Frame.Navigate(typeof(LoginPage));
                    break;

                case "InfoPage":
                    Frame.Navigate(typeof(InfoPage));
                    break;

                case "DebugLogPage":
                    Frame.Navigate(typeof(DebugLogPage));
                    break;

                case "SettingsPage":
                    Frame.Navigate(typeof(SettingsPage));
                    break;

                case "MainPage":
                    {
                        var fakeOscar = CreateFakeOscarWithContacts();
                        Frame.Navigate(typeof(MainPage), fakeOscar);
                        break;
                    }

                case "ChatPage":
                    {
                        var fakeOscar = CreateFakeOscarWithContacts();
                        var fakeContact = new Contact
                        {
                            Uin = "9009",
                            Name = "Debug Contact",
                            GroupId = 1,
                            ItemId = 1,
                            Group = "General",
                            StatusIcon = "/Assets/statuses/f4c.png",
                            IsTemporary = false
                        };
                        Frame.Navigate(typeof(ChatPage),
                            new Tuple<Contact, OscarProtocol>(fakeContact, fakeOscar));
                        break;
                    }

                case "AccountInfoPage":
                    {
                        var fakeOscar = CreateFakeOscarWithContacts();
                        Frame.Navigate(typeof(AccountInfoPage), fakeOscar);
                        break;
                    }

                case "SearchPage":
                    {
                        var fakeOscar = CreateFakeOscarWithContacts();
                        Frame.Navigate(typeof(SearchPage), fakeOscar);
                        break;
                    }
            }
        }

        // Создаём OscarProtocol БЕЗ подключения к серверу — только для
        // передачи в конструкторы страниц, чтобы они не падали на null.
        private OscarProtocol CreateFakeOscarWithContacts()
        {
            var oscar = new OscarProtocol("111444", "debug", Window.Current.Dispatcher);

            var fakeContacts = new ObservableCollection<Contact>
            {
                new Contact
                {
                    Uin = "12345", Name = "Тестовый контакт 1",
                    GroupId = 1, ItemId = 1, Group = "General",
                    StatusIcon = "/Assets/statuses/online.png"
                },
                new Contact
                {
                    Uin = "123456789", Name = "Тестовый контакт 2",
                    GroupId = 1, ItemId = 2, Group = "General",
                    StatusIcon = "/Assets/statuses/away.png"
                },
                new Contact
                {
                    Uin = "000000", Name = "Оффлайн контакт",
                    GroupId = 2, ItemId = 3, Group = "Друзья",
                    StatusIcon = "/Assets/statuses/offline.png"
                }
            };

            oscar.SetFakeContactsForDebug(fakeContacts);
            return oscar;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}