using Abstractions.Commands;
using SimpleChat.Core.MessageFactory;
using SimpleChat.Core.MessageHandler;
using SimpleChat.Core.MessageService;
using SimpleChat.Core.UserRegistry;
using SimpleChat.Model;
using System;
using System.Collections.Generic;
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

            TxtLocalName.Text = _userInfo.LocalName;
            TxtLocalId.Text = _userInfo.UserId.ToString("N").Substring(0, 8) + "…";

            LstUsers.SelectionChanged += (_, __) =>
            {
                if (LstUsers.SelectedItem is string name)
                    TxtReceiver.Text = name;
            };
        }

        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LstMessages.Items.Add($"[{e.SendTime:HH:mm:ss}] {e.Sender}: {e.Message}");
                LstMessages.ScrollIntoView(LstMessages.Items[^1]);
            });
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            BtnRefresh.IsEnabled = false;
            string? previouslySelected = LstUsers.SelectedItem as string;
            try
            {
                List<string> users = await _connectionService.GetUsersAsync();

                LstUsers.Items.Clear();
                foreach (var name in users)
                    LstUsers.Items.Add(name);

                if (previouslySelected != null && LstUsers.Items.Contains(previouslySelected))
                    LstUsers.SelectedItem = previouslySelected;
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
            if (LstUsers.SelectedItem is not string user)
            {
                MessageBox.Show("Выберите пользователя в списке.");
                return;
            }

            try
            {
                BtnConnect.IsEnabled = false;
                int result = await _connectionService.ConnectAsync(user);
                MessageBox.Show($"Подключение к '{user}': {result}");
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
            if (LstUsers.SelectedItem is not string user)
            {
                MessageBox.Show("Выберите пользователя в списке.");
                return;
            }

            try
            {
                await _connectionService.DisconnectAsync(user, "user requested");
                LstUsers.Items.Remove(user);
                if (TxtReceiver.Text == user)
                    TxtReceiver.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отключения: {ex.Message}");
            }
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (LstUsers.SelectedItem is not string oldName)
            {
                MessageBox.Show("Выберите пользователя для переименования.");
                return;
            }

            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Новое имя для '{oldName}':", "Переименование", oldName);

            if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
                return;

            if (_connectionService.TryRename(oldName, newName))
            {
                int index = LstUsers.Items.IndexOf(oldName);
                if (index >= 0)
                {
                    LstUsers.Items[index] = newName;
                    LstUsers.SelectedItem = newName;
                }
            }
            else
            {
                MessageBox.Show("Не удалось переименовать (имя занято или не найдено).");
            }
        }

        private async void LstUsers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstUsers.SelectedItem is string)
                BtnConnect_Click(sender, e);
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            string message = TxtMessage.Text;
            string receiver = TxtReceiver.Text;

            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(receiver))
            {
                MessageBox.Show("Выберите получателя и введите текст.");
                return;
            }

            try
            {
                BtnSend.IsEnabled = false;
                await _messageFactory.SendMessageAsync(message, receiver);

                LstMessages.Items.Add($"[{DateTime.Now:HH:mm:ss}] Я → {receiver}: {message}");
                LstMessages.ScrollIntoView(LstMessages.Items[^1]);

                TxtMessage.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки: {ex.Message}");
            }
            finally
            {
                BtnSend.IsEnabled = true;
                TxtMessage.Focus();
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