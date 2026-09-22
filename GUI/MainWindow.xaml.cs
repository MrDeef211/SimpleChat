using Abstractions.Commands;
using SimpleChat.Core.MessageFactory;
using SimpleChat.Core.MessageHandler;
using SimpleChat.Core.MessageService;
using SimpleChat.Model;
using System;
using System.Windows;

namespace GUI
{
    public partial class MainWindow : Window
    {
        private readonly IMessageFactory _messageFactory;
        private readonly IMessageHandler _messageHandler;
        private readonly IConnectionService _connectionService;
        private readonly UserInfo _userInfo;

        // Внедряем зависимости через конструктор
        public MainWindow(
            IMessageFactory messageFactory,
            IMessageHandler messageHandler,
            IConnectionService connectionService,
            UserInfo userInfo)
        {
            InitializeComponent();

            _messageFactory = messageFactory;
            _messageHandler = messageHandler;
            _connectionService = connectionService;
            _userInfo = userInfo;

            _messageHandler.MessageReceived += OnMessageReceived;

            TxtUserId.Text = _userInfo.UserId.ToString();
            TxtUserName.Text = _userInfo.LocalName; 
        }

        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LstMessages.Items.Add($"[{e.SendTime:HH:mm:ss}] {e.Sender}: {e.Message}");
                LstMessages.ScrollIntoView(LstMessages.Items[^1]);
            });
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (Guid.TryParse(TxtUserId.Text, out Guid userId))
            {
                try
                {
                    BtnConnect.IsEnabled = false;
                    var result = await _connectionService.ConnectAsync(userId, TxtUserName.Text);
                    MessageBox.Show($"Результат подключения: {result}");
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
            else
            {
                MessageBox.Show("Некорректный формат User ID (должен быть Guid).");
            }
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            string message = TxtMessage.Text;
            string receiver = TxtReceiver.Text;

            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(receiver))
            {
                MessageBox.Show("Заполните получателя и текст сообщения.");
                return;
            }

            try
            {
                await _messageFactory.SendMessageAsync(message, receiver);

                LstMessages.Items.Add($"[{DateTime.Now:HH:mm:ss}] Я: {message}");
                LstMessages.ScrollIntoView(LstMessages.Items[^1]);

                TxtMessage.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки: {ex.Message}");
            }
        }
    }
}