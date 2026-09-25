using System.Net;
using System.Text.Json;

namespace Connector.PeerDirectory
{
    /// <summary>
    /// Реестр пиров, загружаемый из JSON-файла.
    /// </summary>
    public sealed class FilePeerDirectory : IPeerDirectory
    {
        private readonly Dictionary<Guid, IPEndPoint> _map;

        private FilePeerDirectory(Dictionary<Guid, IPEndPoint> map) => _map = map;

        public IReadOnlyCollection<Guid> KnownPeers => _map.Keys.ToArray();

        public bool TryGetEndpoint(Guid id, out IPEndPoint endpoint) =>
            _map.TryGetValue(id, out endpoint!);

        private static readonly JsonSerializerOptions Options = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// Загрузить реестр из JSON вида { "guid-string": "host:port", ... }.
        /// </summary>
        public static FilePeerDirectory FromJsonFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл адресов не найден: {filePath}", filePath);


            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(filePath), Options)
                ?? throw new InvalidOperationException($"Файл {filePath} пуст или некорректен.");

            var map = new Dictionary<Guid, IPEndPoint>();
            foreach (var (key, value) in raw)
            {
                if (!Guid.TryParse(key, out var id))
                    throw new InvalidOperationException($"Некорректный Guid в {filePath}: '{key}'.");

                var parts = value.Split(':');
                if (parts.Length != 2 ||
                    !IPAddress.TryParse(parts[0], out var ip) ||
                    !int.TryParse(parts[1], out var port))
                {
                    throw new InvalidOperationException(
                        $"Некорректный адрес в {filePath}: '{value}'. Ожидается 'host:port'.");
                }

                map[id] = new IPEndPoint(ip, port);
            }

            return new FilePeerDirectory(map);
        }

        /// <summary>
        /// Создать реестр из готового словаря (для тестов, конфигов в коде и т.п.).
        /// </summary>
        public static FilePeerDirectory FromDictionary(IDictionary<Guid, IPEndPoint> map) =>
            new FilePeerDirectory(new Dictionary<Guid, IPEndPoint>(map));
    }
}
