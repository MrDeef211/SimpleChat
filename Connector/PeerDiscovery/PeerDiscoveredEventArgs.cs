using System.Net;

namespace Connector.PeerDiscovery
{
    public sealed class PeerDiscoveredEventArgs : EventArgs
    {
        public Guid Id { get; }
        public IPEndPoint Endpoint { get; }
        public string? DisplayName { get; }

        public PeerDiscoveredEventArgs(Guid id, IPEndPoint endpoint, string? displayName = null)
        {
            Id = id;
            Endpoint = endpoint;
            DisplayName = displayName;
        }
    }
}
