namespace Connector.PeerDiscovery
{
    public interface IPeerDiscovery : IDisposable
    {
        /// <summary>
        /// Объявить себя в сети
        /// </summary>
        Task StartAsync(CancellationToken token = default);

        /// <summary>
        /// Прекратить объявления и слушать перестать
        /// </summary>
        Task StopAsync(CancellationToken token = default);

        /// <summary>
        /// Новый узел обнаружен
        /// </summary>
        event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;

        /// <summary>
        /// Узел перестал отвечать (timeout)
        /// </summary>
        event EventHandler<Guid>? PeerLost;
    }
}
