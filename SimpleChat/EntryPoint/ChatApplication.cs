using Microsoft.Extensions.DependencyInjection;
using SimpleChat.Core.MessageService;
using SimpleChat.Extensions;

namespace SimpleChat.ChatApplication
{
    /// <summary>
    /// Точка входа в приложение чата. Управляет контейнером и жизненным циклом сервисов.
    /// </summary>
    public sealed class ChatApplication : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private ChatApplication(ServiceProvider provider) => _provider = provider;

        public IServiceProvider Services => _provider;

        /// <summary>
        /// Собирает контейнер, запускает все долгоживущие сервисы.
        /// </summary>
        /// <param name="configure">Дополнительная настройка (например, регистрация главного окна).</param>
        public static async Task<ChatApplication> StartAsync(
            Action<IServiceCollection>? configure = null,
            CancellationToken token = default)
        {
            var services = new ServiceCollection();
            services.AddChatServices();
            configure?.Invoke(services);

            var provider = services.BuildServiceProvider();
            var app = new ChatApplication(provider);

            foreach (var s in provider.GetServices<IStartableService>())
                await s.StartAsync(token).ConfigureAwait(false);

            return app;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var s in _provider.GetServices<IStartableService>().Reverse())
            {
                try { await s.StopAsync(CancellationToken.None).ConfigureAwait(false); }
                catch { }
            }
            await _provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}