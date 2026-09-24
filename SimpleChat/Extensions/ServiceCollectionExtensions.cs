using System.Text.Json;
using Abstractions.Interfaces;
using Connector;
using Microsoft.Extensions.DependencyInjection;
using SimpleChat.Core.MessageFactory;
using SimpleChat.Core.MessageHandler;
using SimpleChat.Core.MessageService;
using SimpleChat.Core.UserRegistry;
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

            services.AddSingleton<IFixedConnector>(_ =>
            {
                var peers = new[]
                {
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                };

                return new FakeConnector
                {
                    KnownPeers = peers,
                    Latency = TimeSpan.FromMilliseconds(80),
                    DropRate = 0.05,
                    IncomingMessagePeriod = TimeSpan.FromSeconds(10)
                };
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
