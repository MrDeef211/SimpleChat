using System.Net;
using Connector.Configuration;
using Connector.PeerDirectory;
using Connector.PeerDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Abstractions.Interfaces;

namespace Connector.Extensions
{
    public static class ConnectorServiceCollectionExtensions
    {
        /// <summary>
        /// Регистрирует транспортный слой: коннектор, discovery, реестр пиров.
        /// Настройки читаются из connector.json.
        /// </summary>
        public static IServiceCollection AddConnectorServices(
            this IServiceCollection services,
            string settingsPath = "connector.json")
        {
            var settings = ConnectorSettings.Load(settingsPath);

            // Discovery.
            services.AddSingleton<UdpDiscovery>(sp =>
            {

                var myId = sp.GetRequiredService<ConnectorContext>().UserId;
                var myName = sp.GetRequiredService<ConnectorContext>().DisplayName;

                return new UdpDiscovery(
                    myId,
                    settings.TcpPort,
                    myName,
                    settings.DiscoveryPort,
                    IPAddress.Parse(settings.MulticastAddress));
            });

            services.AddSingleton<IPeerDiscovery>(sp => sp.GetRequiredService<UdpDiscovery>());

            services.AddSingleton<IPeerDirectory>(sp =>
            {
                var discovery = sp.GetRequiredService<IPeerDiscovery>();

                IPeerDirectory? staticPeers = null;
                if (File.Exists(settings.PeersFile))
                    staticPeers = FilePeerDirectory.FromJsonFile(settings.PeersFile);

                return new HybridPeerDirectory(staticPeers, discovery);
            });

            services.AddSingleton<IConnector>(sp =>
            {
                var ctx = sp.GetRequiredService<ConnectorContext>();
                var peers = sp.GetRequiredService<IPeerDirectory>();
                var discovery = sp.GetRequiredService<IPeerDiscovery>();

                return new Connector(peers, ctx.UserId, settings.TcpPort, discovery);
            });

            return services;
        }
    }
}