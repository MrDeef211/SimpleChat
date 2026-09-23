using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abstractions.DTO;
using Abstractions.Interfaces;

namespace Connector
{
    public class FakeConnector : IFixedConnector, IDisposable
    {
        public event EventHandler<MessageDTO>? MessageReceived;
        public event EventHandler<PingDTO>? PingReceived;

        /// <summary>
        /// Искусственная задержка сетевых операций
        /// </summary>
        public TimeSpan Latency { get; init; } = TimeSpan.FromMilliseconds(150);

        /// <summary>
        /// Вероятность «потери» сообщения (0.0 — 1.0)
        /// </summary>
        public double DropRate { get; init; } = 0.0;

        /// <summary>
        /// Код ответа на успешные операции
        /// </summary>
        public int OkCode { get; init; } = 200;

        /// <summary>
        /// Код ответа на ошибку (например, «не найден»)
        /// </summary>
        public int ErrorCode { get; init; } = 404;

        /// <summary>
        /// Набор «известных в сети» пиров (эмулируют других клиентов)
        /// </summary>
        public IReadOnlyCollection<Guid> KnownPeers
        {
            get => (IReadOnlyCollection<Guid>)_knownPeers.Keys;
            set
            {
                _knownPeers.Clear();
                foreach (var id in value)
                    _knownPeers[id] = new PeerState(id);
            }
        }

        /// <summary>
        /// Период автоматической генерации входящих сообщений (null — выключено)
        /// </summary>
        public TimeSpan? IncomingMessagePeriod { get; set; }

        private sealed class PeerState
        {
            public PeerState(Guid id) { Id = id; }
            public Guid Id { get; }
            public bool IsConnected { get; set; }
            public bool IsOnline { get; set; } = true;
        }

        private readonly ConcurrentDictionary<Guid, PeerState> _knownPeers = new();
        private readonly ConcurrentDictionary<Guid, byte> _connected = new();
        private readonly Random _rng = new();

        private CancellationTokenSource? _incomingCts;
        private bool _receiving;
        private bool _disposed;

        public int Connect(Guid address)
        {
            Simulate();
            if (!_knownPeers.TryGetValue(address, out var peer) || !peer.IsOnline)
                return ErrorCode;

            _connected[address] = 0;
            peer.IsConnected = true;
            return OkCode;
        }

        public async Task<int> ConnectAsync(Guid address, CancellationToken token = default)
        {
            await Task.Delay(Latency, token).ConfigureAwait(false);
            if (!_knownPeers.TryGetValue(address, out var peer) || !peer.IsOnline)
                return ErrorCode;

            _connected[address] = 0;
            peer.IsConnected = true;
            return OkCode;
        }

        public void Disconnect(Guid address, string reason)
        {
            Simulate();
            _connected.TryRemove(address, out _);
            if (_knownPeers.TryGetValue(address, out var peer))
                peer.IsConnected = false;
        }

        public Task DisconnectAsync(Guid address, string reason)
        {
            Disconnect(address, reason);
            return Task.CompletedTask;
        }

        public int Send(MessageDTO message, Guid receiver)
        {
            Simulate();
            if (!_connected.ContainsKey(receiver))
                return ErrorCode;

            if (_rng.NextDouble() < DropRate)
                return 0;

            _ = Task.Run(async () =>
            {
                await Task.Delay(Latency).ConfigureAwait(false);
                OnMessageReceived(new MessageDTO(message.Message, receiver, DateTime.UtcNow));
            });

            return OkCode;
        }

        public async Task<int> SendAsync(MessageDTO message, Guid receiver, CancellationToken token = default)
        {
            await Task.Delay(Latency, token).ConfigureAwait(false);
            if (!_connected.ContainsKey(receiver))
                return ErrorCode;

            if (_rng.NextDouble() < DropRate)
                return 0;

            _ = Task.Run(async () =>
            {
                await Task.Delay(Latency).ConfigureAwait(false);
                OnMessageReceived(new MessageDTO(message.Message, receiver, DateTime.UtcNow));
            }, token);

            return OkCode;
        }

        public void Ping(PingDTO ping, Guid receiver)
        {
            Simulate();
        }

        public async Task PingAsync(PingDTO ping, Guid receiver)
        {
            await Task.Delay(Latency).ConfigureAwait(false);
        }

        public void Broadcast(PingDTO ping)
        {
            Simulate();

            foreach (var peer in _knownPeers.Values.Where(p => p.IsOnline))
                OnPingReceived(new PingDTO(peer.Id, DateTime.UtcNow, "pong"));
        }

        public async Task BroadcastAsync(PingDTO ping)
        {
            await Task.Delay(Latency).ConfigureAwait(false);
            foreach (var peer in _knownPeers.Values.Where(p => p.IsOnline))
                OnPingReceived(new PingDTO(peer.Id, DateTime.UtcNow, "pong"));
        }

        public Task StartReciveAsync(CancellationToken token = default)
        {
            if (_receiving) return Task.CompletedTask;
            _receiving = true;
            _incomingCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            if (IncomingMessagePeriod is TimeSpan period)
                _ = IncomingLoopAsync(period, _incomingCts.Token);

            return Task.CompletedTask;
        }

        public Task StopReciveAsync()
        {
            _receiving = false;
            _incomingCts?.Cancel();
            _incomingCts?.Dispose();
            _incomingCts = null;
            return Task.CompletedTask;
        }

        private async Task IncomingLoopAsync(TimeSpan period, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(period, token).ConfigureAwait(false);

                    var sender = _connected.Keys.FirstOrDefault();
                    if (sender == Guid.Empty) sender = _knownPeers.Keys.FirstOrDefault();
                    if (sender == Guid.Empty) continue;

                    var text = $"Случайное сообщение #{_rng.Next(1000, 9999)}";
                    OnMessageReceived(new MessageDTO(text, sender, DateTime.UtcNow));
                }
            }
            catch (OperationCanceledException) { /* штатное завершение */ }
        }

        private void Simulate()
        {
            if (Latency > TimeSpan.Zero)
                Thread.Sleep(Latency);
        }

        private void OnMessageReceived(MessageDTO dto) =>
            MessageReceived?.Invoke(this, dto);

        private void OnPingReceived(PingDTO dto) =>
            PingReceived?.Invoke(this, dto);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _incomingCts?.Cancel();
            _incomingCts?.Dispose();
            _connected.Clear();
            _knownPeers.Clear();

            MessageReceived = null;
            PingReceived = null;
        }
    }
}

