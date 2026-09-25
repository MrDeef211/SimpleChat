using System.Text.Json;

namespace Connector.Configuration
{
    /// <summary>
    /// Настройки транспорта. Загружаются из connector.json.
    /// </summary>
    public sealed class ConnectorSettings
    {
        /// <summary>
        /// TCP-порт, который слушает Connector
        /// </summary>
        public int TcpPort { get; set; } = 5001;

        /// <summary>
        /// UDP-порт для multicast-discovery
        /// </summary>
        public int DiscoveryPort { get; set; } = 5000;

        /// <summary>
        /// Multicast-адрес для discovery
        /// </summary>
        public string MulticastAddress { get; set; } = "239.255.42.99";

        /// <summary>
        /// Файл со статическим списком пиров (guid → "host:port")
        /// </summary>
        public string PeersFile { get; set; } = "peers.json";

        public static ConnectorSettings Load(string path = "connector.json")
        {
            if (!File.Exists(path))
                return new ConnectorSettings();  

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ConnectorSettings>(json)
                   ?? new ConnectorSettings();
        }
    }
}