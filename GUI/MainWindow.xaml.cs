using Abstractions.Commands;
using GUI.ViewModels;
using SimpleChat.Core.MessageFactory;
using SimpleChat.Core.MessageHandler;
using SimpleChat.Core.MessageService;
using SimpleChat.Core.UserRegistry;
using SimpleChat.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GUI
{
    public partial class MainWindow : Window
    {
        private readonly IMessageFactory _messageFactory;
        private readonly IMessageHandler _messageHandler;
        private readonly IConnectionService _connectionService;
        private readonly IUserRegistry _userRegistry;
        private readonly UserInfo _userInfo;
        private readonly ObservableCollection<UserListItem> _users = new();

        public MainWindow(
            IMessageFactory messageFactory,
            IMessageHandler messageHandler,
            IConnectionService connectionService,
            IUserRegistry userRegistry,
            UserInfo userInfo)
        {
            InitializeComponent();

            _messageFactory = messageFactory;
            _messageHandler = messageHandler;
            _connectionService = connectionService;
            _userRegistry = userRegistry;
            _userInfo = userInfo;

            _messageHandler.MessageReceived += OnMessageReceived;
            _connectionService.UserConnected += OnUserConnected;
            _connectionService.UserDisconnected += OnUserDisconnected;

            LstUsers.ItemsSource = _users;
            LstUsers.SelectionChanged += LstUsers_SelectionChanged;

            TxtLocalName.Text = _userInfo.LocalName;
            TxtLocalId.Text = _userInfo.UserId.ToString("N")[..8] + "…";

            UpdateSendAvailability();
        }

        private void LstUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstUsers.SelectedItem is UserListItem item)
                TxtReceiver.Text = item.Name;
            else
                TxtReceiver.Text = string.Empty;

            UpdateSendAvailability();
        }

        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            var localTime = ToLocal(e.SendTime);

            Dispatcher.Invoke(() =>
            {
                LstMessages.Items.Add($"[{localTime:HH:mm:ss}] {e.Sender}: {e.Message}");
                LstMessages.ScrollIntoView(LstMessages.Items[^1]);
            });
        }

        private void OnUserConnected(object? sender, string name) =>
            Dispatcher.Invoke(() =>
            {
                var item = _users.FirstOrDefault(u => u.Name == name);
                if (item != null) item.IsConnected = true;
                UpdateSendAvailability();
            });

        private void OnUserDisconnected(object? sender, string name) =>
            Dispatcher.Invoke(() =>
            {
                var item = _users.FirstOrDefault(u => u.Name == name);
                if (item != null) item.IsConnected = false;

                if (TxtReceiver.Text == name)
                    TxtReceiver.Clear();

                UpdateSendAvailability();
            });

        /// <summary>
        /// Безопасно приводит DateTime к локальному времени, даже если Kind был потерян при сериализации.
        /// </summary>
        private static DateTime ToLocal(DateTime dt) => dt.Kind switch
        {
            DateTimeKind.Local => dt,
            DateTimeKind.Utc => dt.ToLocalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime()
        };

        private void UpdateSendAvailability()
        {
            BtnSend.IsEnabled = LstUsers.SelectedItem is UserListItem;
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            BtnRefresh.IsEnabled = false;
            string? previouslySelected = (LstUsers.SelectedItem as UserListItem)?.Name;

            try
            {
                List<string> users = await _connectionService.GetUsersAsync();
                var connected = _connectionService.GetConnectedUsers().ToHashSet();

                _users.Clear();
                foreach (var name in users)
                    _users.Add(new UserListItem(name, connected.Contains(name)));

                if (previouslySelected != null)
                    LstUsers.SelectedItem = _users.FirstOrDefault(u => u.Name == previouslySelected);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска пользователей: {ex.Message}");
            }
            finally
            {
                BtnRefresh.IsEnabled = true;
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (LstUsers.SelectedItem is not UserListItem user)
            {
                MessageBox.Show("Выберите пользователя в списке.");
                return;
            }

            try
            {
                BtnConnect.IsEnabled = false;
                int result = await _connectionService.ConnectAsync(user.Name);
                MessageBox.Show($"Подключение к '{user.Name}': {result}");
                // Индикатор обновится сам через событие UserConnected.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
            finally
            {
                BtnConnect.IsEnabled = true;
            }
        }

        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (LstUsers.SelectedItem is not UserListItem user)
            {
                MessageBox.Show("Выберите пользователя в списке.");
                return;
            }

            try
            {
                await _connectionService.DisconnectAsync(user.Name, "user requested");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отключения: {ex.Message}");
            }
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (LstUsers.SelectedItem is not UserListItem item)
            {
                MessageBox.Show("Выберите пользователя для переименования.");
                return;
            }

            string oldName = item.Name;
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Новое имя для '{oldName}':", "Переименование", oldName);

            if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
                return;

            if (!_connectionService.TryRename(oldName, newName))
            {
                MessageBox.Show("Не удалось переименовать (имя занято или не найдено).");
                return;
            }

            int index = _users.IndexOf(item);
            if (index >= 0)
            {
                var renamed = new UserListItem(newName, item.IsConnected);
                _users[index] = renamed;
                LstUsers.SelectedItem = renamed;
            }
        }

        private void LstUsers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstUsers.SelectedItem is UserListItem { IsConnected: false })
                BtnConnect_Click(sender, e);
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (LstUsers.SelectedItem is not UserListItem user)
            {
                MessageBox.Show("Выберите получателя.");
                return;
            }

            string message = TxtMessage.Text;
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Введите текст сообщения.");
                return;
            }

            BtnSend.IsEnabled = false;
            try
            {
                if (!user.IsConnected)
                {
                    int result = await _connectionService.ConnectAsync(user.Name);
                    if (result < 200 || result >= 300)
                    {
                        MessageBox.Show($"Не удалось подключиться к '{user.Name}' (код {result}).");
                        return;
                    }
                }

                await _messageFactory.SendMessageAsync(message, user.Name);

                LstMessages.Items.Add($"[{DateTime.Now:HH:mm:ss}] Я → {user.Name}: {message}");
                LstMessages.ScrollIntoView(LstMessages.Items[^1]);

                TxtMessage.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки: {ex.Message}");
            }
            finally
            {
                TxtMessage.Focus();
                UpdateSendAvailability(); 
            }
        }

        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                BtnSend_Click(sender, e);
            }
        }
    }
}