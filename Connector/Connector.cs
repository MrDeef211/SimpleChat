using Abstractions.DTO;
using SimpleChat.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Xsl;

namespace Connector
{
    public class Connector : IConnector
    {
        // Словарь активных сокетов подключенных узлов
        private readonly ConcurrentDictionary<Guid, Socket> _activeConnections = new();

        // Карта известных адресов
        private readonly Dictionary<Guid, IPEndPoint> _knowConnections;

        private bool _disposed;
        private bool _isReceiving;
        private CancellationTokenSource? _receiveCts;
        private readonly object _receiveLock = new object();

        public event EventHandler<MessageDTO>? MessageReceived;
        public event EventHandler<PingDTO>? PingReceived;

        // Для разлечения типов пакетов
        private const byte MessagePacketType = 1;
        private const byte PingPacketType = 2;

        // Конструктор
        public Connector(Dictionary<Guid, IPEndPoint> knowConnections)
        {
            _knowConnections = knowConnections ?? throw new ArgumentException(null, nameof(knowConnections));
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
            {
                return 200;
            }

            if (!_knowConnections.TryGetValue(address, out var endPoint))
            {
                return 404;
            }

            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(endPoint, token).ConfigureAwait(false);

                _activeConnections[address] = socket;
                return 200; // Соединение успешное
            }
            catch
            {
                return 503; // Узел недоступен
            }
        }

        public void Disconnect(Guid address, string reason)
        {
            if(_activeConnections.TryRemove(address, out var socket))
            {
                try
                {
                    if(socket.Connected)
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
            lock (_receiveLock)
            {
                if (_isReceiving) return Task.CompletedTask;

                _isReceiving = true;
                _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            }
            Task.Run(() => GlobalReceiveMonitoringLoopAsync(_receiveCts.Token), _receiveCts.Token);

            return Task.CompletedTask;
        }

        public Task StopReciveAsync()
        {
            lock (_receiveLock)
            {
                if (!_isReceiving) return Task.CompletedTask;

                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                _receiveCts= null;
                _isReceiving = false;
            }
            
            return Task.CompletedTask;
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

        private async Task ReadFromSocketInternalAsync(Guid connectionsGuid, Socket socket, CancellationToken token)
        {
            try
            {
                if (!socket.Connected || token.IsCancellationRequested)
                {
                    return;
                }

                // 1. Читаем маркер типа пакета (1 байт)
                byte[] markerBuffer = new byte[1];
                int bytesRead = await socket.ReceiveAsync(markerBuffer, SocketFlags.None, token).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    Disconnect(connectionsGuid, "Удалённый узел разорвал соединение");
                    return;
                }
                byte packetType = markerBuffer[0];

                // 2. ИСПРАВЛЕНО: Надежное чтение ровно 4 байт длины JSON-пакета в цикле
                byte[] lengthBuffer = new byte[4];
                int totalLengthBytesRead = 0;
                while (totalLengthBytesRead < 4)
                {
                    token.ThrowIfCancellationRequested();
                    int read = await socket.ReceiveAsync(
                        new ArraySegment<byte>(lengthBuffer, totalLengthBytesRead, 4 - totalLengthBytesRead),
                        SocketFlags.None,
                        token).ConfigureAwait(false);

                    if (read == 0) return; // Сокет закрылся
                    totalLengthBytesRead += read;
                }

                int jsonLength = BitConverter.ToInt32(lengthBuffer, 0);

                // Защита от некорректных / слишком больших пакетов
                if (jsonLength <= 0 || jsonLength > 10 * 1024 * 1024)
                {
                    return;
                }

                // 3. Вычитываем тело JSON
                byte[] jsonBuffer = new byte[jsonLength];
                int totalBytesReceived = 0;
                while (totalBytesReceived < jsonLength)
                {
                    token.ThrowIfCancellationRequested();
                    // ИСПРАВЛЕНО: Добавлен generic-параметр <byte> в ArraySegment
                    int read = await socket.ReceiveAsync(
                        new ArraySegment<byte>(jsonBuffer, totalBytesReceived, jsonLength - totalBytesReceived),
                        SocketFlags.None,
                        token).ConfigureAwait(false);

                    if (read == 0)
                    {
                        return;
                    }
                    totalBytesReceived += read;
                }

                string json = Encoding.UTF8.GetString(jsonBuffer);

                // 4. ИСПРАВЛЕНО: Добавлены generic-типы <MessageDTO> и <PingDTO> в десериализатор
                if (packetType == MessagePacketType)
                {
                    var msgDto = JsonSerializer.Deserialize<MessageDTO>(json);
                    if (msgDto != null)
                    {
                        MessageReceived?.Invoke(this, msgDto);
                    }
                }
                else if (packetType == PingPacketType)
                {
                    var pingDto = JsonSerializer.Deserialize<PingDTO>(json);
                    if (pingDto != null)
                    {
                        PingReceived?.Invoke(this, pingDto);
                    }
                }
            }
            catch
            {
                // Игнорируем сетевые ошибки, чтобы не уронить весь сервис
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
