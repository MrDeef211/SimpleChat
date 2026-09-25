using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Connector.PeerDiscovery
{
    public sealed class UdpDiscovery : IPeerDiscovery, IDisposable
    {
        private static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.42.99");
        private const int DiscoveryPort = 5000;
        private static readonly TimeSpan AnnounceInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(10);

        private readonly Guid _myId;
        private readonly int _myTcpPort;
        private readonly string? _myName;
        private int _disposed;

        private readonly UdpClient _sendClient;
        private readonly UdpClient _receiveClient;

        private readonly ConcurrentDictionary<Guid, DateTime> _lastSeen = new();
        private CancellationTokenSource? _cts;

        public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;
        public event EventHandler<Guid>? PeerLost;

        public UdpDiscovery(Guid myId, int myTcpPort, string? myName = null)
        {
            _myId = myId;
            _myTcpPort = myTcpPort;
            _myName = myName;

            _sendClient = new UdpClient();
            _sendClient.MulticastLoopback = true;
            _sendClient.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastTimeToLive,
                1);

            _receiveClient = new UdpClient();
            _receiveClient.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);
            _receiveClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            _receiveClient.JoinMulticastGroup(MulticastAddress);
        }

        public Task StartAsync(CancellationToken token = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ = AnnounceLoopAsync(_cts.Token);
            _ = ListenLoopAsync(_cts.Token);
            _ = CleanupLoopAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken token = default)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            return Task.CompletedTask;
        }

        private async Task AnnounceLoopAsync(CancellationToken token)
        {
            var endpoint = new IPEndPoint(MulticastAddress, DiscoveryPort);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var hello = new HelloPacket(_myId, _myTcpPort, _myName, DateTime.UtcNow);
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(hello);
                    await _sendClient.SendAsync(bytes, endpoint, token).ConfigureAwait(false);
                    Console.WriteLine($"[UdpDiscovery] Announce: id={_myId:N}, port={_myTcpPort}");
                    await Task.Delay(AnnounceInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Console.WriteLine($"[UdpDiscovery] Announce err: {ex}"); }
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _receiveClient.ReceiveAsync(token).ConfigureAwait(false);
                    Console.WriteLine($"[UdpDiscovery] Received {result.Buffer.Length}B from {result.RemoteEndPoint}");

                    var hello = JsonSerializer.Deserialize<HelloPacket>(result.Buffer);
                    if (hello is null) continue;

                    Console.WriteLine($"[UdpDiscovery] Hello from id={hello.Id:N}, tcpPort={hello.TcpPort}");
                    if (hello.Id == _myId) continue;

                    var endpoint = new IPEndPoint(result.RemoteEndPoint.Address, hello.TcpPort);
                    _lastSeen[hello.Id] = DateTime.UtcNow;

                    Console.WriteLine($"[UdpDiscovery] DISCOVERED: {hello.Id:N} @ {endpoint}");
                    PeerDiscovered?.Invoke(this, new PeerDiscoveredEventArgs(hello.Id, endpoint, hello.Name));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Console.WriteLine($"[UdpDiscovery] Listen err: {ex}"); }
            }
        }

        private async Task CleanupLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
                var now = DateTime.UtcNow;
                foreach (var (id, lastSeen) in _lastSeen)
                {
                    if (now - lastSeen > PeerTimeout && _lastSeen.TryRemove(id, out _))
                    {
                        Console.WriteLine($"[UdpDiscovery] LOST: {id:N}");
                        PeerLost?.Invoke(this, id);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { StopAsync().GetAwaiter().GetResult(); } catch { }
            try { _receiveClient.DropMulticastGroup(MulticastAddress); } catch { }
            try { _receiveClient.Dispose(); } catch { }
            try { _sendClient.Dispose(); } catch { }
        }

        private sealed record HelloPacket(Guid Id, int TcpPort, string? Name, DateTime SentAt);
    }
}