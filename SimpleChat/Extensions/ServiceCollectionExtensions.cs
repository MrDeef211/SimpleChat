using System.Net;
using System.Text.Json;
using Connector.Configuration;
using Connector.Extensions;
using Connector.PeerDirectory;
using Connector.PeerDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Abstractions.Core.MessageFactory;
using Abstractions.Core.MessageHandler;
using Abstractions.Core.MessageService;
using Abstractions.Core.UserRegistry;
using Abstractions.Interfaces;
using Abstractions.Model;

namespace Abstractions.Extensions
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

            services.AddSingleton<ConnectorContext>(sp =>
            {
                var user = sp.GetRequiredService<UserInfo>();
                return new ConnectorContext(user.UserId, user.LocalName);
            });

            services.AddConnectorServices();

            services.AddSingleton<IUserRegistry, UserRegistry>();
            services.AddSingleton<MessageService>();

            services.AddSingleton<IConnectionService>(sp => sp.GetRequiredService<MessageService>());
            services.AddSingleton<IMessageService>(sp => sp.GetRequiredService<MessageService>());
            services.AddSingleton<IStartableService>(sp => sp.GetRequiredService<MessageService>());

            services.AddSingleton<IMessageHandler, MessageHandler>();
            services.AddSingleton<IMessageFactory, MessageFactory>();

            return services;
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
