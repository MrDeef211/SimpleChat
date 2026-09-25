using System.Collections.Concurrent;
using System.Net;
using Connector.PeerDiscovery;

namespace Connector.PeerDirectory
{
    public sealed class HybridPeerDirectory : IPeerDirectory, IDisposable
    {
        private readonly ConcurrentDictionary<Guid, IPEndPoint> _peers = new();
        private readonly IPeerDiscovery? _discovery;

        public event EventHandler<Guid>? PeerLost;

        public HybridPeerDirectory(
            IPeerDirectory? staticPeers = null,
            IPeerDiscovery? discovery = null)
        {
            if (staticPeers is not null)
                foreach (var id in staticPeers.KnownPeers)
                    if (staticPeers.TryGetEndpoint(id, out var ep))
                        _peers[id] = ep;

            _discovery = discovery;
            if (_discovery is not null)
            {
                _discovery.PeerDiscovered += OnDiscovered;
                _discovery.PeerLost += OnLost;
            }
        }

        public IReadOnlyCollection<Guid> KnownPeers => _peers.Keys.ToArray();

        public bool TryGetEndpoint(Guid id, out IPEndPoint endpoint) =>
            _peers.TryGetValue(id, out endpoint!);

        private void OnDiscovered(object? sender, PeerDiscoveredEventArgs e) =>
            _peers[e.Id] = e.Endpoint;

        private void OnLost(object? sender, Guid id)
        {
            if (_peers.TryRemove(id, out _))
                PeerLost?.Invoke(this, id);
        }

        public void Dispose()
        {
            if (_discovery is not null)
            {
                _discovery.PeerDiscovered -= OnDiscovered;
                _discovery.PeerLost -= OnLost;
            }
        }
    }
}