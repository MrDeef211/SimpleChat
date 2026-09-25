using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Abstractions.DTO;
using Connector.PeerDirectory;
using SimpleChat.Interfaces;


namespace Connector
{
    public class Connector : IConnector
    {
        // Словарь активных сокетов подключенных узлов
        private readonly ConcurrentDictionary<Guid, Socket> _activeConnections = new();

        // Карта известных адресов
        private readonly IPeerDirectory _peers;
        private readonly int _myListeningPort;
        private readonly Guid _myId;

        private TcpListener? _listener;
        private bool _disposed;
        private bool _isReceiving;
        private CancellationTokenSource? _receiveCts;
        private readonly object _receiveLock = new object();

        public event EventHandler<MessageDTO>? MessageReceived;
        public event EventHandler<PingDTO>? PingReceived;

        // Для разлечения типов пакетов
        private const byte MessagePacketType = 1;
        private const byte PingPacketType = 2;
        private const byte HelloPacketType = 3;

        // Конструктор
        public Connector(IPeerDirectory peers, Guid myId, int myListeningPort)
        {
            _peers = peers ?? throw new ArgumentNullException(nameof(peers));
            _myId = myId;
            _myListeningPort = myListeningPort;
        }

        #region Сериализация и отправка пакетов

        private byte[] SerializePacket<T>(byte packetType, T dto)
        {
            string json = JsonSerializer.Serialize(dto);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

            // Структура пакета: 1 байт типа + 4 байта длины + JSON
            byte[] packet = new byte[1 + 4 + jsonBytes.Length];
            packet[0] = packetType;
            Buffer.BlockCopy(lengthBytes, 0, packet, 1, 4);
            Buffer.BlockCopy(jsonBytes, 0, packet, 5, jsonBytes.Length);

            return packet;
        }

        private async Task<int> SendPacketAsync(byte[] packet, Guid receiver, CancellationToken token)
        {
            if (_disposed) return 400; // Узел уничтножен

            if (!_activeConnections.TryGetValue(receiver, out var socket) || !socket.Connected)
            {
                return 400; // Отсутствие подключение
            }

            try
            {
                await socket.SendAsync(packet, SocketFlags.None, token);
                return 200; // Успешно
            }
            catch
            {
                return 500; // Ошибка сети
            }
        }

        #endregion

        #region Сообщения

        public int Send(MessageDTO message, Guid receiver)
        {
            try
            {
                return Task.Run(() => SendAsync(message, receiver, CancellationToken.None)).GetAwaiter().GetResult();
            }
            catch
            {
                return 500;
            }
        }

        public async Task<int> SendAsync(MessageDTO message, Guid receiver, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            byte[] packet = SerializePacket(MessagePacketType, message);
            return await SendPacketAsync(packet, receiver, token).ConfigureAwait(false);

        }

        #endregion

        #region Пинги

        public void Ping(PingDTO ping, Guid receiver)
        {
            try
            {
                Task.Run(() => PingAsync(ping, receiver)).GetAwaiter().GetResult();
            }
            catch
            {

            }
        }

        public async Task PingAsync(PingDTO ping, Guid receiver)
        {
            byte[] packet = SerializePacket(PingPacketType, ping);
            await SendPacketAsync(packet, receiver, CancellationToken.None).ConfigureAwait(false);
        }

        public void Broadcast(PingDTO ping)
        {
            try
            {
                Task.Run(() => BroadcastAsync(ping)).GetAwaiter().GetResult();
            }
            catch
            {

            }
        }

        public async Task BroadcastAsync(PingDTO ping)
        {
            byte[] packet = SerializePacket(PingPacketType, ping);
            var sendTask = new List<Task<int>>();

            foreach (var connections in _activeConnections.Keys)
            {
                sendTask.Add(SendPacketAsync(packet, connections, CancellationToken.None));
            }

            await Task.WhenAll(sendTask).ConfigureAwait(false);
        }

        #endregion

        #region Соединение

        public int Connect(Guid address)
        {
            try
            {
                return Task.Run(() => ConnectAsync(address, CancellationToken.None)).GetAwaiter().GetResult();
            }
            catch
            {
                return 500;
            }
        }

        public async Task<int> ConnectAsync(Guid address, CancellationToken token = default)
        {
            if (_activeConnections.TryGetValue(address, out var existingSocket) && existingSocket.Connected)
                return 200;

            if (!_peers.TryGetEndpoint(address, out var endPoint))
                return 404;

            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(endPoint, token).ConfigureAwait(false);

                // РУКОПОЖАТИЕ 
                var hello = SerializePacket(HelloPacketType, new HelloDTO(_myId, _myListeningPort));
                await socket.SendAsync(hello, SocketFlags.None, token).ConfigureAwait(false);

                _activeConnections[address] = socket;
                return 200;
            }
            catch
            {
                return 503;
            }
        }

        public void Disconnect(Guid address, string reason)
        {
            if (_activeConnections.TryRemove(address, out var socket))
            {
                try
                {
                    if (socket.Connected)
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                }
                catch
                {

                }
                finally
                {
                    socket.Close();
                    socket.Dispose();
                }
            }
        }

        public async Task DisconnectAsync(Guid address, string reason)
        {
            await Task.Run(() => Disconnect(address, reason)).ConfigureAwait(false);
        }

        #endregion

        #region Приём

        public Task StartReciveAsync(CancellationToken token = default)
        {
            CancellationToken loopToken;

            lock (_receiveLock)
            {
                if (_isReceiving) return Task.CompletedTask;

                _isReceiving = true;
                _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                loopToken = _receiveCts.Token;

                _listener = new TcpListener(IPAddress.Any, _myListeningPort);
                _listener.Start();
            }

            _ = AcceptLoopAsync(loopToken);
            _ = Task.Run(() => GlobalReceiveMonitoringLoopAsync(loopToken), loopToken);

            return Task.CompletedTask;
        }

        public Task StopReciveAsync()
        {
            lock (_receiveLock)
            {
                if (!_isReceiving) return Task.CompletedTask;

                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                _receiveCts = null;

                try { _listener?.Stop(); } catch { }
                _listener = null;

                _isReceiving = false;
            }
            return Task.CompletedTask;
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var socket = await _listener.AcceptSocketAsync(token).ConfigureAwait(false);
                    _ = HandleNewConnectionAsync(socket, token);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private async Task HandleNewConnectionAsync(Socket socket, CancellationToken token)
        {
            try
            {
                // Читаем первый пакет — обязан быть Hello.
                var (packetType, jsonBytes) = await ReadPacketAsync(socket, token).ConfigureAwait(false);

                if (packetType != HelloPacketType)
                {
                    socket.Dispose();
                    return;
                }

                var hello = JsonSerializer.Deserialize<HelloDTO>(jsonBytes);
                if (hello is null || hello.Id == _myId)
                {
                    socket.Dispose();
                    return;
                }

                _activeConnections[hello.Id] = socket;
            }
            catch
            {
                try { socket.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Читает один пакет из сокета. Возвращает (тип пакета, JSON-байты).
        /// Если соединение закрыто — бросает OperationCanceledException.
        /// </summary>
        private static async Task<(byte PacketType, byte[] JsonBytes)> ReadPacketAsync(
            Socket socket, CancellationToken token)
        {

            var marker = new byte[1];
            int read = await socket.ReceiveAsync(marker, SocketFlags.None, token).ConfigureAwait(false);
            if (read == 0) throw new OperationCanceledException("Соединение закрыто.");

            var lengthBuffer = new byte[4];
            int total = 0;
            while (total < 4)
            {
                token.ThrowIfCancellationRequested();
                read = await socket.ReceiveAsync(
                    new ArraySegment<byte>(lengthBuffer, total, 4 - total),
                    SocketFlags.None, token).ConfigureAwait(false);
                if (read == 0) throw new OperationCanceledException("Соединение закрыто.");
                total += read;
            }

            int jsonLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (jsonLength <= 0 || jsonLength > 10 * 1024 * 1024)
                throw new InvalidOperationException($"Некорректная длина пакета: {jsonLength}.");

            var jsonBuffer = new byte[jsonLength];
            total = 0;
            while (total < jsonLength)
            {
                token.ThrowIfCancellationRequested();
                read = await socket.ReceiveAsync(
                    new ArraySegment<byte>(jsonBuffer, total, jsonLength - total),
                    SocketFlags.None, token).ConfigureAwait(false);
                if (read == 0) throw new OperationCanceledException("Соединение закрыто.");
                total += read;
            }

            return (marker[0], jsonBuffer);
        }

        private async Task GlobalReceiveMonitoringLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var readTasks = new List<Task>();

                    foreach (var pair in _activeConnections)
                    {
                        readTasks.Add(ReadFromSocketInternalAsync(pair.Key, pair.Value, token));
                    }

                    if (readTasks.Count == 0)
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                        continue;
                    }

                    await Task.WhenAny(readTasks).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {

            }
        }

        private async Task ReadFromSocketInternalAsync(Guid connectionGuid, Socket socket, CancellationToken token)
        {
            try
            {
                if (!socket.Connected || token.IsCancellationRequested) return;

                var (packetType, jsonBytes) = await ReadPacketAsync(socket, token).ConfigureAwait(false);
                string json = Encoding.UTF8.GetString(jsonBytes);

                switch (packetType)
                {
                    case MessagePacketType:
                        var msg = JsonSerializer.Deserialize<MessageDTO>(json);
                        if (msg is not null) MessageReceived?.Invoke(this, msg);
                        break;

                    case PingPacketType:
                        var ping = JsonSerializer.Deserialize<PingDTO>(json);
                        if (ping is not null) PingReceived?.Invoke(this, ping);
                        break;

                }
            }
            catch (OperationCanceledException)
            {
                Disconnect(connectionGuid, "Соединение закрыто удалённой стороной.");
            }
            catch
            {

            }
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                StopReciveAsync().GetAwaiter().GetResult();
                foreach (var guid in _activeConnections.Keys)
                {
                    Disconnect(guid, "Connector уничтожен");
                }
                _disposed = true;
            }
        }

    }
}
