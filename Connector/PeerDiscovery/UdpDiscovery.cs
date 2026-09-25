using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Connector.PeerDiscovery
{
    public sealed class UdpDiscovery : IPeerDiscovery, IDisposable
    {
        private readonly IPAddress _multicastAddress;
        private readonly int _discoveryPort;
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

        private record ByePacket(Guid Id);

        public UdpDiscovery(
        Guid myId,
        int myTcpPort,
        string? myName,
        int discoveryPort,
        IPAddress multicastAddress)
        {
            _myId = myId;
            _myTcpPort = myTcpPort;
            _myName = myName;
            _discoveryPort = discoveryPort;
            _multicastAddress = multicastAddress;

            _sendClient = new UdpClient();
            _sendClient.MulticastLoopback = true;
            _sendClient.Client.SetSocketOption(
                SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

            _receiveClient = new UdpClient();
            _receiveClient.Client.SetSocketOption(
                SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _receiveClient.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            _receiveClient.JoinMulticastGroup(multicastAddress);
        }

        public Task StartAsync(CancellationToken token = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ = AnnounceLoopAsync(_cts.Token);
            _ = ListenLoopAsync(_cts.Token);
            _ = CleanupLoopAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken token = default)
        {
            await SendGoodbyeAsync().ConfigureAwait(false);
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task AnnounceLoopAsync(CancellationToken token)
        {
            var endpoint = new IPEndPoint(_multicastAddress, _discoveryPort);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var hello = new HelloPacket(_myId, _myTcpPort, _myName);
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
                    Console.WriteLine($"[UdpDiscovery] Received {result.Buffer.Length}B " +
                        $"from {result.RemoteEndPoint}");

                    HelloPacket? hello = null;
                    ByePacket? bye = null;

                    try
                    {
                        hello = JsonSerializer.Deserialize<HelloPacket>(result.Buffer);
                    }
                    catch
                    {
                        try
                        {
                            bye = JsonSerializer.Deserialize<ByePacket>(result.Buffer);
                        }
                        catch { }
                    }
                    if (bye is not null)
                    {
                        if (bye.Id == _myId) continue;

                        _lastSeen.TryRemove(bye.Id, out _);   
                        Console.WriteLine($"[UdpDiscovery] BYE: {bye.Id:N}");
                        PeerLost?.Invoke(this, bye.Id);    
                        continue;
                    }
                    if (hello is null) continue;

                    Console.WriteLine($"[UdpDiscovery] Hello " +
                        $"from id={hello.Id:N}, tcpPort={hello.TcpPort}");
                    if (hello.Id == _myId) continue;

                    var endpoint = new IPEndPoint(result.RemoteEndPoint.Address, hello.TcpPort);
                    _lastSeen[hello.Id] = DateTime.UtcNow;

                    Console.WriteLine($"[UdpDiscovery] DISCOVERED: {hello.Id:N} @ {endpoint}");
                    PeerDiscovered?.Invoke(this, 
                        new PeerDiscoveredEventArgs(hello.Id, endpoint, hello.Name));
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
                var removed = 0;
                foreach (var (id, lastSeen) in _lastSeen)
                {
                    if (now - lastSeen > PeerTimeout && _lastSeen.TryRemove(id, out _))
                    {
                        Console.WriteLine($"[UdpDiscovery] LOST by timeout: {id:N}");
                        PeerLost?.Invoke(this, id);
                        removed++;
                    }
                }

                if (removed > 0) Console.WriteLine($"[UdpDiscovery] Cleanup removed {removed} peers");
            }
        }

        private async Task SendGoodbyeAsync()
        {
            try
            {
                var bye = JsonSerializer.SerializeToUtf8Bytes(new ByePacket(_myId));
                await _sendClient.SendAsync(bye, new IPEndPoint(_multicastAddress, _discoveryPort))
                                .ConfigureAwait(false);
            }
            catch { }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { StopAsync().GetAwaiter().GetResult(); } catch { }
            try { _receiveClient.DropMulticastGroup(_multicastAddress); } catch { }
            try { _receiveClient.Dispose(); } catch { }
            try { _sendClient.Dispose(); } catch { }
        }

        private sealed record HelloPacket(Guid Id, int TcpPort, string? Name);
    }
}