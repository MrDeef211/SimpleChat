using SimpleChat.MessageFactory;
using SimpleChat.MessageHandler;
using SimpleChat.MessageService;
using SimpleChat.Model;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace SimpleChat.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddChatServices(this IServiceCollection services)
        {
            services.AddSingleton<UserInfo>(provider => GetUserInfo());

            services.AddSingleton<IConnectionService, MessageService.MessageService>();
            services.AddSingleton<IMessageService, MessageService.MessageService>();
            services.AddSingleton<IMessageHandler, MessageHandler.MessageHandler>();
            services.AddSingleton<IMessageFactory, MessageFactory.MessageFactory>();

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
