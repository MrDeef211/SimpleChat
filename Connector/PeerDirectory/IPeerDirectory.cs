using System.Net;

namespace Connector.PeerDirectory
{
    public interface IPeerDirectory
    {
        /// <summary>
        /// Попытаться получить endpoint узла по его Guid.
        /// </summary>
        /// <param name="id">Guid-адрес узла.</param>
        /// <param name="endpoint">Endpoint узла, если он известен.</param>
        /// <returns><c>true</c>, если узел известен; иначе <c>false</c>.</returns>
        bool TryGetEndpoint(Guid id, out IPEndPoint endpoint);

        /// <summary>
        /// Все известные узлы.
        /// </summary>
        IReadOnlyCollection<Guid> KnownPeers
        {
            get;
        }
    }
}
