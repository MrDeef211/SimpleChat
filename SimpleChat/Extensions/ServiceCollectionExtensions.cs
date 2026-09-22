using SimpleChat.Model;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using SimpleChat.Core.MessageService;
using SimpleChat.Core.MessageHandler;
using SimpleChat.Core.MessageFactory;
using SimpleChat.Core.UserRegistry;

namespace SimpleChat.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddChatServices(this IServiceCollection services)
        {
            services.AddSingleton<UserInfo>(provider => GetUserInfo());

            services.AddSingleton<IUserRegistry, UserRegistry>();
            services.AddSingleton<MessageService>();

            services.AddSingleton<IConnectionService>(sp => sp.GetRequiredService<MessageService>());
            services.AddSingleton<IMessageService>(sp => sp.GetRequiredService<MessageService>());

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
