using System.Net;
using System.Text.Json;
using Connector.PeerDirectory;
using Connector.PeerDiscovery;
using Microsoft.Extensions.DependencyInjection;
using SimpleChat.Core.MessageFactory;
using SimpleChat.Core.MessageHandler;
using SimpleChat.Core.MessageService;
using SimpleChat.Core.UserRegistry;
using SimpleChat.Interfaces;
using SimpleChat.Model;

namespace SimpleChat.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Регистрация сервисов и интерфейсов
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddChatServices(this IServiceCollection services)
        {
            services.AddSingleton<UserInfo>(provider => GetUserInfo());

            const int TcpPort = 5001;

            services.AddSingleton<UdpDiscovery>(sp =>
            {
                var user = sp.GetRequiredService<UserInfo>();
                return new UdpDiscovery(user.UserId, TcpPort, user.LocalName);
            });

            services.AddSingleton<IPeerDiscovery>(sp => sp.GetRequiredService<UdpDiscovery>());

            services.AddSingleton<IPeerDirectory>(sp =>
            {
                var discovery = sp.GetRequiredService<IPeerDiscovery>();
                var staticPeers = TryLoadPeersJson("peers.json");
                return new HybridPeerDirectory(staticPeers, discovery);
            });

            services.AddSingleton<IConnector>(sp =>
            {
                var user = sp.GetRequiredService<UserInfo>();
                var peers = sp.GetRequiredService<IPeerDirectory>();
                var discovery = sp.GetRequiredService<IPeerDiscovery>();

                return new Connector.Connector(peers, user.UserId, TcpPort, discovery);
            });

            services.AddSingleton<IUserRegistry, UserRegistry>();
            services.AddSingleton<MessageService>();

            services.AddSingleton<IConnectionService>(sp => sp.GetRequiredService<MessageService>());
            services.AddSingleton<IMessageService>(sp => sp.GetRequiredService<MessageService>());
            services.AddSingleton<IStartableService>(sp => sp.GetRequiredService<MessageService>());

            services.AddSingleton<IMessageHandler, MessageHandler>();
            services.AddSingleton<IMessageFactory, MessageFactory>();

            return services;
        }

        private static IDictionary<Guid, IPEndPoint>? TryLoadPeersJson(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var raw = JsonSerializer
                    .Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                if (raw is null) return null;

                var result = new Dictionary<Guid, IPEndPoint>();
                foreach (var (key, value) in raw)
                {
                    if (!Guid.TryParse(key, out var id)) continue;

                    var parts = value.Split(':');
                    if (parts.Length != 2) continue;
                    if (!IPAddress.TryParse(parts[0], out var ip)) continue;
                    if (!int.TryParse(parts[1], out var port)) continue;

                    result[id] = new IPEndPoint(ip, port);
                }
                Console.WriteLine($"[PEERS] Загружено {result.Count} адресов из {path}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PEERS] Ошибка загрузки {path}: {ex.Message}");
                return null;
            }
        }

        private static UserInfo GetUserInfo()
        {
            string filePath = "user_info.json";

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл конфигурации пользователя не найден: {filePath}");
            }

            string jsonString = File.ReadAllText(filePath);
            var userInfo = JsonSerializer.Deserialize<UserInfo>(jsonString);

            return userInfo ?? throw new InvalidOperationException("Не удалось десериализовать UserInfo");
        }
    }
}
