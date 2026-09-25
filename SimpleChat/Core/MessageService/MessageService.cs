using System.Collections.Concurrent;
using Abstractions.Commands;
using Abstractions.DTO;
using Abstractions.Core.UserRegistry;
using Abstractions.Interfaces;
using Abstractions.Model;

namespace Abstractions.Core.MessageService
{
    public class MessageService : IMessageService, IConnectionService, IStartableService, IDisposable
    {
        public event EventHandler<ReceiveMessageCommand>? MessageReceived;

        public event EventHandler<string>? UserConnected;

        public event EventHandler<string>? UserDisconnected;

        public event EventHandler<(string oldName, string newName)>? UserRenamed;

        private readonly IConnector _connector;

        private readonly IUserRegistry _registry;

        private readonly Guid _localId;

        private readonly ConcurrentDictionary<string, byte> _connected = new();

        private readonly TimeSpan _discoveryTimeout = TimeSpan.FromSeconds(2);

        private readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(5);

        public MessageService(IConnector connector,
            IUserRegistry registry, UserInfo userInfo,
            TimeSpan? discoveryTimeout = null,
            TimeSpan? sendTimeout = null)
        {
            _connector = connector;
            _registry = registry;
            _localId = userInfo.UserId;

            _discoveryTimeout = discoveryTimeout ?? TimeSpan.FromSeconds(2);
            _sendTimeout = sendTimeout ?? TimeSpan.FromSeconds(5);

            _connector.MessageReceived += OnConnectorMessageReceived;
            _connector.PeerDisconnected += OnConnectorPeerDisconnected;

        }

        #region Запуск конектора

        public async Task StartAsync(CancellationToken token = default)
        {
            await _connector.StartReciveAsync(token).ConfigureAwait(false);
        }

        public Task StopAsync(CancellationToken token = default)
        {
            return _connector.StopReciveAsync();
        }

        #endregion

        #region Отправка сообщений

        public void SendMessage(SendMessageCommand command)
        {
            var receiverId = _registry.GetId(command.Receiver);
            var dto = CreateDTO(command);
            var task = Task.Run(() => _connector.Send(dto, receiverId));
            if (!task.Wait(_sendTimeout))
                throw new TimeoutException($"Превышено время ожидания отправки ({_sendTimeout.TotalSeconds} с).");
            Console.WriteLine($"[MessageService] SendMessage: receiver='{command.Receiver}', message='{command.Message}'");
            task.GetAwaiter().GetResult();
        }

        public async Task SendMessageAsync(SendMessageCommand command)
        {
            var receiverId = _registry.GetId(command.Receiver);

            using var cts = new CancellationTokenSource(_sendTimeout);
            try
            {
                Console.WriteLine($"[MessageService] SendMessage: receiver='{command.Receiver}', message='{command.Message}'");
                int code = await _connector.SendAsync(CreateDTO(command), receiverId, cts.Token).ConfigureAwait(false);

                if (code < 200 || code >= 300)
                    throw new InvalidOperationException(
                        $"Не удалось отправить сообщение (код {code}). Пользователь не подключён.");
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw new TimeoutException($"Превышено время ожидания отправки ({_sendTimeout.TotalSeconds} с).");
            }
        }

        #endregion

        #region Подключение и отключение

        public int Connect(string user)
        {
            var id = _registry.GetId(user);
            var result = _connector.Connect(id);

            if (result >= 200 && result < 300)
                MarkConnected(user);

            return result;
        }

        public async Task<int> ConnectAsync(string user)
        {
            var id = _registry.GetId(user);
            var result = await _connector.ConnectAsync(id).ConfigureAwait(false);

            if (result >= 200 && result < 300)
                MarkConnected(user);

            return result;
        }

        public void Disconnect(string user, string reason)
        {
            var id = _registry.GetId(user);
            _connector.Disconnect(id, reason);
            MarkDisconnected(user);
        }

        public async Task DisconnectAsync(string user, string reason)
        {
            var id = _registry.GetId(user);
            await _connector.DisconnectAsync(id, reason).ConfigureAwait(false);
            MarkDisconnected(user);
        }

        #endregion

        #region Работа с пользователями

        private void OnConnectorPeerDisconnected(object? sender, Guid peerId)
        {
            Console.WriteLine($"[MessageService] PeerDisconnected: {peerId:N}");
            if (peerId == _localId) return;

            if (_registry.TryGetName(peerId, out var name))
            {
                Console.WriteLine($"[MessageService] MarkDisconnected: {name}");
                MarkDisconnected(name);
            }
            else
            {
                Console.WriteLine($"[MessageService] Unknown peer disconnected: {peerId:N}");
            }
        }

        public bool TryRename(string oldName, string newName)
        {
            if (!_registry.TryRename(oldName, newName))
                return false;

            UserRenamed?.Invoke(this, (oldName, newName));
            return true;
        }

        public List<string> GetUsers()
        {
            Thread.Sleep(_discoveryTimeout);

            return _connector.GetKnownPeers()
                .Where(id => id != _localId)
                .Select(_registry.GetOrAddName)
                .ToList();
        }

        public async Task<List<string>> GetUsersAsync()
        {
            await Task.Delay(_discoveryTimeout).ConfigureAwait(false);

            return _connector.GetKnownPeers()
                .Where(id => id != _localId)
                .Select(_registry.GetOrAddName)
                .ToList();
        }

        public bool IsConnected(string user) => _connected.ContainsKey(user);

        public IReadOnlyCollection<string> GetConnectedUsers() => _connected.Keys.ToArray();

        private void MarkConnected(string user)
        {
            if (_connected.TryAdd(user, 0))
                UserConnected?.Invoke(this, user);
        }

        private void MarkDisconnected(string user)
        {
            if (_connected.TryRemove(user, out _))
                UserDisconnected?.Invoke(this, user);
        }

        #endregion

        #region Приём сообщений

        private void OnConnectorMessageReceived(object? sender, MessageDTO dto)
        {
            var senderName = _registry.GetOrAddName(dto.Sender);

            MarkConnected(senderName);

            var command = new ReceiveMessageCommand(dto.Message, senderName, dto.SendTime);
            MessageReceived?.Invoke(this, command);
        }

        #endregion

        public void Dispose()
        {
            _connector.MessageReceived -= OnConnectorMessageReceived;
            _connector.PeerDisconnected -= OnConnectorPeerDisconnected;
            MessageReceived = null;
            _connected.Clear();
        }

        private MessageDTO CreateDTO(SendMessageCommand command) =>
            new MessageDTO(command.Message, command.Sender, command.SendTime);

    }
}
