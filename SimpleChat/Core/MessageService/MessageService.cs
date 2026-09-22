using System.Collections.Concurrent;
using Abstractions.Commands;
using Abstractions.DTO;
using Abstractions.Interfaces;
using Microsoft.Win32;
using SimpleChat.Core.UserRegistry;
using SimpleChat.Model;

namespace SimpleChat.Core.MessageService
{
    public class MessageService : IMessageService, IConnectionService, IDisposable
    {
        public event EventHandler<ReceiveMessageCommand>? MessageReceived;

        private readonly IFixedConnector _connector;

        private readonly IUserRegistry _registry;

        private readonly Guid _localId;

        private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(2);

        private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

        public MessageService(IFixedConnector connector, IUserRegistry registry, UserInfo userInfo)
        {
            _connector = connector;
            _registry = registry;
            _localId = userInfo.UserId;
            _connector.MessageReceived += OnConnectorMessageReceived;
        }

        #region Отправка сообщений

        public void SendMessage(SendMessageCommand command)
        {
            var receiverId = _registry.GetId(command.Receiver); 
            var dto = CreateDTO(command);
            var task = Task.Run(() => _connector.Send(dto, receiverId));
            if (!task.Wait(SendTimeout))
                throw new TimeoutException($"Превышено время ожидания отправки ({SendTimeout.TotalSeconds} с).");
            task.GetAwaiter().GetResult();
        }

        public async Task SendMessageAsync(SendMessageCommand command)
        {
            var receiverId = _registry.GetId(command.Receiver);
            using var cts = new CancellationTokenSource(SendTimeout);
            try
            {
                await _connector.SendAsync(CreateDTO(command), receiverId, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw new TimeoutException($"Превышено время ожидания отправки ({SendTimeout.TotalSeconds} с).");
            }
        }

        #endregion

        #region Подключение и отключение

        public int Connect(string user)
        {
            var id = _registry.GetId(user);
            var result = _connector.Connect(id);
            return result;
        }

        public async Task<int> ConnectAsync(string user)
        {
            var id = _registry.GetId(user);
            return await _connector.ConnectAsync(id).ConfigureAwait(false);
        }

        public void Disconnect(string user, string reason)
        {
            var id = _registry.GetId(user);
            _connector.Disconnect(id, reason);
            _registry.Remove(user);
        }

        public async Task DisconnectAsync(string user, string reason)
        {
            var id = _registry.GetId(user);
            await _connector.DisconnectAsync(id, reason).ConfigureAwait(false);
            _registry.Remove(user);
        }

        #endregion

        #region Работа с пользователями

        public bool TryRename(string oldName, string newName) => _registry.TryRename(oldName, newName);

        public List<string> GetUsers()
        {
            var collected = new ConcurrentDictionary<Guid, byte>();

            void OnPing(object? _, PingDTO ping)
            {
                if (ping.Sender != _localId)
                    collected.TryAdd(ping.Sender, 0);
            }

            _connector.PingReceived += OnPing;
            try
            {
                _connector.GetUsers(new PingDTO(_localId, DateTime.UtcNow, "GetUsers"));
                Thread.Sleep(DiscoveryTimeout);
            }
            finally
            {
                _connector.PingReceived -= OnPing;
            }

            return collected.Keys.Select(_registry.GetOrAddName).ToList();
        }

        public async Task<List<string>> GetUsersAsync()
        {
            var collected = new ConcurrentDictionary<Guid, byte>();

            void OnPing(object? _, PingDTO ping)
            {
                if (ping.Sender != _localId)
                    collected.TryAdd(ping.Sender, 0);
            }

            _connector.PingReceived += OnPing;
            try
            {
                await _connector.GetUsersAsync(new PingDTO(_localId, DateTime.UtcNow, "GetUsers"))
                                .ConfigureAwait(false);
                await Task.Delay(DiscoveryTimeout).ConfigureAwait(false);
            }
            finally
            {
                _connector.PingReceived -= OnPing;
            }


            return collected.Keys.Select(_registry.GetOrAddName).ToList();
        }

        #endregion

        private void OnConnectorMessageReceived(object? sender, MessageDTO dto)
        {
            var senderName = _registry.GetOrAddName(dto.Sender);
            var command = new ReceiveMessageCommand(dto.Message, senderName, dto.SendTime);
            MessageReceived?.Invoke(this, command);
        }

        public void Dispose()
        {
            _connector.MessageReceived -= OnConnectorMessageReceived;
            MessageReceived = null;
        }

        private MessageDTO CreateDTO(SendMessageCommand command) =>
            new MessageDTO(command.Message, command.Sender, command.SendTime);

    }
}
