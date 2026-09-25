using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Abstractions.DTO;
using Connector.PeerDirectory;
using Connector.PeerDiscovery;
using Abstractions.Interfaces;


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

        private readonly IPeerDiscovery? _discovery;
        private TcpListener? _listener;

        private int _disposed;
        private bool _isReceiving;
        private CancellationTokenSource? _receiveCts;
        private readonly object _receiveLock = new object();

        public event EventHandler<MessageDTO>? MessageReceived;
        public event EventHandler<PingDTO>? PingReceived;
        public event EventHandler<Guid>? PeerDisconnected;

        // Для разлечения типов пакетов
        private const byte MessagePacketType = 1;
        private const byte PingPacketType = 2;
        private const byte HelloPacketType = 3;

        // Конструктор
        public Connector(
        IPeerDirectory peers,
        Guid myId,
        int myListeningPort,
        IPeerDiscovery? discovery = null)
        {
            _peers = peers ?? throw new ArgumentNullException(nameof(peers));
            _myId = myId;
            _myListeningPort = myListeningPort;
            _discovery = discovery;
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
            if (Volatile.Read(ref _disposed) != 0) return 400;

            Console.WriteLine($"[Connector] SendPacket to {receiver:N}, active={_activeConnections.Count}");

            if (!_activeConnections.TryGetValue(receiver, out var socket) || !socket.Connected)
            {
                return 400; // Отсутствие подключение
            }

            try
            {
                await socket.SendAsync(packet, SocketFlags.None, token);
                Console.WriteLine($"[Connector] SendPacket to {receiver:N} → code 200");
                return 200; // Успешно
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Connector] SendPacket err: {ex.Message}");
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
            foreach (var id in _peers.KnownPeers)
            {
                if (id == _myId) continue;
                if (_activeConnections.ContainsKey(id)) continue;

                try { await ConnectAsync(id).ConfigureAwait(false); }
                catch { }
            }

            var packet = SerializePacket(PingPacketType, ping);
            var tasks = _activeConnections.Keys
                .Select(id => SendPacketAsync(packet, id, CancellationToken.None))
                .ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        #endregion

        #region Соединение

        public IReadOnlyCollection<Guid> GetKnownPeers() => _peers.KnownPeers;

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
            if (_activeConnections.TryGetValue(address, out var existing) && IsSocketAlive(existing))
            {
                Console.WriteLine($"[Connector] Connect: already connected to {address:N}");
                return 200;
            }

            if (_activeConnections.TryRemove(address, out var dead))
            {
                try { dead.Dispose(); } catch { }
            }

            if (!_peers.TryGetEndpoint(address, out var endPoint))
            {
                Console.WriteLine($"[Connector] Connect: no endpoint for {address:N}");
                return 404;
            }

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            handshakeCts.CancelAfter(TimeSpan.FromSeconds(3));

            try
            {
                await socket.ConnectAsync(endPoint, handshakeCts.Token).ConfigureAwait(false);

                var hello = SerializePacket(HelloPacketType, new HelloDTO(_myId, _myListeningPort));
                await socket.SendAsync(hello, SocketFlags.None, handshakeCts.Token).ConfigureAwait(false);

                var (ackType, ackJson) = await ReadPacketAsync(socket, handshakeCts.Token).ConfigureAwait(false);
                if (ackType != HelloPacketType)
                {
                    Console.WriteLine($"[Connector] Handshake failed for {address:N}: unexpected packet type {ackType}");
                    socket.Dispose();
                    return 503;
                }

                var remoteHello = JsonSerializer.Deserialize<HelloDTO>(ackJson);
                if (remoteHello is null || remoteHello.Id != address)
                {
                    Console.WriteLine($"[Connector] Handshake failed for {address:N}: id mismatch");
                    socket.Dispose();
                    return 503;
                }

                _activeConnections[address] = socket;
                Console.WriteLine($"[Connector] Connect to {address:N} OK (handshake complete)");
                return 200;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                Console.WriteLine($"[Connector] Handshake timeout for {address:N}");
                try { socket.Dispose(); } catch { }
                return 503;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Connector] Connect err: {ex.Message}");
                try { socket.Dispose(); } catch { }
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
                        socket.Shutdown(SocketShutdown.Both);
                }
                catch { }
                finally
                {
                    try { socket.Close(); } catch { }
                    try { socket.Dispose(); } catch { }
                }

                Console.WriteLine($"[Connector] Disconnected {address:N} ({reason})");
                PeerDisconnected?.Invoke(this, address);
            }
        }

        public async Task DisconnectAsync(Guid address, string reason)
        {
            await Task.Run(() => Disconnect(address, reason)).ConfigureAwait(false);
        }

        #endregion

        #region Приём

        public async Task StartReciveAsync(CancellationToken token = default)
        {
            CancellationToken loopToken;

            lock (_receiveLock)
            {
                if (_isReceiving) return;

                _isReceiving = true;
                _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                loopToken = _receiveCts.Token;

                _listener = new TcpListener(IPAddress.Any, _myListeningPort);
                _listener.Start();
                Console.WriteLine($"[Connector] Listener started on port {_myListeningPort}");
            }

            if (_discovery is not null)
            {
                Console.WriteLine($"[Connector] Starting discovery...");
                await _discovery.StartAsync(loopToken).ConfigureAwait(false);
            }


            _ = AcceptLoopAsync(loopToken);
            _ = Task.Run(() => GlobalReceiveMonitoringLoopAsync(loopToken), loopToken);
        }

        public async Task StopReciveAsync()
        {
            CancellationTokenSource? cts;
            TcpListener? listener;

            lock (_receiveLock)
            {
                if (!_isReceiving) return;
                _isReceiving = false;

                cts = _receiveCts;
                _receiveCts = null;

                listener = _listener;
                _listener = null;
            }

            try { cts?.Cancel(); } catch { }
            try { listener?.Stop(); } catch { }
            cts?.Dispose();

            if (_discovery is not null)
                await _discovery.StopAsync().ConfigureAwait(false);
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

                if (_activeConnections.TryGetValue(hello.Id, out var existing) && IsSocketAlive(existing))
                {
                    Console.WriteLine($"[Connector] Duplicate connection from {hello.Id:N}, rejecting");
                    try { socket.Dispose(); } catch { }
                    return;
                }

                if (_activeConnections.TryRemove(hello.Id, out var dead))
                {
                    Console.WriteLine($"[Connector] Replacing dead connection to {hello.Id:N}");
                    try { dead.Dispose(); } catch { }
                }

                _activeConnections[hello.Id] = socket;
                Console.WriteLine($"[Connector] Accepted connection from {hello.Id:N}");

                var ack = SerializePacket(HelloPacketType, new HelloDTO(_myId, _myListeningPort));
                try
                {
                    await socket.SendAsync(ack, SocketFlags.None, token).ConfigureAwait(false);
                    Console.WriteLine($"[Connector] Sent hello-ack to {hello.Id:N}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Connector] Failed to send hello-ack to {hello.Id:N}: {ex.Message}");
                    _activeConnections.TryRemove(hello.Id, out _);
                    try { socket.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Connector] HandleNewConnection error: {ex.Message}");
                try { socket.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Проверяет, живое ли TCP-соединение, без блокировки.
        /// Возвращает false, если удалённая сторона прислала FIN/RST.
        /// </summary>
        private static bool IsSocketAlive(Socket socket)
        {
            try
            {
                if (!socket.Connected) return false;
                return !(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch
            {
                return false;
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

        /// <summary>
        /// Зовёт Disconnect только если в словаре всё ещё лежит ТОТ ЖЕ сокет
        /// </summary>
        private void DisconnectIfSame(Guid address, Socket expected, string reason)
        {
            if (_activeConnections.TryGetValue(address, out var current) && ReferenceEquals(current, expected))
            {
                Disconnect(address, reason);
            }
            else
            {
                try { expected.Dispose(); } catch { }
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
                DisconnectIfSame(connectionGuid, socket, "Соединение закрыто удалённой стороной.");
            }
            catch (SocketException)
            {
                DisconnectIfSame(connectionGuid, socket, "Сетевая ошибка при чтении.");
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Connector] Read error: {ex}");
            }
        }

        #endregion

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try { StopReciveAsync().GetAwaiter().GetResult(); } catch { }
            foreach (var guid in _activeConnections.Keys)
            {
                try { Disconnect(guid, "Connector уничтожен"); } catch { }
            }
        }

    }
}
